// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.IPC;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The overlay data source's server against real sockets, on a free port picked for every test (never tosu's 24050).
    /// </summary>
    [TestFixture]
    public class BanchosucksOverlayServerTest
    {
        private int port;
        private string overlayFolder = null!;
        private string document = null!;
        private BanchosucksOverlayServer server = null!;

        [SetUp]
        public void SetUp()
        {
            port = FreePort();
            document = "{\"state\":{\"number\":0,\"name\":\"menu\"}}";

            overlayFolder = Path.Combine(Path.GetTempPath(), $"banchosucks-overlay-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(overlayFolder, "demo", "js"));
            File.WriteAllText(Path.Combine(overlayFolder, "demo", "index.html"), "<html>demo overlay</html>");
            File.WriteAllText(Path.Combine(overlayFolder, "demo", "js", "app.js"), "console.log('hi');");
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "banchosucks-overlay-secret.txt"), "secret");

            server = new BanchosucksOverlayServer(port, () => document, overlayFolder);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    server.StopAsync(cts.Token).Wait(TimeSpan.FromSeconds(6));
            }
            catch (Exception)
            {
                // a test that never started the server
            }

            server.Dispose();

            try
            {
                Directory.Delete(overlayFolder, true);
                File.Delete(Path.Combine(Path.GetTempPath(), "banchosucks-overlay-secret.txt"));
            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// A port nobody listens on right now.
        /// </summary>
        public static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int free = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            Assert.That(free, Is.Not.EqualTo(BanchosucksOverlayHttp.DEFAULT_PORT));
            return free;
        }

        [Test]
        public async Task TestWebSocketGetsDocumentOnConnectAndOnChange()
        {
            await server.StartAsync();

            using (var client = await Connect(port))
            {
                Assert.That(await Receive(client), Is.EqualTo(document), "the current document right after connecting");
                Assert.That(server.ClientCount, Is.EqualTo(1));

                // an unchanged document is not sent again: the next thing to arrive is the changed one
                server.Publish(document);
                server.Publish(document);
                await Task.Delay(200);
                server.Publish("{\"n\":1}");
                Assert.That(await Receive(client), Is.EqualTo("{\"n\":1}"));

                // larger than the 4096 bytes the game's websocket channel accepts when receiving: sending has no such limit
                string large = "{\"beatmap\":{\"title\":\"" + new string('x', 20000) + "\"}}";
                server.Publish(large);
                Assert.That(await Receive(client), Is.EqualTo(large));

                server.Publish("{\"n\":2}");
                Assert.That(await Receive(client), Is.EqualTo("{\"n\":2}"));

                // tosu overlays may send a filter request; it is ignored without breaking the connection
                await client.SendAsync(Encoding.UTF8.GetBytes("applyFilters:[\"beatmap\"]"), WebSocketMessageType.Text, true, CancellationToken.None);
                server.Publish("{\"n\":3}");
                Assert.That(await Receive(client), Is.EqualTo("{\"n\":3}"));
            }
        }

        [Test]
        public async Task TestTwoOverlays()
        {
            await server.StartAsync();

            using (var first = await Connect(port))
            using (var second = await Connect(port))
            {
                Assert.That(await Receive(first), Is.EqualTo(document));
                Assert.That(await Receive(second), Is.EqualTo(document));

                server.Publish("{\"n\":1}");

                Assert.That(await Receive(first), Is.EqualTo("{\"n\":1}"));
                Assert.That(await Receive(second), Is.EqualTo("{\"n\":1}"));
            }
        }

        [Test]
        public async Task TestJsonEndpoint()
        {
            await server.StartAsync();

            using (var http = new HttpClient())
            {
                var response = await http.GetAsync($"http://127.0.0.1:{port}/json/v2");

                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(document));

                document = "{\"changed\":true}";
                Assert.That(await http.GetStringAsync($"http://localhost:{port}/json/v2"), Is.EqualTo(document), "always the current document, also by name");

                Assert.That((await http.PostAsync($"http://127.0.0.1:{port}/json/v2", new StringContent("x"))).StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            }
        }

        [Test]
        public async Task TestOverlayFiles()
        {
            await server.StartAsync();

            using (var http = new HttpClient())
            {
                Assert.That(await http.GetStringAsync($"http://127.0.0.1:{port}/demo/"), Is.EqualTo("<html>demo overlay</html>"));
                Assert.That(await http.GetStringAsync($"http://127.0.0.1:{port}/demo/index.html"), Is.EqualTo("<html>demo overlay</html>"));

                var script = await http.GetAsync($"http://127.0.0.1:{port}/demo/js/app.js");
                Assert.That(script.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/javascript"));
                Assert.That(await script.Content.ReadAsStringAsync(), Is.EqualTo("console.log('hi');"));

                Assert.That((await http.GetAsync($"http://127.0.0.1:{port}/demo/missing.js")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await http.GetAsync($"http://127.0.0.1:{port}/nothing-here/")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }
        }

        [Test]
        public async Task TestNoFileOutsideTheOverlayFolder()
        {
            await server.StartAsync();

            // HttpClient would tidy the path before sending it, so talk to the socket directly
            foreach (string path in new[]
                     {
                         "/../banchosucks-overlay-secret.txt",
                         "/demo/../../banchosucks-overlay-secret.txt",
                         "/%2e%2e/banchosucks-overlay-secret.txt",
                         "/..%5cbanchosucks-overlay-secret.txt",
                         "/demo/..%2f..%2fbanchosucks-overlay-secret.txt",
                     })
            {
                string response = await RawRequest(port, $"GET {path} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n");

                Assert.That(response, Does.Not.Contain("secret\r\n").And.Not.EndsWith("secret"), path);
                Assert.That(response, Does.Not.StartWith("HTTP/1.1 200"), path);
            }
        }

        [Test]
        public async Task TestForeignWebPagesAreRefused()
        {
            await server.StartAsync();

            using (var http = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/json/v2");
                request.Headers.Add("Origin", "https://evil.example");
                Assert.That((await http.SendAsync(request)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

                var local = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/json/v2");
                local.Headers.Add("Origin", $"http://127.0.0.1:{port}");
                var allowed = await http.SendAsync(local);
                Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(allowed.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { $"http://127.0.0.1:{port}" }));
            }

            Assert.ThrowsAsync<WebSocketException>(async () =>
            {
                using (await Connect(port, origin: "https://evil.example"))
                {
                }
            });

            using (var fromFile = await Connect(port, origin: "null"))
                Assert.That(await Receive(fromFile), Is.EqualTo(document), "an overlay opened from disk (OBS local file)");

            Assert.That(server.ClientCount, Is.LessThanOrEqualTo(1), "the refused page never became a client");
        }

        [Test]
        public async Task TestOnlyTheTosuWebSocketPath()
        {
            await server.StartAsync();

            Assert.ThrowsAsync<WebSocketException>(async () =>
            {
                using (await Connect(port, "/"))
                {
                }
            });

            Assert.ThrowsAsync<WebSocketException>(async () =>
            {
                using (await Connect(port, "/websocket/v2/precise"))
                {
                }
            });

            using (var client = await Connect(port, "/websocket/v2"))
                Assert.That(await Receive(client), Is.EqualTo(document));
        }

        [Test]
        public async Task TestPortTakenByAnotherProgram()
        {
            // what a running tosu looks like to us
            var other = new TcpListener(IPAddress.Loopback, port);
            other.Start();

            try
            {
                Exception? failure = null;

                try
                {
                    await server.StartAsync();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                Assert.That(failure, Is.Not.Null, "starting on a taken port fails instead of pretending to listen");
                Assert.That(server.IsRunning, Is.False);
            }
            finally
            {
                other.Stop();
            }
        }

        [Test]
        public async Task TestPortTakenByASecondInstance()
        {
            await server.StartAsync();

            using (var second = new BanchosucksOverlayServer(port, () => document, null))
            {
                Exception? failure = null;

                try
                {
                    await second.StartAsync();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                Assert.That(failure, Is.Not.Null);
            }

            // the first one is unharmed
            using (var client = await Connect(port))
                Assert.That(await Receive(client), Is.EqualTo(document));
        }

        [Test]
        public async Task TestPortIsFreeAfterStop()
        {
            await server.StartAsync();
            await server.StopAsync();

            var other = new TcpListener(IPAddress.Loopback, port);
            Assert.DoesNotThrow(() => other.Start());
            other.Stop();
        }

        public static async Task<ClientWebSocket> Connect(int port, string path = BanchosucksOverlayHttp.WEBSOCKET_PATH, string? origin = null)
        {
            var client = new ClientWebSocket();

            if (origin != null)
                client.Options.SetRequestHeader("Origin", origin);

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}{path}"), cts.Token);
            }
            catch
            {
                client.Dispose();
                throw;
            }

            return client;
        }

        /// <summary>
        /// The next text message, or null when none arrives in time.
        /// </summary>
        public static async Task<string?> Receive(ClientWebSocket client, int timeoutMilliseconds = 10000)
        {
            byte[] buffer = new byte[65536];
            var message = new MemoryStream();

            using (var cts = new CancellationTokenSource(timeoutMilliseconds))
            {
                try
                {
                    while (true)
                    {
                        var result = await client.ReceiveAsync(buffer, cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                            return null;

                        message.Write(buffer, 0, result.Count);

                        if (result.EndOfMessage)
                            return Encoding.UTF8.GetString(message.ToArray());
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        public static async Task<string> RawRequest(int port, string request)
        {
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(IPAddress.Loopback, port);

                var stream = tcp.GetStream();
                stream.ReadTimeout = 5000;

                byte[] bytes = Encoding.ASCII.GetBytes(request);
                await stream.WriteAsync(bytes);

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    try
                    {
                        return await reader.ReadToEndAsync();
                    }
                    catch (IOException)
                    {
                        return string.Empty;
                    }
                }
            }
        }
    }
}

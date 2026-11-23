// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: the network side of the overlay data source. Uses the game's own <see cref="WebSocketServer"/>
    /// (loopback only: <c>127.0.0.1</c> and <c>localhost</c>) on tosu's port and answers what a tosu overlay asks for:
    /// <list type="bullet">
    /// <item><c>ws://127.0.0.1:PORT/websocket/v2</c>: the JSON document, once on connect and again whenever it changed;</item>
    /// <item><c>GET /json/v2</c>: the same document once;</item>
    /// <item><c>GET /&lt;folder&gt;/...</c>: the files of an overlay the player has put into the overlay folder, because most
    /// tosu overlays expect to be loaded from the address they then connect to.</item>
    /// </list>
    /// Everything else gets 404, web pages that are not local get 403 (<see cref="BanchosucksOverlayHttp.IsAllowedOrigin"/>).
    /// Messages sent by overlays (tosu's field filters) are ignored. Nothing is ever written to disk.
    /// </summary>
    public sealed class BanchosucksOverlayServer : IDisposable
    {
        /// <summary>
        /// Files larger than this are not served (overlays are small; this keeps the request loop from hanging on a mistake).
        /// </summary>
        private const long max_file_size = 32 * 1024 * 1024;

        public readonly int Port;

        private readonly Func<string> currentDocument;
        private readonly string? overlayFolder;
        private readonly WebSocketServer server;
        private readonly ConcurrentDictionary<int, Client> clients = new ConcurrentDictionary<int, Client>();
        private readonly Logger logger = Logger.GetLogger(@"websocket");

        /// <summary>
        /// Number of overlays currently connected through the websocket.
        /// </summary>
        public int ClientCount => clients.Count;

        public bool IsRunning => server.IsRunning;

        /// <param name="port">The port to listen on (loopback only).</param>
        /// <param name="currentDocument">Returns the current JSON document; called from server threads.</param>
        /// <param name="overlayFolder">The folder overlay files are served from, or null to serve none.</param>
        public BanchosucksOverlayServer(int port, Func<string> currentDocument, string? overlayFolder)
        {
            Port = port;
            this.currentDocument = currentDocument;
            this.overlayFolder = overlayFolder;

            server = new WebSocketServer(port)
            {
                RequestFilter = handleRequest,
                LogMessages = false,
            };
            server.ClientConnected += onClientConnected;
        }

        /// <summary>
        /// Starts listening. The task fails when the port is taken (for example by a running tosu).
        /// </summary>
        public Task StartAsync() => server.StartAsync();

        /// <summary>
        /// Sends the document to every connected overlay that has not received exactly this document last.
        /// An overlay that is still busy receiving the previous one is skipped, so a stuck browser tab neither
        /// holds up the others nor piles up messages.
        /// </summary>
        public void Publish(string document)
        {
            foreach (var (id, client) in clients)
            {
                if (client.LastSent == document)
                    continue;

                if (Interlocked.CompareExchange(ref client.Sending, 1, 0) != 0)
                    continue;

                send(id, client, document);
            }
        }

        private void onClientConnected(int id)
        {
            var client = new Client { Sending = 1 };
            clients[id] = client;

            string document;

            try
            {
                document = currentDocument();
            }
            catch (Exception ex)
            {
                logger.Add("Overlay data source: building the document failed.", LogLevel.Error, ex);
                client.Sending = 0;
                return;
            }

            send(id, client, document);
        }

        private void send(int id, Client client, string document)
        {
            Task.Run(async () =>
            {
                try
                {
                    await server.SendAsync(id, document).ConfigureAwait(false);
                    client.LastSent = document;
                }
                catch (Exception)
                {
                    // the overlay is gone (or the server is shutting down); the websocket server has already dropped it
                    clients.TryRemove(id, out _);
                }
                finally
                {
                    Interlocked.Exchange(ref client.Sending, 0);
                }
            });
        }

        /// <summary>
        /// The <see cref="WebSocketServer.RequestFilter"/>: answers everything that is not a websocket connection to
        /// <see cref="BanchosucksOverlayHttp.WEBSOCKET_PATH"/> from an allowed origin.
        /// </summary>
        private bool handleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string path = request.Url?.AbsolutePath ?? "/";
            string? origin = request.Headers["Origin"];

            if (!BanchosucksOverlayHttp.IsAllowedOrigin(origin))
            {
                logger.Add($"Overlay data source: refused a request from the web page {origin}.");
                return respond(response, HttpStatusCode.Forbidden);
            }

            if (request.IsWebSocketRequest)
            {
                // hand over to the websocket server
                if (BanchosucksOverlayHttp.IsWebSocketPath(path))
                    return false;

                return respond(response, HttpStatusCode.NotFound);
            }

            bool head = request.HttpMethod == "HEAD";

            if (request.HttpMethod != "GET" && !head)
                return respond(response, HttpStatusCode.MethodNotAllowed);

            if (!string.IsNullOrEmpty(origin))
                response.Headers["Access-Control-Allow-Origin"] = origin;

            if (BanchosucksOverlayHttp.IsJsonPath(path))
                return respond(response, HttpStatusCode.OK, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(currentDocument()), head);

            if (overlayFolder != null)
            {
                // RawUrl keeps the percent-encoding, which the resolver wants to undo itself
                string rawPath = request.RawUrl ?? "/";
                int query = rawPath.IndexOf('?');
                if (query >= 0)
                    rawPath = rawPath.Substring(0, query);

                string? file = BanchosucksOverlayHttp.ResolveStaticFile(overlayFolder, rawPath);

                if (file != null && File.Exists(file) && new FileInfo(file).Length <= max_file_size)
                    return respond(response, HttpStatusCode.OK, BanchosucksOverlayHttp.ContentTypeFor(file), File.ReadAllBytes(file), head);
            }

            return respond(response, HttpStatusCode.NotFound);
        }

        private static bool respond(HttpListenerResponse response, HttpStatusCode status, string? contentType = null, byte[]? body = null, bool headOnly = false)
        {
            try
            {
                response.StatusCode = (int)status;
                response.Headers["Cache-Control"] = "no-store";

                if (contentType != null)
                    response.ContentType = contentType;

                if (body != null)
                {
                    response.ContentLength64 = body.Length;

                    if (!headOnly)
                        response.OutputStream.Write(body, 0, body.Length);
                }

                response.Close();
            }
            catch (Exception)
            {
                // the other side went away mid-answer
                response.Abort();
            }

            return true;
        }

        /// <summary>
        /// Stops listening and closes all connections.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            server.ClientConnected -= onClientConnected;
            clients.Clear();
            return server.IsRunning ? server.StopAsync(cancellationToken) : Task.CompletedTask;
        }

        public void Dispose() => server.Dispose();

        private sealed class Client
        {
            /// <summary>
            /// 1 while a message is on its way to this overlay.
            /// </summary>
            public int Sending;

            public volatile string? LastSent;
        }
    }
}

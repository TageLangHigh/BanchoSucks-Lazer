// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.IPC;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.NonVisual;

namespace osu.Game.Tests.Visual.UserInterface
{
    /// <summary>
    /// The overlay data source as a component: setting on and off, a real websocket client, the port being taken.
    /// Every test uses a free port of its own, never tosu's 24050.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBanchosucksOverlayDataSource : OsuTestScene
    {
        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notifications = new NotificationOverlay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
        };

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private BanchosucksOverlayDataSource source = null!;
        private IScreen? currentScreen;
        private int port;
        private ClientWebSocket? client;
        private Task<string?>? received;

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(notifications);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setting off", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, false));
            AddStep("select a beatmap", () =>
            {
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                SelectedMods.Value = new[] { new OsuModHidden() };
                Beatmap.Value = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
            });
            AddStep("create data source", () =>
            {
                currentScreen = null;
                port = BanchosucksOverlayServerTest.FreePort();

                foreach (var n in notifications.AllNotifications.ToArray())
                    n.Close(false);

                Add(source = new BanchosucksOverlayDataSource(() => currentScreen) { Port = port });
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("close client", () =>
            {
                client?.Dispose();
                client = null;
            });
            AddStep("setting off", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, false));
            AddStep("remove data source", () => Remove(source, true));
        }

        [Test]
        public void TestOffByDefault()
        {
            AddAssert("default is off", () => config.GetBindable<bool>(OsuSetting.BanchosucksOverlayDataSource).Default, () => Is.False);
            AddWaitStep("wait", 5);
            AddAssert("not listening", () => source.IsRunning, () => Is.False);
            AddAssert("port is closed", () => canConnect(port), () => Is.False);
        }

        [Test]
        public void TestOverlayReceivesState()
        {
            AddStep("setting on", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, true));
            AddUntilStep("listening", () => source.IsRunning);

            connect();
            receive();

            AddAssert("menu state", () => json("state.name").GetString(), () => Is.EqualTo("menu"));
            AddAssert("beatmap title", () => json("beatmap.title").GetString(), () => Is.EqualTo(Beatmap.Value.Metadata.Title));
            AddAssert("beatmap artist", () => json("beatmap.artist").GetString(), () => Is.EqualTo(Beatmap.Value.Metadata.Artist));
            AddAssert("beatmap version", () => json("beatmap.version").GetString(), () => Is.EqualTo(Beatmap.Value.BeatmapInfo.DifficultyName));
            AddAssert("ruleset", () => json("settings.mode.name").GetString(), () => Is.EqualTo("osu"));
            AddAssert("selected mods", () => json("play.mods.name").GetString(), () => Is.EqualTo("HD"));
            AddAssert("legacy mod number", () => json("play.mods.number").GetInt32(), () => Is.EqualTo(8));
            AddAssert("player name", () => json("profile.name").GetString(), () => Is.EqualTo(API.LocalUser.Value.Username));
            AddAssert("no play", () => json("play.score").GetInt64(), () => Is.EqualTo(0));

            AddUntilStep("details arrive (objects counted)", () => nextDocument(() => json("beatmap.stats.objects.total").GetInt32() > 0 && json("beatmap.time.lastObject").GetInt64() > 0));
            AddAssert("first object before last", () => json("beatmap.time.firstObject").GetInt64(), () => Is.LessThan(json("beatmap.time.lastObject").GetInt64()));

            AddStep("change mods", () => SelectedMods.Value = new osu.Game.Rulesets.Mods.Mod[] { new OsuModHidden(), new OsuModDoubleTime() });
            AddUntilStep("new mods arrive", () => nextDocument(() => json("play.mods.name").GetString() == "HDDT"));
            AddAssert("rate", () => json("play.mods.rate").GetDouble(), () => Is.EqualTo(1.5));

            AddStep("player loader on screen", () => currentScreen = new Screens.Play.PlayerLoader(() => new TestPlayer()));
            AddUntilStep("play state arrives", () => nextDocument(() => json("state.name").GetString() == "play"));
            AddAssert("state number", () => json("state.number").GetInt32(), () => Is.EqualTo(2));
        }

        [Test]
        public void TestJsonEndpointAndSwitchingOff()
        {
            AddStep("setting on", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, true));
            AddUntilStep("listening", () => source.IsRunning);

            Task<string>? get = null;

            AddStep("GET /json/v2", () => get = new HttpClient().GetStringAsync($"http://127.0.0.1:{port}/json/v2"));
            AddUntilStep("answered", () => get!.IsCompleted);
            AddAssert("a tosu document", () => JsonDocument.Parse(get!.GetResultSafely()).RootElement.GetProperty("beatmap").GetProperty("title").GetString(),
                () => Is.EqualTo(Beatmap.Value.Metadata.Title));

            AddStep("setting off", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, false));
            AddAssert("not running", () => source.IsRunning, () => Is.False);
            AddUntilStep("port is closed", () => !canConnect(port));

            AddStep("setting on again", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, true));
            AddUntilStep("listening again", () => source.IsRunning);
            AddAssert("port is open", () => canConnect(port));
        }

        [Test]
        public void TestPortTaken()
        {
            TcpListener other = null!;

            AddStep("another program takes the port", () =>
            {
                other = new TcpListener(IPAddress.Loopback, port);
                other.Start();
            });

            AddStep("setting on", () => config.SetValue(OsuSetting.BanchosucksOverlayDataSource, true));

            AddUntilStep("one notification", () => notifications.AllNotifications.OfType<SimpleNotification>().Count(n => n.Text.ToString().Contains(port.ToString())), () => Is.EqualTo(1));
            AddAssert("not running", () => source.IsRunning, () => Is.False);
            AddWaitStep("wait", 5);
            AddAssert("still one notification", () => notifications.AllNotifications.Count(), () => Is.EqualTo(1));

            AddStep("the other program quits", () => other.Stop());
            AddStep("setting off and on", () =>
            {
                config.SetValue(OsuSetting.BanchosucksOverlayDataSource, false);
                config.SetValue(OsuSetting.BanchosucksOverlayDataSource, true);
            });
            AddUntilStep("listening now", () => source.IsRunning);
        }

        private void connect()
        {
            Task<ClientWebSocket>? connecting = null;

            AddStep("connect overlay", () => connecting = BanchosucksOverlayServerTest.Connect(port));
            AddUntilStep("connected", () => connecting!.IsCompleted);
            AddStep("keep client", () => client = connecting!.GetResultSafely());
        }

        private void receive()
        {
            AddStep("receive", () => received = BanchosucksOverlayServerTest.Receive(client!));
            AddUntilStep("document received", () => received!.IsCompleted && received.GetResultSafely() != null);
        }

        /// <summary>
        /// Whether the document received last passes the check; if it does not, the next one is awaited.
        /// </summary>
        private bool nextDocument(Func<bool> check)
        {
            if (received != null && received.IsCompletedSuccessfully && received.GetResultSafely() != null && check())
                return true;

            if (received == null || received.IsCompleted)
                received = BanchosucksOverlayServerTest.Receive(client!);

            return false;
        }

        private JsonElement lastDocument;
        private string? lastDocumentText;

        private JsonElement json(string path)
        {
            string text = received!.GetResultSafely()!;

            if (text != lastDocumentText)
            {
                lastDocument = JsonDocument.Parse(text).RootElement;
                lastDocumentText = text;
            }

            var element = lastDocument;

            foreach (string key in path.Split('.'))
                element = element.GetProperty(key);

            return element;
        }

        private static bool canConnect(int port)
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    tcp.Connect(IPAddress.Loopback, port);
                    return true;
                }
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}

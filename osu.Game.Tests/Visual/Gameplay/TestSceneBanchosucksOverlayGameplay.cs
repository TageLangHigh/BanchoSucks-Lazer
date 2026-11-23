// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.IPC;
using osu.Game.Tests.NonVisual;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// The overlay data source during a real (autoplayed) play: score, combo, hits and live pp reach a websocket client.
    /// </summary>
    public partial class TestSceneBanchosucksOverlayGameplay : OsuPlayerTestScene
    {
        protected override bool Autoplay => true;

        private BanchosucksOverlayDataSource source = null!;
        private int port;
        private ClientWebSocket? client;
        private Task<string?>? received;
        private JsonElement document;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("create data source", () =>
            {
                port = BanchosucksOverlayServerTest.FreePort();
                LocalConfig.SetValue(OsuSetting.BanchosucksOverlayDataSource, true);
                Add(source = new BanchosucksOverlayDataSource(() => Player) { Port = port });
            });
            AddUntilStep("listening", () => source.IsRunning);
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("close client", () =>
            {
                client?.Dispose();
                client = null;
            });
            AddStep("remove data source", () =>
            {
                LocalConfig.SetValue(OsuSetting.BanchosucksOverlayDataSource, false);
                Remove(source, true);
            });
        }

        [Test]
        public void TestLiveValues()
        {
            AddUntilStep("play is tracked", () => source.LatestState.Play.Active);
            AddAssert("state is play", () => source.LatestState.Game, () => Is.EqualTo(BanchosucksOverlayGameState.Play));
            AddAssert("one play this session", () => source.LatestState.SessionPlayCount, () => Is.EqualTo(1));

            AddUntilStep("score counts up", () => source.LatestState.Play.Score, () => Is.GreaterThan(0));
            AddUntilStep("combo counts up", () => source.LatestState.Play.Combo, () => Is.GreaterThan(1));
            AddUntilStep("300s counted", () => source.LatestState.Play.Hits.Great, () => Is.GreaterThan(1));
            AddAssert("accuracy 100", () => source.LatestState.Play.Accuracy, () => Is.EqualTo(100).Within(0.01));
            AddAssert("rank", () => source.LatestState.Play.Rank, () => Is.EqualTo("X").Or.EqualTo("XH"));
            AddAssert("health", () => source.LatestState.Play.Health, () => Is.GreaterThan(0).And.LessThanOrEqualTo(1));
            AddAssert("autoplay is the player", () => source.LatestState.Play.PlayerName, () => Is.EqualTo(Player.GameplayState.Score.ScoreInfo.User.Username));
            AddAssert("mods include autoplay", () => source.LatestState.Play.Mods.Acronyms, () => Does.Contain("AT"));

            AddUntilStep("live pp above zero", () => source.LatestState.Play.PP.Total, () => Is.GreaterThan(0));
            AddAssert("pp if FC is at least the live pp", () => source.LatestState.Play.PPIfFullCombo.Total, () => Is.GreaterThanOrEqualTo(source.LatestState.Play.PP.Total - 0.01));
            AddAssert("best pp of this play", () => source.LatestState.Play.PPMaxThisPlay, () => Is.GreaterThanOrEqualTo(source.LatestState.Play.PP.Total - 0.01));
            AddAssert("live stars", () => source.LatestState.Beatmap.StarsLive, () => Is.GreaterThan(0));

            AddUntilStep("beatmap details in", () => source.LatestState.Beatmap.TimeLastObject, () => Is.GreaterThan(0));
            AddAssert("objects counted", () => source.LatestState.Beatmap.Objects, () => Is.EqualTo(Beatmap.Value.Beatmap.HitObjects.Count));

            Task<ClientWebSocket>? connecting = null;

            AddStep("connect overlay", () => connecting = BanchosucksOverlayServerTest.Connect(port));
            AddUntilStep("connected", () => connecting!.IsCompleted);
            AddStep("receive", () =>
            {
                client = connecting!.GetResultSafely();
                received = BanchosucksOverlayServerTest.Receive(client);
            });
            AddUntilStep("document received", () => received!.IsCompleted && received.GetResultSafely() != null);
            AddStep("parse", () => document = JsonDocument.Parse(received!.GetResultSafely()!).RootElement);

            AddAssert("json state", () => document.GetProperty("state").GetProperty("name").GetString(), () => Is.EqualTo("play"));
            AddAssert("json score", () => document.GetProperty("play").GetProperty("score").GetInt64(), () => Is.GreaterThan(0));
            AddAssert("json combo", () => document.GetProperty("play").GetProperty("combo").GetProperty("max").GetInt32(), () => Is.GreaterThan(1));
            AddAssert("json pp", () => document.GetProperty("play").GetProperty("pp").GetProperty("current").GetDouble(), () => Is.GreaterThan(0));
            AddAssert("json 300s", () => document.GetProperty("play").GetProperty("hits").GetProperty("300").GetInt32(), () => Is.GreaterThan(1));
            AddAssert("json title", () => document.GetProperty("beatmap").GetProperty("title").GetString(), () => Is.EqualTo(Beatmap.Value.Metadata.Title));

            Task<string?>? next = null;

            AddStep("receive the next one", () => next = BanchosucksOverlayServerTest.Receive(client!));
            AddUntilStep("a newer document follows", () => next!.IsCompleted && next.GetResultSafely() != null && next.GetResultSafely() != received!.GetResultSafely());
        }
    }
}

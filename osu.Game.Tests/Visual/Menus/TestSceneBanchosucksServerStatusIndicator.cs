// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays.Toolbar;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// The toolbar's server status with a fake status plugin.
    /// </summary>
    public partial class TestSceneBanchosucksServerStatusIndicator : OsuTestScene
    {
        private BanchosucksServerStatusIndicator indicator = null!;

        private int requests;
        private bool fail;
        private BanchosucksStatusSummary answer = null!;

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("online", () => dummyAPI.SetState(APIState.Online));

            AddStep("fake plugin", () =>
            {
                requests = 0;
                fail = false;
                answer = new BanchosucksStatusSummary { LazerOnline = 7, OpenRooms = 2, DailyChallenge = "Artist - Title [Insane]" };

                dummyAPI.HandleRequest = request =>
                {
                    if (request is not GetBanchosucksStatusSummaryRequest summary)
                        return false;

                    requests++;

                    if (fail)
                        return false;

                    summary.RoundTrip = TimeSpan.FromMilliseconds(23.4);
                    summary.TriggerSuccess(answer);
                    return true;
                };
            });
        }

        private void create(APIState state = APIState.Online)
        {
            AddStep($"create while {state}", () =>
            {
                dummyAPI.SetState(state);

                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.X,
                    Height = Toolbar.HEIGHT,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                        indicator = new BanchosucksServerStatusIndicator(),
                    },
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("reset api", () =>
            {
                dummyAPI.HandleRequest = null;
                dummyAPI.SetState(APIState.Online);
            });
        }

        [Test]
        public void TestOnlineShowsResponseTimeAndNumbers()
        {
            create();

            AddUntilStep("response time shown", () => indicator.LabelText, () => Is.EqualTo("23 ms"));
            AddAssert("connected", () => indicator.Connection, () => Is.EqualTo(BanchosucksConnectionState.Online));
            AddAssert("tooltip has the numbers", () => indicator.TooltipText.ToString(),
                () => Does.Contain("Players online (lazer): 7").And.Contain("Open multiplayer rooms: 2").And.Contain("Daily challenge: Artist - Title [Insane]").And.Contain("Response time: 23 ms"));
            AddAssert("no restart mentioned", () => indicator.TooltipText.ToString(), () => Does.Not.Contain("restart"));

            // one request; neither waiting nor clicking brings a second one within the minute
            AddStep("click", () => indicator.TriggerClick());
            AddWaitStep("wait", 10);
            AddAssert("one request", () => requests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNoRequestsWhileLoggedOut()
        {
            create(APIState.Offline);

            AddUntilStep("offline shown", () => indicator.LabelText, () => Is.EqualTo("offline"));
            AddStep("click", () => indicator.TriggerClick());
            AddWaitStep("wait", 10);
            AddAssert("nothing requested", () => requests, () => Is.Zero);
            AddAssert("tooltip says so", () => indicator.TooltipText.ToString(), () => Is.EqualTo("Banchosucks Lazer: not logged in"));

            AddStep("log in", () => dummyAPI.SetState(APIState.Online));
            AddUntilStep("response time shown", () => indicator.LabelText, () => Is.EqualTo("23 ms"));
            AddAssert("one request", () => requests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestConnectingAndFailing()
        {
            create(APIState.Connecting);
            AddUntilStep("connecting shown", () => indicator.LabelText, () => Is.EqualTo("connecting"));
            AddAssert("nothing requested", () => requests, () => Is.Zero);

            AddStep("api failing", () => dummyAPI.SetState(APIState.Failing));
            AddUntilStep("trouble shown", () => indicator.LabelText, () => Is.EqualTo("unstable"));
            AddAssert("nothing requested", () => requests, () => Is.Zero);
        }

        [Test]
        public void TestFailedStatusRequest()
        {
            AddStep("plugin fails", () => fail = true);
            create();

            AddUntilStep("trouble shown", () => indicator.Connection, () => Is.EqualTo(BanchosucksConnectionState.Trouble));
            AddAssert("label", () => indicator.LabelText, () => Is.EqualTo("unstable"));
            AddAssert("tooltip explains", () => indicator.TooltipText.ToString(), () => Does.Contain("the last status request failed"));
            AddWaitStep("wait", 10);
            AddAssert("not retried within the minute", () => requests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestMaintenanceCountdown()
        {
            var now = new DateTimeOffset(2026, 11, 23, 19, 55, 55, TimeSpan.Zero);

            // the plugin does not send these fields yet; this is what the client does once it does
            AddStep("maintenance planned", () => answer = new BanchosucksStatusSummary
            {
                LazerOnline = 3,
                MaintenanceAt = new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero),
                MaintenanceMessage = "Update auf 2026.913.18",
            });

            create();
            AddStep("fixed clock", () => indicator.Now = () => now);

            AddUntilStep("countdown shown", () => indicator.LabelText, () => Is.EqualTo("restart 4:05"));
            AddAssert("tooltip has the notice", () => indicator.TooltipText.ToString(),
                () => Does.Contain("Planned restart in 4:05").And.Contain("Update auf 2026.913.18"));

            AddStep("time passes", () => now = now.AddMinutes(4));
            AddUntilStep("countdown follows without a request", () => indicator.LabelText, () => Is.EqualTo("restart 0:05"));
            AddAssert("one request", () => requests, () => Is.EqualTo(1));

            AddStep("start reached", () => now = now.AddMinutes(1));
            AddUntilStep("now", () => indicator.LabelText, () => Is.EqualTo("restart now"));

            AddStep("long over", () => now = now.AddHours(1));
            AddUntilStep("back to the response time", () => indicator.LabelText, () => Is.EqualTo("23 ms"));
        }
    }
}

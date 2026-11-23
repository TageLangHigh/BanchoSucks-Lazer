// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Performance;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneBanchosucksStutterReport : OsuTestScene
    {
        [Cached(typeof(ILocalUserPlayInfo))]
        private readonly TestPlayInfo playInfo = new TestPlayInfo();

        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notifications = new NotificationOverlay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
        };

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private BanchosucksStutterMonitor monitor = null!;
        private IScreen? currentScreen;

        private IScreen player = null!;
        private IScreen results = null!;
        private IScreen songSelect = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // only their types matter, none of them is ever pushed
            player = new TestPlayer();
            results = new SoloResultsScreen(TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo));
            songSelect = new Screen();

            Add(notifications);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("report on", () => config.SetValue(OsuSetting.BanchosucksStutterReport, true));
            AddStep("do not disturb off", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, false));
            AddStep("reset", () =>
            {
                playInfo.State.Value = LocalUserPlayingState.NotPlaying;
                currentScreen = songSelect;

                foreach (var n in notifications.AllNotifications.ToArray())
                    n.Close(false);

                if (monitor != null)
                    Remove(monitor, true);

                Add(monitor = new BanchosucksStutterMonitor(() => currentScreen));
            });
            AddUntilStep("no notifications", () => !reports().Any());
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("do not disturb back on", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, true));
        }

        [Test]
        public void TestFramesAreRecordedOnlyWhilePlaying()
        {
            AddStep("enter player", () => currentScreen = player);
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);

            AddUntilStep("update frames recorded", () => monitor.UpdateFrames.Count, () => Is.GreaterThan(5));
            AddUntilStep("draw frames recorded", () => monitor.DrawFrames.Count, () => Is.GreaterThan(5));

            int updateCount = 0;
            int drawCount = 0;

            AddStep("break", () => playInfo.State.Value = LocalUserPlayingState.Break);
            AddWaitStep("let the break settle", 3);
            AddStep("remember counts", () =>
            {
                updateCount = monitor.UpdateFrames.Count;
                drawCount = monitor.DrawFrames.Count;
            });
            AddWaitStep("wait in the break", 10);
            AddAssert("no update frames in the break", () => monitor.UpdateFrames.Count, () => Is.EqualTo(updateCount));
            AddAssert("no draw frames in the break", () => monitor.DrawFrames.Count, () => Is.EqualTo(drawCount));

            AddStep("continue playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddUntilStep("recording again", () => monitor.UpdateFrames.Count, () => Is.GreaterThan(updateCount));
        }

        [Test]
        public void TestReportOnResultsScreen()
        {
            playWith(30, 45, 83.6);

            AddAssert("nothing yet, the results are not up", () => reports().Any(), () => Is.False);

            AddStep("results open", () => currentScreen = results);
            AddUntilStep("report posted", () => reports().Length, () => Is.EqualTo(1));
            AddAssert("three stutters", () => reports().Single().Report.Stutters, () => Is.EqualTo(3));
            AddAssert("worst frame", () => reports().Single().Report.Worst, () => Is.EqualTo(83.6));
            AddAssert("text", () => reports().Single().Text.ToString(), () => Does.Contain("3 stutters").And.Contain("84 ms"));

            AddWaitStep("wait", 5);
            AddAssert("posted only once", () => reports().Length, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNoReportForSmoothPlay()
        {
            playWith();

            AddStep("results open", () => currentScreen = results);
            AddWaitStep("wait", 10);
            AddAssert("no report", () => reports().Any(), () => Is.False);
        }

        [Test]
        public void TestNoReportWhenLeavingWithoutResults()
        {
            playWith(30, 45, 83.6);

            AddStep("back to song select", () => currentScreen = songSelect);
            AddWaitStep("wait", 5);
            AddStep("results of an old score opened later", () => currentScreen = results);
            AddWaitStep("wait", 10);
            AddAssert("no report", () => reports().Any(), () => Is.False);
        }

        [Test]
        public void TestSettingOff()
        {
            AddStep("report off", () => config.SetValue(OsuSetting.BanchosucksStutterReport, false));
            AddStep("enter player", () => currentScreen = player);
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddWaitStep("play", 10);
            AddAssert("nothing recorded", () => monitor.UpdateFrames.Count + monitor.DrawFrames.Count, () => Is.EqualTo(0));
            AddStep("map over", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);
            AddStep("results open", () => currentScreen = results);
            AddWaitStep("wait", 10);
            AddAssert("no report", () => reports().Any(), () => Is.False);
        }

        /// <summary>
        /// Goes through a play and replaces whatever the test run itself recorded by 1000 smooth frames plus the given long ones.
        /// </summary>
        private void playWith(params double[] longFrames)
        {
            AddStep("enter player", () => currentScreen = player);
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddUntilStep("recording", () => monitor.UpdateFrames.Count, () => Is.GreaterThan(0));
            AddStep("map over", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);
            AddStep("set recorded frames", () =>
            {
                monitor.UpdateFrames.Reset();
                monitor.DrawFrames.Reset();

                for (int i = 0; i < 1000; i++)
                {
                    monitor.UpdateFrames.Add(1);
                    monitor.DrawFrames.Add(4);
                }

                foreach (double frame in longFrames)
                    monitor.UpdateFrames.Add(frame);
            });
        }

        private BanchosucksStutterMonitor.BanchosucksStutterNotification[] reports()
            => notifications.AllNotifications.OfType<BanchosucksStutterMonitor.BanchosucksStutterNotification>().ToArray();

        private class TestPlayInfo : ILocalUserPlayInfo
        {
            public readonly Bindable<LocalUserPlayingState> State = new Bindable<LocalUserPlayingState>();

            public IBindable<LocalUserPlayingState> PlayingState => State;
        }
    }
}

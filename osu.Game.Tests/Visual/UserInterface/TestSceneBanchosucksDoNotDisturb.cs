// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneBanchosucksDoNotDisturb : OsuTestScene
    {
        [Cached(typeof(ILocalUserPlayInfo))]
        private readonly TestPlayInfo playInfo = new TestPlayInfo();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private NotificationOverlay overlay = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("do not disturb on", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, true));
            AddStep("not playing", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);
            AddStep("create overlay", () => Child = overlay = new NotificationOverlay
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
            });
        }

        [Test]
        public void TestHeldDuringMapAndSummarisedAfterwards()
        {
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);

            AddStep("post three", () =>
            {
                for (int i = 0; i < 3; i++)
                    overlay.Post(new SimpleNotification { Text = $"download {i} finished" });
            });

            AddAssert("three held", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(3));
            AddAssert("nothing shown", () => overlay.AllNotifications.Count(), () => Is.EqualTo(0));

            AddStep("break", () => playInfo.State.Value = LocalUserPlayingState.Break);
            AddAssert("still held in the break", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(3));

            AddStep("map over", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);

            AddUntilStep("three in the overlay and one summary", () => overlay.AllNotifications.Count(), () => Is.EqualTo(4));
            AddAssert("nothing held", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(0));
            AddAssert("only the summary is a toast", () => overlay.AllNotifications.Where(n => n.IsInToastTray).Select(n => n.GetType()),
                () => Is.EqualTo(new[] { typeof(NotificationOverlay.BanchosucksHeldSummaryNotification) }));
            AddAssert("summary counts three", () => overlay.AllNotifications.OfType<NotificationOverlay.BanchosucksHeldSummaryNotification>().Single().HeldCount, () => Is.EqualTo(3));

            AddStep("click summary", () => overlay.AllNotifications.OfType<NotificationOverlay.BanchosucksHeldSummaryNotification>().Single().TriggerClick());
            AddUntilStep("overlay opened", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void TestErrorsAndCriticalAreNotHeld()
        {
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);

            AddStep("post error and outage", () =>
            {
                overlay.Post(new SimpleErrorNotification { Text = "import failed" });
                overlay.Post(new OutageNotification("server on fire"));
            });

            AddAssert("nothing held", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(0));
            AddUntilStep("both shown", () => overlay.AllNotifications.Count(), () => Is.EqualTo(2));
        }

        [Test]
        public void TestSingleHeldNotificationIsShownAsItself()
        {
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddStep("post one", () => overlay.Post(new SimpleNotification { Text = "download finished" }));
            AddStep("map over", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);

            AddUntilStep("one notification", () => overlay.AllNotifications.Count(), () => Is.EqualTo(1));
            AddAssert("no summary", () => overlay.AllNotifications.OfType<NotificationOverlay.BanchosucksHeldSummaryNotification>().Any(), () => Is.False);
            AddAssert("shown as toast", () => overlay.AllNotifications.Single().IsInToastTray);
        }

        [Test]
        public void TestSettingOff()
        {
            AddStep("do not disturb off", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, false));
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddStep("post one", () => overlay.Post(new SimpleNotification { Text = "download finished" }));

            AddAssert("not held", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(0));
            AddUntilStep("shown", () => overlay.AllNotifications.Count(), () => Is.EqualTo(1));
        }

        [Test]
        public void TestSwitchingOffMidMapReleases()
        {
            AddStep("start playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddStep("post two", () =>
            {
                overlay.Post(new SimpleNotification { Text = "a" });
                overlay.Post(new SimpleNotification { Text = "b" });
            });
            AddAssert("two held", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(2));

            AddStep("do not disturb off", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, false));
            AddUntilStep("two and a summary", () => overlay.AllNotifications.Count(), () => Is.EqualTo(3));
        }

        private class TestPlayInfo : ILocalUserPlayInfo
        {
            public readonly Bindable<LocalUserPlayingState> State = new Bindable<LocalUserPlayingState>();

            public IBindable<LocalUserPlayingState> PlayingState => State;
        }
    }
}

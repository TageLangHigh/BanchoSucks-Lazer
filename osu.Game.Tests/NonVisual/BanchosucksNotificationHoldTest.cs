// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksNotificationHoldTest
    {
        [Test]
        public void TestNothingHeldWithoutReason()
        {
            var hold = new BanchosucksNotificationHold<string>();

            Assert.That(hold.TryHold("download", BanchosucksNotificationKind.Other), Is.False);
            Assert.That(hold.TryHold("pm", BanchosucksNotificationKind.PrivateMessage), Is.False);
            Assert.That(hold.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestGameplayHoldsEverythingButCriticalErrorsAndProgress()
        {
            var hold = new BanchosucksNotificationHold<string>();
            hold.SetActive(BanchosucksHoldReason.Gameplay, true);

            Assert.That(hold.TryHold("outage", BanchosucksNotificationKind.Critical), Is.False);
            Assert.That(hold.TryHold("error", BanchosucksNotificationKind.Error), Is.False);
            Assert.That(hold.TryHold("import", BanchosucksNotificationKind.Progress), Is.False);

            Assert.That(hold.TryHold("pm", BanchosucksNotificationKind.PrivateMessage), Is.True);
            Assert.That(hold.TryHold("mention", BanchosucksNotificationKind.ChatMention), Is.True);
            Assert.That(hold.TryHold("friend", BanchosucksNotificationKind.Friend), Is.True);
            Assert.That(hold.TryHold("invite", BanchosucksNotificationKind.OtherPlayer), Is.True);
            Assert.That(hold.TryHold("download", BanchosucksNotificationKind.Other), Is.True);

            var release = hold.SetActive(BanchosucksHoldReason.Gameplay, false);

            Assert.That(release.EndedReason, Is.EqualTo(BanchosucksHoldReason.Gameplay));
            Assert.That(release.Items, Is.EqualTo(new[] { "pm", "mention", "friend", "invite", "download" }), "released in the order they arrived");
            Assert.That(hold.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestStreamerModeHoldsOnlyChatAndFriends()
        {
            var hold = new BanchosucksNotificationHold<string>();
            hold.SetActive(BanchosucksHoldReason.StreamerMode, true);

            Assert.That(hold.TryHold("pm", BanchosucksNotificationKind.PrivateMessage), Is.True);
            Assert.That(hold.TryHold("mention", BanchosucksNotificationKind.ChatMention), Is.True);
            Assert.That(hold.TryHold("friend", BanchosucksNotificationKind.Friend), Is.True);

            Assert.That(hold.TryHold("invite", BanchosucksNotificationKind.OtherPlayer), Is.False, "shown, with the name removed");
            Assert.That(hold.TryHold("download", BanchosucksNotificationKind.Other), Is.False);
            Assert.That(hold.TryHold("error", BanchosucksNotificationKind.Error), Is.False);
            Assert.That(hold.TryHold("outage", BanchosucksNotificationKind.Critical), Is.False);

            var release = hold.SetActive(BanchosucksHoldReason.StreamerMode, false);

            Assert.That(release.EndedReason, Is.EqualTo(BanchosucksHoldReason.StreamerMode));
            Assert.That(release.Items, Has.Count.EqualTo(3));
        }

        [Test]
        public void TestItemStaysHeldWhileAnotherReasonApplies()
        {
            var hold = new BanchosucksNotificationHold<string>();
            hold.SetActive(BanchosucksHoldReason.StreamerMode, true);
            hold.SetActive(BanchosucksHoldReason.Gameplay, true);

            hold.TryHold("pm", BanchosucksNotificationKind.PrivateMessage);
            hold.TryHold("download", BanchosucksNotificationKind.Other);

            // the map ends: the download is shown, the private message still waits for the end of streamer mode
            var afterMap = hold.SetActive(BanchosucksHoldReason.Gameplay, false);
            Assert.That(afterMap.Items, Is.EqualTo(new[] { "download" }));
            Assert.That(hold.Count, Is.EqualTo(1));

            var afterStream = hold.SetActive(BanchosucksHoldReason.StreamerMode, false);
            Assert.That(afterStream.Items, Is.EqualTo(new[] { "pm" }));
        }

        [Test]
        public void TestReasonThatBeganLaterKeepsItem()
        {
            var hold = new BanchosucksNotificationHold<string>();
            hold.SetActive(BanchosucksHoldReason.StreamerMode, true);
            hold.TryHold("pm", BanchosucksNotificationKind.PrivateMessage);

            // a map starts, then streamer mode is switched off mid-map: nothing may pop up now
            hold.SetActive(BanchosucksHoldReason.Gameplay, true);
            var midMap = hold.SetActive(BanchosucksHoldReason.StreamerMode, false);

            Assert.That(midMap.Items, Is.Empty);
            Assert.That(hold.SetActive(BanchosucksHoldReason.Gameplay, false).Items, Is.EqualTo(new[] { "pm" }));
        }

        [Test]
        public void TestRepeatedSwitchingReleasesOnce()
        {
            var hold = new BanchosucksNotificationHold<string>();

            Assert.That(hold.SetActive(BanchosucksHoldReason.Gameplay, false).EndedReason, Is.EqualTo(BanchosucksHoldReason.None), "was not active");

            hold.SetActive(BanchosucksHoldReason.Gameplay, true);
            Assert.That(hold.SetActive(BanchosucksHoldReason.Gameplay, true).Items, Is.Empty, "switching on never releases");

            hold.TryHold("a", BanchosucksNotificationKind.Other);

            Assert.That(hold.SetActive(BanchosucksHoldReason.Gameplay, false).Items, Has.Count.EqualTo(1));
            Assert.That(hold.SetActive(BanchosucksHoldReason.Gameplay, false).Items, Is.Empty);
        }

        [Test]
        public void TestCapacityDropsOldest()
        {
            var hold = new BanchosucksNotificationHold<int>();
            hold.SetActive(BanchosucksHoldReason.Gameplay, true);

            for (int i = 0; i < BanchosucksNotificationHold<int>.CAPACITY + 5; i++)
                hold.TryHold(i, BanchosucksNotificationKind.Other);

            Assert.That(hold.Count, Is.EqualTo(BanchosucksNotificationHold<int>.CAPACITY));
            Assert.That(hold.Dropped, Is.EqualTo(5));

            var release = hold.SetActive(BanchosucksHoldReason.Gameplay, false);
            Assert.That(release.Items[0], Is.EqualTo(5), "the five oldest are gone");
            Assert.That(hold.Dropped, Is.EqualTo(0));
        }
    }
}

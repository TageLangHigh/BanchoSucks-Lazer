// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneBanchosucksStreamerMode : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private TestContainer content = null!;

        private NotificationOverlay overlay => content.NotificationOverlay;
        private ChatOverlay chatOverlay => content.ChatOverlay;
        private BanchosucksStreamerTextBox textBox => content.TextBox;

        private readonly APIUser other = new APIUser { Id = 4242, Username = "SomeOtherPlayer" };

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("streamer mode off", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, false));
            AddStep("do not disturb off", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, false));
            AddStep("create content", () => Child = content = new TestContainer(API) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("wait for load", () => content.IsLoaded && overlay.IsLoaded && chatOverlay.IsLoaded);
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("streamer mode off", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, false));
            AddStep("do not disturb back on", () => config.SetValue(OsuSetting.BanchosucksDoNotDisturb, true));
        }

        [Test]
        public void TestClassification()
        {
            AddAssert("private message", () => BanchosucksNotificationGate.Classify(privateMessage()), () => Is.EqualTo(BanchosucksNotificationKind.PrivateMessage));
            AddAssert("mention", () => BanchosucksNotificationGate.Classify(mention()), () => Is.EqualTo(BanchosucksNotificationKind.ChatMention));
            AddAssert("friend online", () => BanchosucksNotificationGate.Classify(friend("SingleFriendOnlineNotification")), () => Is.EqualTo(BanchosucksNotificationKind.Friend));
            AddAssert("friend offline", () => BanchosucksNotificationGate.Classify(friend("SingleFriendOfflineNotification")), () => Is.EqualTo(BanchosucksNotificationKind.Friend));
            AddAssert("friends online", () => BanchosucksNotificationGate.Classify(friends("MultipleFriendsOnlineNotification")), () => Is.EqualTo(BanchosucksNotificationKind.Friend));
            AddAssert("friends offline", () => BanchosucksNotificationGate.Classify(friends("MultipleFriendsOfflineNotification")), () => Is.EqualTo(BanchosucksNotificationKind.Friend));
            AddAssert("invitation", () => BanchosucksNotificationGate.Classify(invitation()), () => Is.EqualTo(BanchosucksNotificationKind.OtherPlayer));
            AddAssert("plain", () => BanchosucksNotificationGate.Classify(new SimpleNotification()), () => Is.EqualTo(BanchosucksNotificationKind.Other));
            AddAssert("error", () => BanchosucksNotificationGate.Classify(new SimpleErrorNotification()), () => Is.EqualTo(BanchosucksNotificationKind.Error));
            AddAssert("progress", () => BanchosucksNotificationGate.Classify(new ProgressNotification()), () => Is.EqualTo(BanchosucksNotificationKind.Progress));
            AddAssert("outage", () => BanchosucksNotificationGate.Classify(new OutageNotification("down")), () => Is.EqualTo(BanchosucksNotificationKind.Critical));
        }

        [Test]
        public void TestChatAndFriendNotificationsHeldUntilModeEnds()
        {
            AddStep("streamer mode on", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, true));

            AddStep("post private message, mention and friend", () =>
            {
                overlay.Post(privateMessage());
                overlay.Post(mention());
                overlay.Post(friend("SingleFriendOnlineNotification"));
            });

            AddAssert("three held", () => overlay.BanchosucksHeldCount, () => Is.EqualTo(3));
            AddAssert("nothing shown", () => overlay.AllNotifications.Count(), () => Is.EqualTo(0));

            AddStep("post plain notification", () => overlay.Post(new SimpleNotification { Text = "download finished" }));
            AddUntilStep("shown right away", () => overlay.AllNotifications.Count(), () => Is.EqualTo(1));

            AddStep("streamer mode off", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, false));

            AddUntilStep("three delivered plus summary", () => overlay.AllNotifications.Count(), () => Is.EqualTo(5));
            AddAssert("summary names streamer mode", () => overlay.AllNotifications.OfType<NotificationOverlay.BanchosucksHeldSummaryNotification>().Single().Reason,
                () => Is.EqualTo(BanchosucksHoldReason.StreamerMode));
            AddAssert("private message is in the overlay now", () => overlay.AllNotifications.OfType<MessageNotifier.PrivateMessageNotification>().Single().IsInToastTray, () => Is.False);
        }

        [Test]
        public void TestInvitationShownWithoutName()
        {
            MultiplayerInvitationNotification notification = null!;

            AddStep("streamer mode on", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, true));
            AddStep("post invitation", () => overlay.Post(notification = invitation()));

            AddUntilStep("shown", () => overlay.AllNotifications.Any(n => n == notification));
            AddAssert("anonymous", () => notification.BanchosucksAnonymous);
            AddAssert("text has no player name", () => notification.Text.ToString(), () => Does.Not.Contain(other.Username));
            AddAssert("text has no room name", () => notification.Text.ToString(), () => Does.Not.Contain("room of"));
            AddUntilStep("no drawn text has the name", () => notification.IsLoaded && !notification.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains(other.Username)));
        }

        [Test]
        public void TestInvitationKeepsNameWithoutStreamerMode()
        {
            MultiplayerInvitationNotification notification = null!;

            AddStep("post invitation", () => overlay.Post(notification = invitation()));
            AddUntilStep("shown", () => overlay.AllNotifications.Any(n => n == notification));
            AddAssert("not anonymous", () => notification.BanchosucksAnonymous, () => Is.False);
            AddAssert("text has the name", () => notification.Text.ToString(), () => Does.Contain(other.Username));
        }

        [Test]
        public void TestChatOverlayBlocked()
        {
            AddStep("streamer mode on", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, true));

            bool blocked = false;
            AddStep("listen", () =>
            {
                blocked = false;
                chatOverlay.BanchosucksBlockedByStreamerMode += () => blocked = true;
            });

            AddStep("try to open chat", () => chatOverlay.Show());
            AddAssert("chat stays hidden", () => chatOverlay.State.Value, () => Is.EqualTo(Visibility.Hidden));
            AddAssert("block reported", () => blocked);

            AddStep("streamer mode off", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, false));
            AddStep("open chat", () => chatOverlay.Show());
            AddAssert("chat visible", () => chatOverlay.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("close chat", () => chatOverlay.Hide());
        }

        [Test]
        public void TestLoginFieldMasked()
        {
            AddAssert("text drawn in clear", () => textBox.Masked, () => Is.False);
            AddAssert("no mask characters", () => textBox.ChildrenOfType<OsuPasswordTextBox.PasswordMaskChar>().Count(), () => Is.EqualTo(0));

            AddStep("streamer mode on", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, true));

            AddAssert("masked", () => textBox.Masked);
            AddAssert("text kept", () => textBox.Text, () => Is.EqualTo("streamer@example.com"));
            AddAssert("one dot per character", () => textBox.ChildrenOfType<OsuPasswordTextBox.PasswordMaskChar>().Count(c => c.IsAlive && c.Parent?.Parent?.IsAlive == true),
                () => Is.GreaterThanOrEqualTo("streamer@example.com".Length));
            AddAssert("no character drawn in clear", () => textBox.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Length == 1 && "streamer@example.com".Contains(t.Text.ToString())), () => Is.False);

            AddStep("streamer mode off", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, false));
            AddAssert("clear again", () => textBox.Masked, () => Is.False);
        }

        [Test]
        public void TestLoginFieldMaskedWhenCreatedInStreamerMode()
        {
            BanchosucksStreamerTextBox second = null!;

            AddStep("streamer mode on", () => config.SetValue(OsuSetting.BanchosucksStreamerMode, true));
            AddStep("create another box", () => content.Add(second = new BanchosucksStreamerTextBox
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = 60,
                Width = 300,
                Text = "peppy",
            }));

            AddUntilStep("loaded and masked", () => second.IsLoaded && second.Masked);
            AddAssert("dots only", () => second.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Length == 1 && "peppy".Contains(t.Text.ToString())), () => Is.False);
            AddAssert("text kept", () => second.Text, () => Is.EqualTo("peppy"));
        }

        private MessageNotifier.PrivateMessageNotification privateMessage()
        {
            var channel = new Channel(other) { Id = 77 };
            return new MessageNotifier.PrivateMessageNotification(new Message(1) { Sender = other, Content = "psst, secret" }, channel);
        }

        private MessageNotifier.MentionNotification mention()
        {
            var channel = new Channel { Id = 78, Name = "#osu", Type = ChannelType.Public };
            const string content = "hello streamer how are you";
            return new MessageNotifier.MentionNotification(new Message(2) { Sender = other, Content = content }, channel, MessageNotifier.MatchUsername(content, "streamer"));
        }

        private MultiplayerInvitationNotification invitation() => new MultiplayerInvitationNotification(other, new Room { Name = "room of SomeOtherPlayer" });

        private Notification friend(string className) => (Notification)Activator.CreateInstance(friendType(className), other)!;

        private Notification friends(string className) => (Notification)Activator.CreateInstance(friendType(className), (object)new[] { other, new APIUser { Id = 4243, Username = "AnotherOne" } })!;

        private static Type friendType(string className)
            => typeof(FriendPresenceNotifier).GetNestedType(className, BindingFlags.NonPublic) ?? throw new InvalidOperationException($"{className} no longer exists in {nameof(FriendPresenceNotifier)}");

        private partial class TestContainer : Container
        {
            [Cached]
            public ChannelManager ChannelManager { get; }

            [Cached(typeof(INotificationOverlay))]
            public NotificationOverlay NotificationOverlay { get; } = new NotificationOverlay
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
            };

            [Cached]
            public ChatOverlay ChatOverlay { get; } = new ChatOverlay();

            public BanchosucksStreamerTextBox TextBox { get; } = new BanchosucksStreamerTextBox
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 300,
                Text = "streamer@example.com",
            };

            public TestContainer(IAPIProvider api)
            {
                ChannelManager = new ChannelManager(api);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    ChannelManager,
                    ChatOverlay,
                    NotificationOverlay,
                    TextBox,
                };
            }
        }
    }
}

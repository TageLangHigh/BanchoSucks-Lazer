// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Online;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Overlays
{
    /// <summary>
    /// Banchosucks: decides for the <see cref="NotificationOverlay"/> which notifications wait ("do not disturb" during a
    /// map, streamer mode). The rules and the bookkeeping live in <see cref="BanchosucksNotificationHold{T}"/>; this class
    /// only knows the notification types and makes the hold safe to use from the threads notifications are posted from.
    /// </summary>
    public class BanchosucksNotificationGate
    {
        private readonly Lock syncRoot = new Lock();
        private readonly BanchosucksNotificationHold<Notification> hold = new BanchosucksNotificationHold<Notification>();

        private volatile bool streamerMode;

        /// <summary>
        /// Number of notifications currently waiting.
        /// </summary>
        public int HeldCount
        {
            get
            {
                lock (syncRoot)
                    return hold.Count;
            }
        }

        public static BanchosucksNotificationKind Classify(Notification notification)
        {
            if (notification.IsCritical)
                return BanchosucksNotificationKind.Critical;

            switch (notification)
            {
                case SimpleErrorNotification:
                    return BanchosucksNotificationKind.Error;

                case ProgressNotification:
                    return BanchosucksNotificationKind.Progress;

                case MessageNotifier.PrivateMessageNotification:
                    return BanchosucksNotificationKind.PrivateMessage;

                case MessageNotifier.MentionNotification:
                    return BanchosucksNotificationKind.ChatMention;
            }

            // the four friend notifications are private classes of the notifier
            Type? owner = notification.GetType().DeclaringType;

            if (owner == typeof(FriendPresenceNotifier))
                return BanchosucksNotificationKind.Friend;

            if (notification is UserAvatarNotification)
                return BanchosucksNotificationKind.OtherPlayer;

            return BanchosucksNotificationKind.Other;
        }

        /// <summary>
        /// Takes the notification if it has to wait. May be called from any thread.
        /// </summary>
        /// <returns>Whether the notification is held; if not, the overlay shows it as usual.</returns>
        public bool TryHold(Notification notification)
        {
            var kind = Classify(notification);

            lock (syncRoot)
                return hold.TryHold(notification, kind);
        }

        /// <summary>
        /// Switches a reason on or off and returns what no longer has to wait.
        /// </summary>
        public BanchosucksHoldRelease<Notification> SetActive(BanchosucksHoldReason reason, bool active)
        {
            if (reason == BanchosucksHoldReason.StreamerMode)
                streamerMode = active;

            lock (syncRoot)
                return hold.SetActive(reason, active);
        }

        /// <summary>
        /// Called right before a notification is handed to the overlay for display. In streamer mode a notification
        /// about another player (the ones not held back: multiplayer invitations) loses the name and the avatar.
        /// Has to run before the notification is loaded. May be called from any thread.
        /// </summary>
        public void Prepare(Notification notification)
        {
            if (!streamerMode || notification is not UserAvatarNotification about || about.BanchosucksAnonymous)
                return;

            about.BanchosucksAnonymous = true;

            if (about is MultiplayerInvitationNotification)
            {
                // the room name is left out as well, rooms are usually named after their host
                about.Text = ClickerStrings.Text("You were invited to a multiplayer room.", "Du wurdest in einen Mehrspieler-Raum eingeladen.");
                return;
            }

            string name = about.BanchosucksUsername;

            if (!string.IsNullOrEmpty(name))
                about.Text = about.Text.ToString().Replace(name, "…");
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;

namespace osu.Game.Overlays
{
    /// <summary>
    /// Banchosucks: "do not disturb" and the notification part of streamer mode.
    /// </summary>
    /// <remarks>
    /// Upstream already keeps non-critical notifications in a queue while overlays are disabled (active gameplay) and
    /// then shows every one of them as a toast, also in each break. With the setting on, notifications that are neither
    /// critical, errors nor running tasks are instead kept by the <see cref="BanchosucksNotificationGate"/> until the
    /// player has left the map, put straight into the overlay without toast or sound, and announced by one summary toast.
    /// Critical notifications and errors keep the upstream behaviour.
    /// Streamer mode uses the same gate for private message, mention and friend notifications, for as long as it is on.
    /// </remarks>
    public partial class NotificationOverlay
    {
        [Resolved]
        private OsuConfigManager? banchosucksConfig { get; set; }

        [Resolved]
        private ILocalUserPlayInfo? banchosucksPlayInfo { get; set; }

        private readonly BanchosucksNotificationGate banchosucksGate = new BanchosucksNotificationGate();

        private Bindable<bool>? banchosucksDoNotDisturb;
        private Bindable<bool>? banchosucksStreamerMode;
        private IBindable<LocalUserPlayingState>? banchosucksPlayingState;

        /// <summary>
        /// Number of notifications currently held back.
        /// </summary>
        public int BanchosucksHeldCount => banchosucksGate.HeldCount;

        private void initialiseBanchosucksHold()
        {
            if (banchosucksConfig == null)
                return;

            banchosucksDoNotDisturb = banchosucksConfig.GetBindable<bool>(OsuSetting.BanchosucksDoNotDisturb);
            banchosucksDoNotDisturb.BindValueChanged(_ => updateBanchosucksGameplayHold());

            banchosucksStreamerMode = banchosucksConfig.GetBindable<bool>(OsuSetting.BanchosucksStreamerMode);
            banchosucksStreamerMode.BindValueChanged(mode => deliverBanchosucksHeld(banchosucksGate.SetActive(BanchosucksHoldReason.StreamerMode, mode.NewValue)), true);

            if (banchosucksPlayInfo != null)
            {
                banchosucksPlayingState = banchosucksPlayInfo.PlayingState.GetBoundCopy();
                banchosucksPlayingState.BindValueChanged(_ => updateBanchosucksGameplayHold(), true);
            }
        }

        private void updateBanchosucksGameplayHold()
        {
            // a break or the pause menu is still "during the map": nothing pops up until the player has left it
            bool inMap = banchosucksPlayingState != null && banchosucksPlayingState.Value != LocalUserPlayingState.NotPlaying;

            deliverBanchosucksHeld(banchosucksGate.SetActive(BanchosucksHoldReason.Gameplay, inMap && banchosucksDoNotDisturb?.Value == true));
        }

        private void deliverBanchosucksHeld(BanchosucksHoldRelease<Notification> release)
        {
            if (release.Items.Count == 0)
                return;

            if (release.Items.Count == 1)
            {
                // a summary of one would only hide it
                post(release.Items[0], false);
                return;
            }

            foreach (var notification in release.Items)
                post(notification, true);

            post(new BanchosucksHeldSummaryNotification(release.EndedReason, release.Items.Count)
            {
                Activated = () =>
                {
                    Show();
                    return true;
                }
            }, false);
        }

        /// <summary>
        /// The one toast shown in place of the notifications that were held back.
        /// </summary>
        public partial class BanchosucksHeldSummaryNotification : SimpleNotification
        {
            public readonly BanchosucksHoldReason Reason;
            public readonly int Count;

            public BanchosucksHeldSummaryNotification(BanchosucksHoldReason reason, int count)
            {
                Reason = reason;
                Count = count;

                // only a pointer at the overlay, where the held notifications are now
                Transient = true;
                Icon = FontAwesome.Solid.BellSlash;
                Text = TextFor(reason, count);
            }

            public static LocalisableString TextFor(BanchosucksHoldReason reason, int count) => reason == BanchosucksHoldReason.StreamerMode
                ? ClickerStrings.Text("{0} notifications were held back in streamer mode", "{0} Meldungen im Streamer-Modus zurückgehalten", count)
                : ClickerStrings.Text("{0} notifications during the map", "{0} Meldungen während der Map", count);
        }
    }
}

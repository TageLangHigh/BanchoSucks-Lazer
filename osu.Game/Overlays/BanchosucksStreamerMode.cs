// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.OSD;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Overlays
{
    /// <summary>
    /// Banchosucks: streamer mode (<see cref="OsuSetting.BanchosucksStreamerMode"/>), the part that lives in <see cref="OsuGame"/>.
    /// </summary>
    /// <remarks>
    /// What the mode hides, and where:
    /// <list type="bullet">
    /// <item>the chat overlay cannot be opened (<c>ChatOverlay.Banchosucks.cs</c>) and is closed when the mode is switched on (here);</item>
    /// <item>private message, mention and friend notifications are held back and summarised when the mode ends
    /// (<c>NotificationOverlay.Banchosucks.cs</c>, <see cref="BanchosucksNotificationGate"/>);</item>
    /// <item>notifications about another player that still show (multiplayer invitation, duel) lose the name and the avatar
    /// (<see cref="BanchosucksNotificationGate.Prepare"/>);</item>
    /// <item>the account field of the login form shows dots (<c>BanchosucksStreamerTextBox</c>).</item>
    /// </list>
    /// Server announcements are dialogs, not notifications, and are not touched. Not hidden: chat inside multiplayer rooms
    /// and during multiplayer gameplay, names on leaderboards, in the spectator list, in the dashboard and on profiles,
    /// and the player's own name.
    /// </remarks>
    public partial class BanchosucksStreamerMode : Component
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private ChatOverlay? chatOverlay { get; set; }

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private Bindable<bool> enabled = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled = config.GetBindable<bool>(OsuSetting.BanchosucksStreamerMode);
            enabled.BindValueChanged(mode =>
            {
                if (mode.NewValue)
                    chatOverlay?.Hide();

                onScreenDisplay?.Display(new BanchosucksStreamerModeToast(mode.NewValue
                    ? ClickerStrings.Text("on", "an")
                    : ClickerStrings.Text("off", "aus")));
            });

            if (chatOverlay != null)
            {
                chatOverlay.BanchosucksBlockedByStreamerMode += onChatBlocked;

                if (enabled.Value)
                    chatOverlay.Hide();
            }
        }

        /// <summary>
        /// Switches the mode; bound to <see cref="Input.Bindings.GlobalAction.BanchosucksToggleStreamerMode"/>.
        /// </summary>
        public void Toggle() => enabled.Value = !enabled.Value;

        private void onChatBlocked() => onScreenDisplay?.Display(new BanchosucksStreamerModeToast(ClickerStrings.Text("chat is hidden", "Chat ist ausgeblendet")));

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (chatOverlay != null)
                chatOverlay.BanchosucksBlockedByStreamerMode -= onChatBlocked;
        }

        public partial class BanchosucksStreamerModeToast : Toast
        {
            public BanchosucksStreamerModeToast(LocalisableString value)
                : base(ClickerStrings.Text("Streamer mode", "Streamer-Modus"), value)
            {
            }
        }
    }
}

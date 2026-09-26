// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: a server announcement (BanchoBot private message starting with the megaphone marker,
    /// sent by the admin broadcast or <c>!alert</c>) shown as a dialog the player has to close, like the
    /// notification popups of osu!stable. Chat messages alone get swiped away or ignored.
    /// </summary>
    public partial class AnnouncementDialog : PopupDialog
    {
        /// <summary>
        /// Messages from BanchoBot that begin with this are announcements.
        /// </summary>
        public const string MARKER = "📢";

        public AnnouncementDialog(string text)
        {
            HeaderText = "Ankündigung";
            BodyText = text;
            Icon = FontAwesome.Solid.Bullhorn;
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "OK" },
            };
        }
    }
}

// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// The toolbar button which reveals the icon buttons that are currently hidden.
    /// </summary>
    public partial class ToolbarOverflowButton : ToolbarButton, IHasPopover
    {
        /// <summary>
        /// Creates the popover which lists the currently hidden buttons. Assigned by <see cref="Toolbar"/>.
        /// </summary>
        public Func<Popover>? CreatePopover;

        public ToolbarOverflowButton()
        {
            SetIcon(OsuIcon.EllipsisHorizontal);

            TooltipMain = ToolbarStrings.MoreIcons;
            TooltipSub = ToolbarStrings.HiddenIconsHeader;

            Action = this.ShowPopover;
        }

        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public Popover? GetPopover() => CreatePopover?.Invoke();
    }
}

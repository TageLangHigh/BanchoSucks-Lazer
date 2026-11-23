// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// Lists the toolbar icon buttons which are currently hidden, and allows revealing any of them temporarily.
    /// </summary>
    public partial class ToolbarOverflowPopover : OsuPopover
    {
        public const float ROW_WIDTH = 260;

        private const float row_height = 44;

        private readonly Toolbar toolbar;

        public ToolbarOverflowPopover(Toolbar toolbar)
            : base(false)
        {
            FillFlowContainer contentFlow;
            this.toolbar = toolbar;

            Body.CornerRadius = 5;

            Child = contentFlow = new FillFlowContainer
            {
                Width = ROW_WIDTH,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Vertical = 5 },
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = ToolbarStrings.HiddenIconsHeader,
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                        Margin = new MarginPadding { Left = 12, Bottom = 3 },
                        Alpha = 0.7f,
                    },
                },
            };

            // each button carries its own identifier, so the rows for the hidden ones can be built from the
            // buttons directly, rather than by pairing every identifier up with the button it belongs to.
            contentFlow.AddRange(toolbar.HiddenButtons.Select(button => new IconRow(button, () => select(button))));
            contentFlow.Add(new IconRow(FontAwesome.Solid.Cog, ToolbarStrings.ManageToolbarIcons, default, null, showManagementDialog));
        }

        [Resolved(CanBeNull = true)]
        private ToolbarIconManagementDialog? managementDialog { get; set; }

        private void select(ToolbarButton button)
        {
            // a button can only be hidden if it is part of the layout, so it always carries an identifier.
            toolbar.RevealTemporarily(button.ID!);
            this.HidePopover();
        }

        private void showManagementDialog()
        {
            this.HidePopover();
            managementDialog?.Show();
        }

        /// <summary>
        /// A single selectable entry of the hidden icon panel.
        /// </summary>
        private partial class IconRow : OsuClickableContainer
        {
            private readonly Box hoverBackground;

            /// <summary>
            /// Creates an entry describing a toolbar button.
            /// </summary>
            public IconRow(ToolbarButton button, Action action)
                : this(button.Icon ?? default, button.TooltipMain, button.TooltipSub, button.Hotkey, action)
            {
            }

            /// <summary>
            /// Creates an entry which is not tied to a toolbar button.
            /// </summary>
            public IconRow(IconUsage icon, LocalisableString main, LocalisableString sub, GlobalAction? hotkey, Action action)
            {
                Width = ROW_WIDTH;
                Height = row_height;
                Action = action;

                var content = new FillFlowContainer
                {
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Left = 12 },
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Icon = icon,
                            Size = new Vector2(20),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new FillFlowContainer
                        {
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = main,
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                },
                                new OsuSpriteText
                                {
                                    Text = sub,
                                    Font = OsuFont.GetFont(size: 11),
                                    Alpha = 0.65f,
                                },
                            },
                        },
                    },
                };

                var children = new Drawable[]
                {
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    content,
                };

                if (hotkey != null)
                {
                    children = children.Append(new HotkeyDisplay
                    {
                        Hotkey = new Hotkey(hotkey.Value),
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 12 },
                    }).ToArray();
                }

                Children = children;
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.05f, 100, Easing.Out);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeTo(0, 100, Easing.Out);
                base.OnHoverLost(e);
            }
        }
    }
}

// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// A single row of <see cref="ToolbarButtonOrderList"/>, showing one optional toolbar icon,
    /// its description, and whether it is displayed in the toolbar.
    /// </summary>
    public partial class ToolbarButtonOrderListItem : OsuRearrangeableListItem<string>
    {
        private const float item_height = 56;
        private const float content_padding = 10;

        /// <summary>
        /// How bright the flash shown when this item was moved by a swap is.
        /// </summary>
        private const float swap_flash_alpha = 0.15f;

        /// <summary>
        /// How long the flash shown when this item was moved by a swap takes to fade out.
        /// Deliberately longer than <see cref="ToolbarButtonOrderList.SWAP_DURATION"/>, so that the item is still
        /// called out once it has finished sliding into place.
        /// </summary>
        private const double swap_flash_duration = 500;

        private readonly Func<string, ToolbarButton?>? buttonLookup;
        private readonly string id;
        private readonly bool hidden;
        private readonly Action<string, bool>? hiddenChanged;

        private ItemContent? content;

        public ToolbarButtonOrderListItem(string item, Func<string, ToolbarButton?>? buttonLookup, Func<string, bool>? isHidden, Action<string, bool>? hiddenChanged)
            : base(item, true)
        {
            this.buttonLookup = buttonLookup;
            id = item;
            hidden = isHidden?.Invoke(item) ?? false;
            this.hiddenChanged = hiddenChanged;
        }

        /// <summary>
        /// Whether the flash shown when this item was moved by a swap is currently visible.
        /// </summary>
        public bool SwapHighlightVisible => content?.SwapHighlightVisible == true;

        /// <summary>
        /// Briefly calls attention to this item having been moved, so that the swap it was part of is easy to follow.
        /// </summary>
        public void PlaySwapAnimation() => content?.PlaySwapAnimation();

        protected override Drawable CreateContent()
        {
            ToolbarButton? button = buttonLookup?.Invoke(id);

            return content = new ItemContent(button, id, hidden, visible => hiddenChanged?.Invoke(id, !visible));
        }

        private partial class ItemContent : CompositeDrawable
        {
            private readonly Box background;
            private readonly Box hoverBackground;
            private readonly Box swapBackground;

            private readonly VisibilitySwitch visibilitySwitch;

            public ItemContent(ToolbarButton? button, string fallbackId, bool hidden, Action<bool> visibleChanged)
            {
                RelativeSizeAxes = Axes.X;
                Height = item_height;

                Masking = true;
                CornerRadius = 10;

                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    // drawn over the hover background so that a swap stays visible even while the row is hovered.
                    swapBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = content_padding },
                        RowDimensions = [new Dimension()],
                        ColumnDimensions =
                        [
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        ],
                        Content = new Drawable[][]
                        {
                            [
                                new SpriteIcon
                                {
                                    Icon = button?.Icon ?? OsuIcon.EllipsisHorizontal,
                                    Size = new Vector2(24),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding { Horizontal = 15 },
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Text = button?.TooltipMain ?? fallbackId,
                                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                        },
                                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Alpha = 0.6f,
                                            Text = button?.TooltipSub ?? fallbackId,
                                        },
                                    },
                                },
                                visibilitySwitch = new VisibilitySwitch
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Current = { Value = !hidden },
                                    Changed = visibleChanged,
                                },
                            ],
                        },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                background.Colour = colourProvider.Background5;
                hoverBackground.Colour = colourProvider.Highlight1;
                swapBackground.Colour = colourProvider.Highlight1;
            }

            protected override bool OnClick(ClickEvent e)
            {
                visibilitySwitch.Toggle();
                return true;
            }

            /// <summary>
            /// Whether the flash calling attention to a swap is currently visible.
            /// </summary>
            public bool SwapHighlightVisible => swapBackground.Alpha > 0;

            /// <summary>
            /// Briefly flashes this row, calling attention to it having been moved by a swap.
            /// </summary>
            public void PlaySwapAnimation()
            {
                swapBackground.ClearTransforms();
                swapBackground.Alpha = swap_flash_alpha;
                swapBackground.FadeTo(0, swap_flash_duration, Easing.OutCubic);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.05f, 100, Easing.Out);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeTo(0, 100, Easing.Out);
                base.OnHoverLost(e);
            }
        }

        private partial class VisibilitySwitch : SwitchButton
        {
            public Action<bool>? Changed;

            /// <summary>
            /// Toggles the switch as though it had been clicked, so that clicking the row's body cannot be told
            /// apart from clicking the switch itself.
            /// </summary>
            public void Toggle()
            {
                if (Current.Disabled)
                    return;

                // the same two steps a click on the switch performs, so that it is heard and seen the same way.
                Current.Value = !Current.Value;
                OnUserChange(Current.Value);
            }

            protected override void OnUserChange(bool value)
            {
                base.OnUserChange(value);
                Changed?.Invoke(value);
            }
        }
    }
}

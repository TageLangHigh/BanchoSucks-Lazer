// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public abstract partial class ToolbarButton : OsuClickableContainer, IKeyBindingHandler<GlobalAction>, IHasContextMenu
    {
        public const float PADDING = 3;

        /// <summary>
        /// The duration over which hiding or showing this button is animated.
        /// </summary>
        public const double COLLAPSE_DURATION = 300;

        public GlobalAction? Hotkey { get; protected set; }

        /// <summary>
        /// The stable identifier of this button, as used to persist its position and visibility in the user's
        /// toolbar layout. <c>null</c> for buttons which the user cannot reorder or hide.
        /// </summary>
        /// <remarks>
        /// The identifiers are the ones listed in <see cref="ToolbarButtonLayout"/>, and are deliberately decoupled
        /// from the button types, so that renaming or moving a button implementation does not invalidate the
        /// user's configuration.
        /// </remarks>
        public string? ID { get; init; }

        /// <summary>
        /// The icon this button is displaying, if it was set via <see cref="SetIcon(IconUsage)"/>.
        /// </summary>
        public IconUsage? Icon { get; private set; }

        /// <summary>
        /// Provides the items to display when this button is right-clicked.
        /// When null (the default), this button is not considered a context menu target.
        /// </summary>
        internal Func<MenuItem[]?>? ContextMenuItemsProvider;

        public MenuItem[]? ContextMenuItems => ContextMenuItemsProvider?.Invoke();

        private bool collapsed;

        /// <summary>
        /// Whether this button is currently taking up no space in the toolbar, and no longer accepts positional input.
        /// Collapsed buttons remain present, and thus keep receiving the keyboard input which triggers their hotkey.
        /// </summary>
        /// <remarks>
        /// Only usable by buttons whose <see cref="ButtonContent"/> is sized explicitly, i.e. the optional icon buttons.
        /// </remarks>
        public bool Collapsed
        {
            get => collapsed;
            set
            {
                if (collapsed == value)
                    return;

                collapsed = value;

                // this button's own width is automatic, and therefore derived from its content, so animating the
                // content is what animates the size of the button - and with it the room it takes up in the toolbar.
                // the content is faded at the same time, so that it does not linger over its neighbours as it shrinks.
                // the easing is deliberately gentler than the OutQuint used elsewhere, as a front-loaded curve
                // makes the button look like it snaps shut rather than slides.
                ButtonContent.ResizeWidthTo(value ? 0 : Toolbar.HEIGHT, COLLAPSE_DURATION, Easing.OutCubic);

                if (value)
                    ButtonContent.FadeOut(COLLAPSE_DURATION, Easing.OutCubic);
                else
                    ButtonContent.FadeIn(COLLAPSE_DURATION, Easing.OutCubic);
            }
        }

        public void SetIcon(Drawable icon)
        {
            IconContainer.Icon = icon;
            IconContainer.Show();
        }

        public void SetIcon(IconUsage icon)
        {
            Icon = icon;
            SetIcon(new SpriteIcon { Icon = icon });
        }

        public LocalisableString TooltipMain
        {
            get => tooltip1.Text;
            set => tooltip1.Text = value;
        }

        public LocalisableString TooltipSub
        {
            get => tooltip2.Text;
            set => tooltip2.Text = value;
        }

        protected virtual Anchor TooltipAnchor => Anchor.TopLeft;

        protected readonly Container ButtonContent;
        protected ConstrainedIconContainer IconContainer;
        protected Box HoverBackground;
        private readonly Box flashBackground;
        private readonly FillFlowContainer tooltipContainer;
        private readonly SpriteText tooltip1;
        private readonly SpriteText tooltip2;
        protected FillFlowContainer Flow;

        protected readonly Container BackgroundContent;

        private readonly FillFlowContainer subTooltipFlow;

        protected ToolbarButton()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                ButtonContent = new Container
                {
                    Width = Toolbar.HEIGHT,
                    RelativeSizeAxes = Axes.Y,
                    Padding = new MarginPadding(PADDING),
                    Children = new Drawable[]
                    {
                        BackgroundContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 6,
                            CornerExponent = 3f,
                            Children = new Drawable[]
                            {
                                HoverBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = OsuColour.Gray(80).Opacity(180),
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                },
                                flashBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Colour = Color4.White.Opacity(100),
                                    Blending = BlendingParameters.Additive,
                                },
                            }
                        },
                        Flow = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Padding = new MarginPadding { Left = Toolbar.HEIGHT / 2, Right = Toolbar.HEIGHT / 2 },
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Children = new Drawable[]
                            {
                                IconContainer = new ConstrainedIconContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(20),
                                    Alpha = 0,
                                },
                            },
                        },
                    },
                },
                tooltipContainer = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    RelativeSizeAxes = Axes.Both, // stops us being considered in parent's autosize
                    Anchor = TooltipAnchor.HasFlag(Anchor.x0) ? Anchor.BottomLeft : Anchor.BottomRight,
                    Origin = TooltipAnchor,
                    Position = new Vector2(TooltipAnchor.HasFlag(Anchor.x0) ? 5 : -5, 5),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        tooltip1 = new OsuSpriteText
                        {
                            Anchor = TooltipAnchor,
                            Origin = TooltipAnchor,
                            Shadow = true,
                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                        },
                        subTooltipFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = TooltipAnchor,
                            Origin = TooltipAnchor,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                tooltip2 = new OsuSpriteText { Shadow = true },
                            }
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Hotkey != null)
            {
                subTooltipFlow.Add(new HotkeyDisplay
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Hotkey = new Hotkey(Hotkey.Value),
                    Margin = new MarginPadding { Left = 3 },
                });
            }
        }

        // a collapsed button has no width of its own, but its content still does.
        // without this, that content would keep receiving hover and click events.
        public override bool PropagatePositionalInputSubTree => !collapsed && base.PropagatePositionalInputSubTree;

        protected override bool OnMouseDown(MouseDownEvent e) => false;

        protected override bool OnClick(ClickEvent e)
        {
            flashBackground.FadeIn(50).Then().FadeOutFromOne(800, Easing.OutQuint);
            tooltipContainer.FadeOut(100);
            return base.OnClick(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            HoverBackground.FadeIn(300, Easing.OutQuint);
            tooltipContainer.FadeIn(200, Easing.OutQuint);

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            HoverBackground.FadeOut(200, Easing.Out);
            tooltipContainer.FadeOut(100, Easing.Out);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == Hotkey && !e.Repeat)
            {
                TriggerClick();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }

    public partial class OpaqueBackground : Container
    {
        public OpaqueBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(30)
                },
                new Triangles
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = OsuColour.Gray(40),
                    ColourDark = OsuColour.Gray(20),
                },
            };
        }
    }
}

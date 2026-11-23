// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A single option of a <see cref="FormToggleGroup{T}"/>.
    /// </summary>
    /// <remarks>
    /// The option can be displayed as an icon, a text label, or both. Its <see cref="ClickableContainer.Action"/>
    /// is assigned by the group it is added to, and should not be set manually.
    /// </remarks>
    public partial class FormToggleGroupItem<T> : ClickableContainer, IHasTooltip
    {
        /// <summary>
        /// The height every option is rendered at.
        /// </summary>
        public const float HEIGHT = 40;

        /// <summary>
        /// The gap between two adjacent options, split evenly around each of them.
        /// The gap is still part of the option's interactive area.
        /// </summary>
        private const float spacing = 4;

        private const float corner_radius = 5;

        private const float icon_size = 16;
        private const float text_size = 14;
        private const float icon_text_spacing = 6;

        /// <summary>
        /// The value this option represents.
        /// </summary>
        public T Value { get; init; } = default!;

        /// <summary>
        /// The label displayed by this option, if any.
        /// </summary>
        public LocalisableString Text { get; init; }

        /// <summary>
        /// The icon displayed by this option, if any. Displayed on its own (or next to <see cref="Text"/>) when set.
        /// </summary>
        public IconUsage? Icon { get; init; }

        public LocalisableString TooltipText { get; set; }

        private bool selected;

        /// <summary>
        /// Whether this option is the currently selected one.
        /// </summary>
        public bool Selected
        {
            get => selected;
            internal set
            {
                if (selected == value)
                    return;

                selected = value;
                updateState();
            }
        }

        /// <summary>
        /// Whether the visual state can be applied yet, i.e. whether <see cref="load"/> has run.
        /// </summary>
        private bool ready;

        private FormControlBackground background = null!;

        /// <summary>
        /// The icon and label of this option. Colours applied to it are inherited by both.
        /// </summary>
        private Drawable content = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public FormToggleGroupItem()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            Padding = new MarginPadding(spacing / 2);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = corner_radius,
                    CornerExponent = FormControlBackground.CORNER_EXPONENT,
                    Child = content = createContent(),
                },
            };

            Enabled.BindValueChanged(_ => updateState());
            ready = true;
            updateState();
        }

        /// <summary>
        /// Builds the content displayed by this option.
        /// </summary>
        private Drawable createContent()
        {
            if (!Icon.HasValue && Text == default)
                return Empty();

            return new OptionContent(Icon, Text);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            colourProvider.ColoursChanged += updateState;
        }

        protected override void Dispose(bool isDisposing)
        {
            // this drawable can be disposed before it has ever been loaded (eg. the game being torn down
            // while the overlay hosting it is still initialising), in which case the provider is still null.
            if (isDisposing && LoadState >= LoadState.Ready)
                colourProvider.ColoursChanged -= updateState;

            base.Dispose(isDisposing);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        protected override bool OnClick(ClickEvent e)
        {
            bool handled = base.OnClick(e);

            if (Enabled.Value)
                background.FlashOnCommit();

            return handled;
        }

        private void updateState()
        {
            if (!ready)
                return;

            // a selected option reuses the focused style, which is the "this is the active one" look
            // used throughout the rest of the form controls.
            background.VisualStyle = !Enabled.Value
                ? VisualStyle.Disabled
                : selected
                    ? VisualStyle.Focused
                    : IsHovered
                        ? VisualStyle.Hovered
                        : VisualStyle.Normal;

            Colour4 contentColour = Enabled.Value ? colourProvider.Content1 : colourProvider.Background1;

            // the icon and the label both inherit this colour, including the copy the label is scrolled over.
            content.Colour = contentColour;
        }

        /// <summary>
        /// The icon and label displayed by a single option.
        /// </summary>
        private partial class OptionContent : CompositeDrawable
        {
            private const float scroll_gap = 30;
            private const double scroll_delay = 800;
            private const float scroll_speed = 40;

            private const float horizontal_padding = 10;

            private readonly IconUsage? icon;
            private readonly LocalisableString text;

            private SpriteIcon? iconSprite;
            private OsuSpriteText? label;
            private OsuSpriteText labelFiller = null!;
            private Container viewport = null!;
            private FillFlowContainer flow = null!;

            private bool scrolling;
            private float scrollingDistance;

            private float lastAvailableWidth;
            private float lastLabelWidth;

            public OptionContent(IconUsage? icon, LocalisableString text)
            {
                this.icon = icon;
                this.text = text;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (icon.HasValue)
                {
                    AddInternal(iconSprite = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(icon_size),
                        Icon = icon.Value,
                    });
                }

                if (text == default)
                    return;

                AddInternal(viewport = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.Y,
                    Masking = true,
                    Child = flow = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(scroll_gap, 0),
                        Children = new Drawable[]
                        {
                            label = createLabel(),
                            labelFiller = createLabel(),
                        },
                    },
                });

                // the filler only exists to make the scroll loop seamless, so it is invisible unless scrolling.
                labelFiller.Alpha = 0;
            }

            private OsuSpriteText createLabel() => new OsuSpriteText
            {
                Font = OsuFont.Default.With(size: text_size),
                Text = text,
            };

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                float availableWidth = DrawWidth;
                float labelWidth = label?.DrawWidth ?? 0;

                // the layout only depends on the space available to the option and the width of the label,
                // both of which are measured here rather than required to be given up-front.
                if (availableWidth == lastAvailableWidth && labelWidth == lastLabelWidth)
                    return;

                lastAvailableWidth = availableWidth;
                lastLabelWidth = labelWidth;

                updateLayout();
            }

            private void updateLayout()
            {
                float labelWidth = lastLabelWidth;
                float iconWidth = iconSprite?.DrawWidth ?? 0;
                float gap = iconSprite != null && label != null ? icon_text_spacing : 0;
                float contentWidth = iconWidth + gap + labelWidth;

                // the border of the form control background is drawn inside the option, so the space the content
                // may occupy is inset on both sides to keep clear of it.
                float innerWidth = Math.Max(lastAvailableWidth - horizontal_padding * 2, 0);

                // the content only overflows once it stops fitting, at which point the icon stops being part of
                // the centred group and the label is given all of the remaining space to scroll through.
                bool overflows = contentWidth > innerWidth;

                float contentX = horizontal_padding + (overflows ? 0 : (innerWidth - contentWidth) / 2);

                iconSprite?.X = contentX;

                if (label == null)
                    return;

                float viewportX = contentX + iconWidth + gap;

                viewport.X = viewportX;
                viewport.Width = Math.Max(overflows ? innerWidth - iconWidth - gap : labelWidth, 0);

                updateScrolling(overflows);
            }

            private void updateScrolling(bool scroll)
            {
                if (!scroll)
                {
                    if (!scrolling)
                        return;

                    scrolling = false;
                    flow.ClearTransforms();
                    labelFiller.Alpha = 0;
                    flow.MoveToX(0, 300, Easing.OutQuint);
                    return;
                }

                float distance = lastLabelWidth + scroll_gap;

                // nothing to do unless the scroll has to be (re)started, which happens when the label changes
                // width (e.g. courtesy of a language change) as well as when the overflow first appears.
                if (scrolling && distance == scrollingDistance)
                    return;

                if (!scrolling)
                {
                    scrolling = true;
                    labelFiller.Alpha = 1;
                }

                scrollingDistance = distance;

                flow.ClearTransforms();
                flow.MoveToX(0);

                flow.Delay(scroll_delay)
                    .MoveToX(-distance, distance * 1000 / scroll_speed)
                    .Loop();
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: a plain bar chart (one bar per day) built from boxes, with a tooltip per bar.
    /// </summary>
    public partial class BanchosucksBarChart : CompositeDrawable
    {
        /// <summary>
        /// One bar.
        /// </summary>
        /// <param name="Value">Height of the bar, in the unit of the chart.</param>
        /// <param name="Inner">Optional part of <paramref name="Value"/> drawn in the second colour (passed maps within played maps).</param>
        /// <param name="Tooltip">Shown when hovering the bar's column.</param>
        /// <param name="Faded">Drawn paler (a day whose numbers are estimated).</param>
        public readonly record struct Bar(double Value, double? Inner, LocalisableString Tooltip, bool Faded);

        private const float chart_height = 110;

        private readonly Color4 colour;
        private readonly Color4 innerColour;

        private readonly Container columns;
        private readonly OsuSpriteText scaleText;
        private readonly OsuSpriteText leftText;
        private readonly OsuSpriteText rightText;

        public BanchosucksBarChart(LocalisableString title, Color4 colour, Color4 innerColour)
        {
            this.colour = colour;
            this.innerColour = innerColour;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 24,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = title,
                                Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold),
                            },
                            scaleText = new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Font = OsuFont.GetFont(size: 13),
                                Alpha = 0.6f,
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = chart_height,
                        Children = new Drawable[]
                        {
                            columns = new Container { RelativeSizeAxes = Axes.Both },
                            new Box
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                Height = 1,
                                Alpha = 0.35f,
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 18,
                        Children = new Drawable[]
                        {
                            leftText = new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Font = OsuFont.GetFont(size: 12),
                                Alpha = 0.6f,
                            },
                            rightText = new OsuSpriteText
                            {
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
                                Font = OsuFont.GetFont(size: 12),
                                Alpha = 0.6f,
                            },
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Number of bars currently shown with a height above zero (for tests).
        /// </summary>
        public int FilledBars { get; private set; }

        /// <param name="bars">Oldest first.</param>
        /// <param name="maximum">The value a full-height bar stands for.</param>
        /// <param name="scale">Describes the top of the chart ("max 42 min").</param>
        /// <param name="left">Label under the first bar.</param>
        /// <param name="right">Label under the last bar.</param>
        public void SetBars(IReadOnlyList<Bar> bars, double maximum, LocalisableString scale, LocalisableString left, LocalisableString right)
        {
            scaleText.Text = scale;
            leftText.Text = left;
            rightText.Text = right;

            columns.Clear();
            FilledBars = 0;

            if (bars.Count == 0)
                return;

            float width = 1f / bars.Count;

            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                float height = heightFor(bar.Value, maximum);
                float inner = bar.Inner == null ? 0 : heightFor(Math.Min(bar.Inner.Value, bar.Value), maximum);

                if (height > 0)
                    FilledBars++;

                columns.Add(new Column(bar.Tooltip, colour, innerColour, height, inner, bar.Faded)
                {
                    RelativePositionAxes = Axes.X,
                    RelativeSizeAxes = Axes.Both,
                    X = i * width,
                    Width = width,
                });
            }
        }

        private static float heightFor(double value, double maximum)
        {
            if (maximum <= 0 || value <= 0 || double.IsNaN(value))
                return 0;

            // a day with anything on it stays visible next to a much larger one
            return (float)Math.Clamp(value / maximum, 0.02, 1);
        }

        private partial class Column : CompositeDrawable, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            private readonly Box hover;

            public Column(LocalisableString tooltip, Color4 colour, Color4 innerColour, float height, float innerHeight, bool faded)
            {
                TooltipText = tooltip;

                InternalChildren = new Drawable[]
                {
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 2 },
                        Alpha = faded ? 0.45f : 1,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                RelativeSizeAxes = Axes.Both,
                                Height = height,
                                Colour = colour,
                            },
                            new Box
                            {
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                RelativeSizeAxes = Axes.Both,
                                Height = innerHeight,
                                Colour = innerColour,
                                Alpha = innerHeight > 0 ? 1 : 0,
                            },
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.FadeTo(0.12f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(200);
                base.OnHoverLost(e);
            }
        }
    }
}

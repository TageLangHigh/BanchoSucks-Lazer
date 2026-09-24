// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// The big osu! circle: glow, approach circle, orbiting cursors (one per Cursor-Trail) and hit bursts.
    /// </summary>
    internal partial class ClickerCircle : CompositeDrawable
    {
        public const float SIZE = 300;

        private const int max_orbit_cursors = 60;

        public Action<Vector2?>? Clicked;

        private readonly Color4 colour;
        private readonly CircularContainer body;
        private readonly CircularContainer approach;
        private readonly CircularContainer halo;
        private readonly Container orbit;
        private readonly Container bursts;
        private readonly Random random = new Random();
        private int orbitCount = -1;

        public ClickerCircle(Color4 colour)
        {
            this.colour = colour;
            Size = new Vector2(SIZE);

            InternalChildren = new Drawable[]
            {
                halo = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = colour.Opacity(0.55f),
                        Radius = 70,
                        Hollow = true,
                    },
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
                orbit = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                approach = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 6,
                    BorderColour = colour,
                    Alpha = 0,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
                body = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 14,
                    BorderColour = Color4.White,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(0.4f),
                        Radius = 20,
                        Offset = new Vector2(0, 6),
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(colour.Lighten(0.35f), colour.Darken(0.35f)),
                        },
                        // a soft highlight in the upper half, like on a skin's hitcircle
                        new Circle
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(0.8f, 0.45f),
                            Y = 18,
                            Colour = ColourInfo.GradientVertical(Color4.White.Opacity(0.35f), Color4.White.Opacity(0)),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "osu!",
                            Font = OsuFont.GetFont(size: 96, weight: FontWeight.Black),
                            Shadow = true,
                        },
                    },
                },
                bursts = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // an approach circle closing in, again and again
            Scheduler.AddDelayed(() =>
            {
                approach.ScaleTo(1.6f).FadeTo(0)
                        .ScaleTo(1f, 900)
                        .FadeTo(0.8f, 250)
                        .Then().FadeOut(150);
            }, 1100, true);

            halo.Loop(h => h.FadeTo(1f, 900, Easing.InOutSine).Then().FadeTo(0.45f, 900, Easing.InOutSine));
            orbit.Spin(24000, RotationDirection.Clockwise);
        }

        /// <summary>
        /// Shows one small cursor circling the osu! circle per Cursor-Trail (up to 60, in two rings).
        /// </summary>
        public void SetCursorCount(int count)
        {
            count = Math.Min(count, max_orbit_cursors);
            if (count == orbitCount)
                return;

            orbitCount = count;
            orbit.Clear();

            for (int i = 0; i < count; i++)
            {
                bool inner = i < 30;
                int ringCount = inner ? Math.Min(count, 30) : count - 30;
                int indexInRing = inner ? i : i - 30;
                float radius = SIZE / 2 + (inner ? 32 : 58);
                float angle = MathF.PI * 2 * indexInRing / ringCount + (inner ? 0 : MathF.PI / ringCount);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var home = direction * radius;

                var cursor = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.MousePointer,
                    Size = new Vector2(inner ? 18 : 15),
                    Colour = Color4.White,
                    Position = home,
                    // the icon's tip points up-left; turn it towards the circle
                    Rotation = MathHelper.RadiansToDegrees(angle) - 45,
                    Shadow = true,
                };

                orbit.Add(cursor);

                // every cursor pokes the circle now and then, one after another
                cursor.Delay(i * 97 % 2000)
                      .Loop(1600, c => c.MoveTo(home - direction * 9, 120, Easing.OutQuad).Then().MoveTo(home, 280, Easing.InQuad));
            }
        }

        public void Press() => body.ScaleTo(0.94f, 40, Easing.OutQuint);

        public void Release() => body.ScaleTo(1f, 400, Easing.OutElastic);

        /// <summary>
        /// A ring and a few sparks in the given combo colour.
        /// </summary>
        public void Hit(Color4 hitColour)
        {
            var ring = new CircularContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                BorderThickness = 6,
                BorderColour = hitColour,
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
            };

            bursts.Add(ring);
            ring.ScaleTo(1.02f).ScaleTo(1.32f, 320, Easing.OutQuint).FadeOutFromOne(320, Easing.OutQuad).Expire();

            for (int i = 0; i < 6; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

                var spark = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(6 + (float)random.NextDouble() * 6),
                    Colour = hitColour,
                    Position = direction * (SIZE / 2),
                };

                bursts.Add(spark);
                spark.MoveTo(direction * (SIZE / 2 + 50 + (float)random.NextDouble() * 50), 450, Easing.OutQuint)
                     .FadeOut(450, Easing.InQuad)
                     .Expire();
            }
        }

        /// <summary>
        /// Bigger glow for combo milestones.
        /// </summary>
        public void Kiai()
        {
            halo.ScaleTo(1.25f, 80, Easing.OutQuint).Then().ScaleTo(1f, 700, Easing.OutQuint);
            body.FlashColour(Color4.White, 400, Easing.OutQuint);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => body.ReceivePositionalInputAt(screenSpacePos);

        // counts on press like a hit circle, which also makes the BPM counter fair for mouse tapping
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            Press();
            Clicked?.Invoke(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            Release();
            base.OnMouseUp(e);
        }
    }

    /// <summary>
    /// The bonus spinner that appears every one to two and a half minutes.
    /// </summary>
    internal partial class BonusSpinner : CompositeDrawable
    {
        public Action? Clicked;

        private readonly Container disc;
        private readonly SpriteIcon icon;

        public BonusSpinner(Color4 colour)
        {
            Size = new Vector2(120);
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                disc = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = 7,
                    BorderColour = Color4.White,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = colour.Opacity(0.8f),
                        Radius = 26,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(colour.Lighten(0.4f), colour.Darken(0.3f)),
                        },
                        icon = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(52),
                            Icon = FontAwesome.Solid.Sync,
                            Colour = Color4.Black.Opacity(0.7f),
                        },
                    },
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Y = 8,
                    Text = "BONUS!",
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Black),
                    Colour = colour,
                    Shadow = true,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300).ScaleTo(0.5f).ScaleTo(1f, 600, Easing.OutElastic);
            icon.Spin(900, RotationDirection.Clockwise);
            disc.Loop(d => d.ScaleTo(1.08f, 400, Easing.InOutSine).Then().ScaleTo(1f, 400, Easing.InOutSine));
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => disc.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            Clicked?.Invoke();
            Clicked = null;
            return true;
        }
    }

    /// <summary>
    /// osu!-style combo counter ("123x").
    /// </summary>
    internal partial class ClickerComboCounter : CompositeDrawable
    {
        private readonly OsuSpriteText text;
        private readonly OsuSpriteText popOut;

        public int Current { get; private set; }

        public ClickerComboCounter()
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                popOut = new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Font = OsuFont.GetFont(size: 56, weight: FontWeight.Black),
                    Alpha = 0,
                },
                text = new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Font = OsuFont.GetFont(size: 56, weight: FontWeight.Black),
                    Text = "0x",
                    Shadow = true,
                },
            };
        }

        public void Increment()
        {
            Current++;
            text.Text = popOut.Text = $"{Current}x";
            text.FadeColour(Color4.White);
            popOut.FadeTo(0.5f).ScaleTo(1f).ScaleTo(1.5f, 250, Easing.OutQuint).FadeOut(250);
            text.ScaleTo(1.1f, 40).Then().ScaleTo(1f, 200, Easing.OutQuint);
        }

        public void Break()
        {
            Current = 0;
            text.FlashColour(Color4.Red, 600, Easing.OutQuint);
            text.Text = "0x";
        }
    }

    /// <summary>
    /// Owned buildings as small coloured badges ("🥁 12").
    /// </summary>
    internal partial class ClickerBuildingStrip : FillFlowContainer
    {
        private string lastKey = string.Empty;

        public ClickerBuildingStrip()
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8);
        }

        public void SetCounts(IEnumerable<(ClickerProducer producer, int count)> counts)
        {
            var owned = counts.Where(c => c.count > 0).ToArray();
            string key = string.Join(',', owned.Select(c => $"{c.producer.Id}={c.count}"));
            if (key == lastKey)
                return;

            lastKey = key;
            Clear();

            foreach (var (producer, count) in owned)
            {
                Add(new Container
                {
                    AutoSizeAxes = Axes.X,
                    Height = 30,
                    Masking = true,
                    CornerRadius = 15,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = producer.Colour.Opacity(0.3f),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Padding = new MarginPadding { Horizontal = 12 },
                            Children = new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Icon = producer.Icon,
                                    Size = new Vector2(14),
                                    Colour = producer.Colour.Lighten(0.4f),
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = count.ToString("N0"),
                                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                                },
                            },
                        },
                    },
                });
            }
        }
    }

    /// <summary>
    /// One building or upgrade in the shop.
    /// </summary>
    internal partial class ClickerShopRow : CompositeDrawable
    {
        private readonly Func<string> detail;
        private readonly Func<string> buttonText;
        private readonly Func<bool> enabled;
        private readonly Func<string>? bigCount;
        private readonly Color4 accent;
        private readonly Box background;
        private readonly Box accentBar;
        private readonly OsuSpriteText detailText;
        private readonly OsuSpriteText countText;
        private readonly RoundedButton button;
        private string lastDetail = string.Empty;
        private string lastButtonText = string.Empty;
        private string lastCount = string.Empty;
        private bool? lastEnabled;

        public ClickerShopRow(Drawable icon, Color4 accent, string title, string description, Func<string> detail, Func<string> buttonText,
                              Func<bool> enabled, Action action, Func<string>? bigCount = null)
        {
            this.detail = detail;
            this.buttonText = buttonText;
            this.enabled = enabled;
            this.bigCount = bigCount;
            this.accent = accent;

            RelativeSizeAxes = Axes.X;
            Height = 78;
            Masking = true;
            CornerRadius = 10;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.35f,
                },
                accentBar = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 5,
                    Colour = accent,
                    Alpha = 0.3f,
                },
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 16,
                    Size = new Vector2(54),
                    Child = icon,
                },
                countText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -150,
                    Font = OsuFont.GetFont(size: 34, weight: FontWeight.Black),
                    Colour = accent,
                    Alpha = 0.35f,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = 82, Right = bigCount != null ? 205 : 150, Vertical = 9 },
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 1),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = title,
                            Font = OsuFont.GetFont(size: 19, weight: FontWeight.Bold),
                        },
                        new TruncatingSpriteText
                        {
                            Text = description,
                            Font = OsuFont.GetFont(size: 13),
                            RelativeSizeAxes = Axes.X,
                            Alpha = 0.75f,
                        },
                        detailText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                            Colour = accent.Lighten(0.3f),
                        },
                    },
                },
                button = new RoundedButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -10,
                    Width = 130,
                    Action = action,
                    BackgroundColour = accent.Darken(0.45f),
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(0.55f, 150, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(0.35f, 300, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();

            string newDetail = detail();
            if (newDetail != lastDetail)
                detailText.Text = lastDetail = newDetail;

            string newButtonText = buttonText();
            if (newButtonText != lastButtonText)
                button.Text = lastButtonText = newButtonText;

            if (bigCount != null)
            {
                string newCount = bigCount();
                if (newCount != lastCount)
                    countText.Text = lastCount = newCount;
            }

            bool isEnabled = enabled();
            if (isEnabled != lastEnabled)
            {
                lastEnabled = isEnabled;
                button.Enabled.Value = isEnabled;
                accentBar.FadeTo(isEnabled ? 1f : 0.3f, 200);
                if (isEnabled)
                    accentBar.FlashColour(Color4.White, 400);
            }
        }

        /// <summary>
        /// Rounded, coloured square with a FontAwesome icon.
        /// </summary>
        public static Drawable CreateIcon(IconUsage icon, Color4 colour) => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 12,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(colour.Lighten(0.2f), colour.Darken(0.4f)),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = icon,
                    Size = new Vector2(26),
                    Shadow = true,
                },
            },
        };
    }

    /// <summary>
    /// Simple tab header button for the side panel.
    /// </summary>
    internal partial class ClickerTabButton : OsuClickableContainer
    {
        private readonly OsuSpriteText label;
        private readonly Box underline;
        private readonly Color4 activeColour;
        private bool active;

        public ClickerTabButton(string text, IconUsage icon, Color4 activeColour, Action action)
        {
            this.activeColour = activeColour;
            Action = action;
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Padding = new MarginPadding { Horizontal = 6 },
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = icon,
                            Size = new Vector2(16),
                        },
                        label = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                        },
                    },
                },
                underline = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Colour = activeColour,
                    Alpha = 0,
                },
            };
        }

        public bool Active
        {
            get => active;
            set
            {
                active = value;
                underline.FadeTo(value ? 1 : 0, 200);
                this.FadeColour(value ? Color4.White : Color4.White.Opacity(0.55f), 200);
                label.FadeColour(value ? activeColour.Lighten(0.5f) : Color4.White, 200);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Banchosucks.Clicker;
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

        /// <summary>
        /// Called on every press with the screen position; returns false when the tap was ignored (input lock).
        /// </summary>
        public Func<Vector2?, bool>? Clicked;

        private readonly Color4 colour;
        private readonly CircularContainer body;
        private readonly CircularContainer approach;
        private readonly CircularContainer halo;
        private readonly Container orbit;
        private readonly Container bursts;
        private readonly Random random = new Random();
        private int orbitCount = -1;
        private bool boosted;

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

        /// <summary>
        /// Lights the circle up like a kiai section while an ability is active.
        /// </summary>
        public void SetBoosted(bool value)
        {
            if (boosted == value)
                return;

            boosted = value;
            halo.FadeEdgeEffectTo(value ? colour.Lighten(0.3f).Opacity(0.95f) : colour.Opacity(0.55f), 300, Easing.OutQuint);
            halo.ScaleTo(value ? 1.12f : 1f, 300, Easing.OutQuint);
            body.BorderColour = value ? colour.Lighten(0.8f) : Color4.White;
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
            halo.ScaleTo(1.25f, 80, Easing.OutQuint).Then().ScaleTo(boosted ? 1.12f : 1f, 700, Easing.OutQuint);
            body.FlashColour(Color4.White, 400, Easing.OutQuint);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => body.ReceivePositionalInputAt(screenSpacePos);

        // counts on press like a hit circle, which also makes the BPM counter fair for mouse tapping
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (Clicked?.Invoke(e.ScreenSpaceMousePosition) != false)
                Press();

            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            Release();
            base.OnMouseUp(e);
        }
    }

    /// <summary>
    /// The bonus spinner that appears every one to two and a half minutes; a kiai spinner is worth five times as much.
    /// </summary>
    internal partial class BonusSpinner : CompositeDrawable
    {
        public Action? Clicked;

        private readonly Container disc;
        private readonly SpriteIcon icon;

        public BonusSpinner(Color4 colour, bool kiai = false)
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

            if (kiai)
            {
                AddInternal(new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -6,
                    Text = "Kiai!",
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Black, italics: true),
                    Colour = colour.Lighten(0.5f),
                    Shadow = true,
                });
            }
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
    /// A slider event: press the start circle, then keep a mouse button held and follow the ball along the path.
    /// The reward scales with the share of the travel time the cursor stayed inside the follow circle, which an
    /// autoclicker cannot do.
    /// </summary>
    internal partial class ClickerSliderEvent : CompositeDrawable
    {
        public const float RADIUS = 30;

        private const float follow_scale = 2.2f;
        private const double idle_lifetime = 8000;

        /// <summary>
        /// Called once with the tracking accuracy (0..1) when the ball reaches the end.
        /// </summary>
        public Action<double>? Completed;

        public bool Started => !double.IsNaN(startTime);
        public bool Finished { get; private set; }

        private readonly Vector2[] vertices;
        private readonly double durationMs;
        private readonly Color4 colour;
        private readonly SmoothPath outerPath;
        private readonly SmoothPath innerPath;
        private readonly CircularContainer startCircle;
        private readonly CircularContainer ball;
        private readonly CircularContainer follow;
        private readonly OsuSpriteText label;

        private InputManager? inputManager;
        private double startTime = double.NaN;
        private double trackedTime;
        private double totalTime;

        /// <param name="vertices">The path in local coordinates (at least two points, all further than <see cref="RADIUS"/> from the top-left).</param>
        /// <param name="durationMs">Travel time of the ball.</param>
        /// <param name="colour">Accent colour of the slider.</param>
        public ClickerSliderEvent(Vector2[] vertices, double durationMs, Color4 colour)
        {
            this.vertices = vertices;
            this.durationMs = durationMs;
            this.colour = colour;

            float maxX = vertices.Max(v => v.X);
            float maxY = vertices.Max(v => v.Y);
            Size = new Vector2(maxX + RADIUS * follow_scale, maxY + RADIUS * follow_scale);

            InternalChildren = new Drawable[]
            {
                outerPath = new SmoothPath
                {
                    PathRadius = RADIUS,
                    Vertices = vertices,
                    Colour = Color4.White.Opacity(0.85f),
                },
                innerPath = new SmoothPath
                {
                    PathRadius = RADIUS - 5,
                    Vertices = vertices,
                    Colour = colour.Darken(0.55f),
                },
                follow = new CircularContainer
                {
                    Origin = Anchor.Centre,
                    Position = vertices[0],
                    Size = new Vector2(RADIUS * 2 * follow_scale),
                    Masking = true,
                    BorderThickness = 3,
                    BorderColour = Color4.White.Opacity(0.6f),
                    Alpha = 0,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
                ball = new CircularContainer
                {
                    Origin = Anchor.Centre,
                    Position = vertices[0],
                    Size = new Vector2(RADIUS * 2),
                    Masking = true,
                    BorderThickness = 5,
                    BorderColour = Color4.White,
                    Alpha = 0,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(colour.Lighten(0.5f), colour),
                    },
                },
                startCircle = new CircularContainer
                {
                    Origin = Anchor.Centre,
                    Position = vertices[0],
                    Size = new Vector2(RADIUS * 2),
                    Masking = true,
                    BorderThickness = 6,
                    BorderColour = Color4.White,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = colour.Opacity(0.8f),
                        Radius = 22,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(colour.Lighten(0.4f), colour.Darken(0.3f)),
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(22),
                            Icon = FontAwesome.Solid.HandPointer,
                            Colour = Color4.Black.Opacity(0.7f),
                        },
                    },
                },
                label = new OsuSpriteText
                {
                    Origin = Anchor.BottomCentre,
                    Position = vertices[0] + new Vector2(0, -RADIUS - 8),
                    Text = ClickerStrings.Pick("Slider! Hold and follow the ball", "Slider! Halten und dem Ball folgen"),
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Black),
                    Colour = colour.Lighten(0.4f),
                    Shadow = true,
                },
            };

            // the paths are laid out so that a vertex sits at its own local coordinates
            outerPath.OriginPosition = outerPath.PositionInBoundingBox(Vector2.Zero);
            innerPath.OriginPosition = innerPath.PositionInBoundingBox(Vector2.Zero);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager();
            this.FadeInFromZero(300);
            startCircle.Loop(s => s.ScaleTo(1.08f, 500, Easing.InOutSine).Then().ScaleTo(1f, 500, Easing.InOutSine));

            // an untouched slider goes away on its own
            Scheduler.AddDelayed(() =>
            {
                if (!Started)
                    this.FadeOut(500).Expire();
            }, idle_lifetime);
        }

        protected override void Update()
        {
            base.Update();

            if (!Started || Finished)
                return;

            double progress = Math.Clamp((Time.Current - startTime) / durationMs, 0, 1);
            Vector2 position = pointAt((float)progress);
            ball.Position = position;
            follow.Position = position;

            var mouse = inputManager?.CurrentState.Mouse;

            if (mouse != null)
            {
                float radius = follow.ScreenSpaceDrawQuad.Width / 2;
                bool inside = mouse.Buttons.HasAnyButtonPressed && Vector2.Distance(mouse.Position, ball.ScreenSpaceDrawQuad.Centre) <= radius;

                follow.BorderColour = inside ? colour.Lighten(0.6f) : Color4.White.Opacity(0.6f);
                if (inside)
                    trackedTime += Time.Elapsed;
            }

            totalTime += Time.Elapsed;

            if (progress >= 1)
                finish(totalTime > 0 ? trackedTime / totalTime : 0);
        }

        private Vector2 pointAt(float t)
        {
            float f = t * (vertices.Length - 1);
            int index = Math.Clamp((int)f, 0, vertices.Length - 2);
            return Vector2.Lerp(vertices[index], vertices[index + 1], f - index);
        }

        private void begin()
        {
            startTime = Time.Current;
            trackedTime = totalTime = 0;
            label.FadeOut(150);
            startCircle.ClearTransforms();
            startCircle.ScaleTo(1.3f, 150, Easing.OutQuint).FadeOut(150);
            ball.FadeIn(80);
            follow.FadeIn(80);
        }

        private void finish(double accuracy)
        {
            if (Finished)
                return;

            Finished = true;
            Completed?.Invoke(Math.Clamp(accuracy, 0, 1));

            ball.ScaleTo(1.5f, 250, Easing.OutQuint).FadeOut(250);
            follow.FadeOut(200);
            outerPath.FadeOut(350);
            innerPath.FadeOut(350);
            this.Delay(400).Expire();
        }

        /// <summary>
        /// Ends the slider with the given accuracy without any input (tests).
        /// </summary>
        internal void CompleteForTests(double accuracy)
        {
            if (Finished)
                return;

            if (!Started)
                begin();

            finish(accuracy);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => !Started && !Finished && startCircle.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (Started || Finished)
                return false;

            begin();
            return true;
        }
    }

    /// <summary>
    /// A round ability button in the play area: icon, cooldown ring and remaining time.
    /// </summary>
    internal partial class ClickerAbilityButton : OsuClickableContainer
    {
        public enum AbilityState
        {
            Locked,
            Ready,
            Active,
            Cooldown,
        }

        public const float SIZE = 72;

        public readonly ClickerAbility Ability;

        private readonly Color4 colour;
        private readonly CircularContainer disc;
        private readonly CircularProgress ring;
        private readonly SpriteIcon lockIcon;
        private readonly OsuSpriteText timeText;
        private AbilityState? state;
        private string lastTime = string.Empty;

        public ClickerAbilityButton(ClickerAbility ability, Color4 colour, Action action)
        {
            Ability = ability;
            this.colour = colour;
            Action = action;
            AutoSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Padding = new MarginPadding { Horizontal = 10, Bottom = 4 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(SIZE),
                            Children = new Drawable[]
                            {
                                disc = new CircularContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Masking = true,
                                    BorderThickness = 4,
                                    BorderColour = Color4.White,
                                    EdgeEffect = new EdgeEffectParameters
                                    {
                                        Type = EdgeEffectType.Glow,
                                        Colour = colour.Opacity(0),
                                        Radius = 22,
                                    },
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = ColourInfo.GradientVertical(colour.Lighten(0.3f), colour.Darken(0.4f)),
                                        },
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Size = new Vector2(30),
                                            Icon = ability.Icon,
                                            Shadow = true,
                                        },
                                    },
                                },
                                ring = new CircularProgress
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    InnerRadius = 0.14f,
                                    Colour = Color4.White.Opacity(0.8f),
                                    Alpha = 0,
                                },
                                lockIcon = new SpriteIcon
                                {
                                    Anchor = Anchor.BottomRight,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(18),
                                    Icon = FontAwesome.Solid.Lock,
                                    Colour = Color4.White,
                                    Shadow = true,
                                    Alpha = 0,
                                },
                            },
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = ClickerStrings.Text(ability.Name),
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                            Shadow = true,
                        },
                        timeText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                            Colour = Color4.White.Opacity(0.8f),
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Called every frame by the screen; only state changes start transforms.
        /// </summary>
        /// <param name="newState">Locked, ready, active or cooling down.</param>
        /// <param name="fraction">Remaining share of the active time or the cooldown (fills the ring).</param>
        /// <param name="time">Remaining time as text, or empty.</param>
        public void SetState(AbilityState newState, double fraction, string time)
        {
            if (time != lastTime)
            {
                lastTime = time;
                timeText.Text = time;
            }

            ring.Progress = Math.Clamp(fraction, 0, 1);

            if (newState == state)
                return;

            state = newState;
            Enabled.Value = newState == AbilityState.Ready;
            disc.ClearTransforms();

            string name = ClickerStrings.Pick(Ability.Name);
            string description = ClickerStrings.Pick(Ability.Description);

            switch (newState)
            {
                case AbilityState.Locked:
                    this.FadeTo(0.45f, 200);
                    lockIcon.FadeIn(200);
                    ring.FadeOut(200);
                    disc.ScaleTo(1f, 200);
                    disc.FadeEdgeEffectTo(colour.Opacity(0), 200);
                    TooltipText = ClickerStrings.Text("{0}: {1} Unlock it in the shop for {2} PP.", "{0}: {1} Im Shop für {2} PP freischalten.", name, description, ClickerFormat.Number(Ability.UnlockCost));
                    break;

                case AbilityState.Ready:
                    this.FadeTo(1f, 200);
                    lockIcon.FadeOut(200);
                    ring.FadeOut(200);
                    disc.FadeEdgeEffectTo(colour.Opacity(0.8f), 300);
                    disc.Loop(d => d.ScaleTo(1.08f, 500, Easing.InOutSine).Then().ScaleTo(1f, 500, Easing.InOutSine));
                    TooltipText = ClickerStrings.Text("{0}: {1} Click to activate.", "{0}: {1} Klicken zum Aktivieren.", name, description);
                    break;

                case AbilityState.Active:
                    this.FadeTo(1f, 200);
                    lockIcon.FadeOut(200);
                    ring.Colour = colour.Lighten(0.7f);
                    ring.FadeIn(100);
                    disc.ScaleTo(1.12f, 200, Easing.OutQuint);
                    disc.FadeEdgeEffectTo(colour.Opacity(1), 200);
                    TooltipText = ClickerStrings.Text("{0} is active!", "{0} ist aktiv!", name);
                    break;

                case AbilityState.Cooldown:
                    this.FadeTo(0.8f, 200);
                    lockIcon.FadeOut(200);
                    ring.Colour = Color4.White.Opacity(0.7f);
                    ring.FadeIn(200);
                    disc.ScaleTo(1f, 300, Easing.OutQuint);
                    disc.FadeEdgeEffectTo(colour.Opacity(0), 300);
                    TooltipText = ClickerStrings.Text("{0}: ready again soon.", "{0}: bald wieder bereit.", name);
                    break;
            }
        }
    }

    /// <summary>
    /// Small popover with the three volume sliders (master, music, effects) of the audio manager.
    /// </summary>
    internal partial class ClickerVolumePopover : VisibilityContainer
    {
        private const float popover_width = 250;

        public ClickerVolumePopover()
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 10;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(0.4f),
                Radius = 16,
            };
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4Extensions.FromHex("1b1420").Opacity(0.97f),
                },
                new FillFlowContainer
                {
                    Width = popover_width,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(14),
                    Spacing = new Vector2(0, 8),
                    Children = new[]
                    {
                        createRow(ClickerStrings.Text("Master", "Gesamt"), audio.Volume),
                        createRow(ClickerStrings.Text("Music", "Musik"), audio.VolumeTrack),
                        createRow(ClickerStrings.Text("Effects", "Effekte"), audio.VolumeSample),
                    },
                },
            };
        }

        private static Drawable createRow(LocalisableString label, BindableNumber<double> volume) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 2),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                },
                new RoundedSliderBar<double>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = volume.GetBoundCopy(),
                    DisplayAsPercentage = true,
                },
            },
        };

        protected override void PopIn() => this.FadeIn(150, Easing.OutQuint).ScaleTo(1f, 200, Easing.OutQuint);

        protected override void PopOut() => this.FadeOut(150, Easing.OutQuint).ScaleTo(0.95f, 150, Easing.OutQuint);
    }

    /// <summary>
    /// Hold-to-confirm dialog of the clicker (rebirth, respec).
    /// </summary>
    internal partial class ClickerConfirmDialog : DangerousActionDialog
    {
        public ClickerConfirmDialog(LocalisableString header, LocalisableString body, LocalisableString confirm, IconUsage icon, Action action)
        {
            HeaderText = header;
            BodyText = body;
            Icon = icon;
            DangerousAction = action;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogDangerousButton
                {
                    Text = confirm,
                    Action = () => DangerousAction?.Invoke(),
                },
                new PopupDialogCancelButton
                {
                    Text = ClickerStrings.Text("Cancel", "Abbrechen"),
                },
            };
        }
    }

    /// <summary>
    /// Small pill-shaped toggle button (buy amount, stances, expedition choices).
    /// </summary>
    internal partial class ClickerPillButton : OsuClickableContainer
    {
        public readonly LocalisableString Label;

        private readonly Color4 accent;
        private readonly Box background;
        private bool active;

        public ClickerPillButton(LocalisableString label, Color4 accent, Action action, IconUsage? icon = null)
        {
            Label = label;
            this.accent = accent;
            Action = action;

            AutoSizeAxes = Axes.X;
            Height = 30;
            Masking = true;
            CornerRadius = 15;

            var content = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Padding = new MarginPadding { Horizontal = 12 },
            };

            if (icon != null)
            {
                content.Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(13),
                    Icon = icon.Value,
                });
            }

            content.Add(new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = label,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
            });

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.4f,
                },
                content,
            };
        }

        public bool Active
        {
            get => active;
            set
            {
                active = value;
                background.FadeColour(value ? accent : Color4.Black, 150, Easing.OutQuint);
                background.FadeTo(value ? 0.9f : 0.4f, 150, Easing.OutQuint);
            }
        }

        public void SetEnabled(bool enabled)
        {
            Enabled.Value = enabled;
            this.FadeTo(enabled ? 1f : 0.4f, 150, Easing.OutQuint);
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

        public void Increment() => Set(Current + 1);

        /// <summary>
        /// Shows the engine's combo with the pop-out animation of a hit.
        /// </summary>
        public void Set(int value)
        {
            Current = value;
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
    /// One building, star, upgrade, synergy or ability in the shop. The texts are polled every frame through delegates.
    /// </summary>
    internal partial class ClickerShopRow : CompositeDrawable
    {
        public readonly LocalisableString Title;

        private readonly Func<string> detail;
        private readonly Func<string> buttonText;
        private readonly Func<bool> enabled;
        private readonly Func<string>? bigCount;
        private readonly Func<string>? hint;
        private readonly Box background;
        private readonly Box accentBar;
        private readonly OsuSpriteText detailText;
        private readonly OsuSpriteText countText;
        private readonly OsuSpriteText hintText;
        private readonly RoundedButton button;
        private string lastDetail = string.Empty;
        private string lastButtonText = string.Empty;
        private string lastCount = string.Empty;
        private string lastHint = string.Empty;
        private bool? lastEnabled;

        /// <param name="hint">Small text under the button ("ready in 0:42"); empty hides it.</param>
        public ClickerShopRow(Drawable icon, Color4 accent, LocalisableString title, LocalisableString description, Func<string> detail, Func<string> buttonText,
                              Func<bool> enabled, Action action, Func<string>? bigCount = null, Func<string>? hint = null)
        {
            Title = title;
            this.detail = detail;
            this.buttonText = buttonText;
            this.enabled = enabled;
            this.bigCount = bigCount;
            this.hint = hint;

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
                        new TruncatingSpriteText
                        {
                            Text = title,
                            Font = OsuFont.GetFont(size: 19, weight: FontWeight.Bold),
                            RelativeSizeAxes = Axes.X,
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
                    Y = hint != null ? -7 : 0,
                    Width = 130,
                    Action = action,
                    BackgroundColour = accent.Darken(0.45f),
                },
                hintText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    X = -14,
                    Y = -5,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = Color4.White.Opacity(0.6f),
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

            if (hint != null)
            {
                string newHint = hint();
                if (newHint != lastHint)
                    hintText.Text = lastHint = newHint;
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

        public ClickerTabButton(LocalisableString text, IconUsage icon, Color4 activeColour, Action action, float fontSize = 20, float iconSize = 16)
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
                    Spacing = new Vector2(fontSize >= 20 ? 8 : 5, 0),
                    Padding = new MarginPadding { Horizontal = 6 },
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = icon,
                            Size = new Vector2(iconSize),
                        },
                        label = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = OsuFont.GetFont(size: fontSize, weight: FontWeight.Bold),
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

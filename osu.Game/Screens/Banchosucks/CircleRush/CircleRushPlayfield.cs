// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// The field circles appear on. Coordinates are <see cref="CircleRushRules.PLAYFIELD_WIDTH"/> × <see cref="CircleRushRules.PLAYFIELD_HEIGHT"/>;
    /// the parent scales the field to fit. Clicks (mouse, touch, Z or X at the cursor) go to the oldest circle under the
    /// cursor, anything else is a miss. Expiries are recorded at the exact deadline, before any click of the same frame,
    /// so the event log replays identically on the server.
    /// </summary>
    internal partial class CircleRushPlayfield : Container
    {
        /// <summary>Points of the hit and the combo after it.</summary>
        public event Action<int, int>? Hit;

        public event Action? Missed;

        /// <summary>The run ended (clock or lives); <see cref="Run"/> holds the result.</summary>
        public event Action? Finished;

        public CircleRushRun? Run { get; private set; }

        public bool Running => Run != null && !Run.Ended && !double.IsNaN(startTime) && Time.Current >= startTime;

        /// <summary>Run time in milliseconds, 0 before the start.</summary>
        public int RunTime => Running ? Math.Max(0, (int)Math.Floor(Time.Current - startTime)) : 0;

        private const float flashlight_radius = 150;
        private const float flashlight_edge = 50;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly Container circleLayer;
        private readonly Container effectLayer;
        private readonly Box background;
        private readonly Circle light;
        private readonly List<CircleRushCircle> active = new List<CircleRushCircle>();

        private Random positions = new Random();
        private Vector2 lastPosition = new Vector2(CircleRushRules.PLAYFIELD_WIDTH / 2f, CircleRushRules.PLAYFIELD_HEIGHT / 2f);
        private double startTime = double.NaN;
        private int nextIndex;
        private Sample? hitSample;
        private Sample? missSample;

        public CircleRushPlayfield()
        {
            Size = new Vector2(CircleRushRules.PLAYFIELD_WIDTH, CircleRushRules.PLAYFIELD_HEIGHT);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Masking = true;
            CornerRadius = 12;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4Extensions.FromHex("12141b"),
                },
                light = new Circle
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(flashlight_radius * 2 + flashlight_edge),
                    Colour = Color4.White.Opacity(0.11f),
                    Alpha = 0,
                },
                circleLayer = new Container { RelativeSizeAxes = Axes.Both },
                effectLayer = new Container { RelativeSizeAxes = Axes.Both },
            };
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            hitSample = audio.Samples.Get(@"Gameplay/normal-hitnormal");
            missSample = audio.Samples.Get(@"Gameplay/combobreak");
        }

        /// <summary>Clears the field and starts <paramref name="run"/> at the given clock time (now or a little later for the countdown).</summary>
        public void Begin(CircleRushRun run, double startAtClockTime)
        {
            Clear();
            Run = run;
            positions = new Random((int)(run.Seed & 0x7fffffff));
            startTime = startAtClockTime;
            nextIndex = 0;
            light.Alpha = run.Mods.HasFlag(CircleRushMods.Flashlight) ? 1 : 0;
            background.Colour = run.Mods.HasFlag(CircleRushMods.Flashlight) ? Color4Extensions.FromHex("07080c") : Color4Extensions.FromHex("12141b");
        }

        /// <summary>Removes every circle; a running run is aborted and not submitted.</summary>
        public void Clear()
        {
            Run?.Abort(RunTime);
            startTime = double.NaN;
            Run = null;
            active.Clear();
            circleLayer.Clear();
            effectLayer.Clear();
        }

        protected override void Update()
        {
            base.Update();

            if (!Running)
                return;

            advanceTo(RunTime);
            updateFlashlight();
        }

        private void advanceTo(int time)
        {
            var run = Run;
            if (run == null || run.Ended) return;

            // circles are spawned in order and their deadlines grow with the index, so the oldest expires first
            while (active.Count > 0 && time >= run.Schedule.Deadline(active[0].Index))
            {
                var circle = active[0];
                active.RemoveAt(0);
                run.Expire(circle.Index);
                circle.Miss();
                missSample?.Play();
                Missed?.Invoke();

                if (run.Ended)
                {
                    finish();
                    return;
                }
            }

            while (run.Schedule.SpawnTime(nextIndex) <= time)
            {
                if (run.Mode == CircleRushMode.Minute && run.Schedule.SpawnTime(nextIndex) >= CircleRushRules.MINUTE_DURATION)
                    break;

                spawn(nextIndex);
                nextIndex++;
            }

            if (run.Mode == CircleRushMode.Minute && time >= CircleRushRules.MINUTE_DURATION)
            {
                run.FinishMinute();
                finish();
            }
        }

        private void spawn(int index)
        {
            var run = Run!;
            int radius = CircleRushRules.Radius(run.Difficulty, run.Mods);
            var position = nextPosition(radius);

            var circle = new CircleRushCircle(index, radius, run.Schedule.Lifetime(index), colours.Pink, run.Mods.HasFlag(CircleRushMods.Hidden))
            {
                Position = position,
            };

            active.Add(circle);
            circleLayer.Add(circle);
        }

        private Vector2 nextPosition(int radius)
        {
            float margin = radius + 8;
            float minDistance = radius * 2.5f;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                var candidate = new Vector2(
                    margin + (float)positions.NextDouble() * (CircleRushRules.PLAYFIELD_WIDTH - 2 * margin),
                    margin + (float)positions.NextDouble() * (CircleRushRules.PLAYFIELD_HEIGHT - 2 * margin));

                bool clear = Vector2.Distance(candidate, lastPosition) >= minDistance;

                foreach (var other in active)
                {
                    if (Vector2.Distance(candidate, other.Position) < minDistance)
                        clear = false;
                }

                if (clear || attempt == 11)
                {
                    lastPosition = candidate;
                    return candidate;
                }
            }

            return lastPosition;
        }

        private void finish()
        {
            foreach (var circle in active)
                circle.FadeOut(200).Expire();

            active.Clear();
            Finished?.Invoke();
        }

        private bool click(Vector2 position)
        {
            if (!Running) return false;

            int time = RunTime;
            advanceTo(time);

            var run = Run;
            if (run == null || run.Ended) return true;

            CircleRushCircle? target = null;

            foreach (var circle in active)
            {
                if (Vector2.Distance(circle.Position, position) <= circle.Radius)
                {
                    target = circle;
                    break;
                }
            }

            if (target != null)
            {
                int points = run.HitScoreAt(target.Index, time);

                if (run.Hit(target.Index, time))
                {
                    active.Remove(target);
                    target.Hit();
                    hitSample?.Play();
                    effectLayer.Add(new ScorePopup(target.Position, points, run.Combo, colours));
                    Hit?.Invoke(points, run.Combo);
                }

                return true;
            }

            run.EmptyClick(time);
            effectLayer.Add(new MissMarker(position, colours.Red));
            missSample?.Play();
            Missed?.Invoke();

            if (run.Ended)
                finish();

            return true;
        }

        private void updateFlashlight()
        {
            var run = Run;
            if (run == null || !run.Mods.HasFlag(CircleRushMods.Flashlight)) return;

            var mouse = GetContainingInputManager()?.CurrentState.Mouse.Position;
            if (mouse == null) return;

            var local = ToLocalSpace(mouse.Value);
            light.Position = local;

            foreach (var circle in active)
            {
                float distance = Vector2.Distance(circle.Position, local) - circle.Radius;
                circle.Visibility = Math.Clamp((flashlight_radius + flashlight_edge - distance) / flashlight_edge, 0, 1);
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!Running) return false;

            return click(ToLocalSpace(e.ScreenSpaceMouseDownPosition));
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            if (!Running) return false;

            return click(ToLocalSpace(e.ScreenSpaceTouch.Position));
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!Running || e.Repeat) return false;

            if (e.Key != Key.Z && e.Key != Key.X)
                return false;

            var mouse = GetContainingInputManager()?.CurrentState.Mouse.Position;
            if (mouse == null) return false;

            return click(ToLocalSpace(mouse.Value));
        }

        /// <summary>"+183" drifting up from a hit circle; every 25th combo is called out.</summary>
        private partial class ScorePopup : CompositeDrawable
        {
            public ScorePopup(Vector2 position, int points, int combo, OsuColour colours)
            {
                Position = position;
                Origin = Anchor.Centre;
                AutoSizeAxes = Axes.Both;
                bool milestone = combo > 0 && combo % 25 == 0;

                InternalChild = new OsuSpriteText
                {
                    Text = milestone ? $"+{points}  {combo}x!" : $"+{points}",
                    Font = OsuFont.GetFont(size: milestone ? 30 : 22, weight: FontWeight.Bold),
                    Colour = milestone ? colours.Yellow : Color4.White,
                    Shadow = true,
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                this.MoveToOffset(new Vector2(0, -46), 520, Easing.OutQuint).FadeOut(520, Easing.In).Expire();
            }
        }

        private partial class MissMarker : CircularContainer
        {
            public MissMarker(Vector2 position, Color4 colour)
            {
                Position = position;
                Origin = Anchor.Centre;
                Size = new Vector2(24);
                Masking = true;
                BorderThickness = 3;
                BorderColour = colour;
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                this.ScaleTo(2.2f, 300, Easing.OutQuint).FadeOut(300).Expire();
            }
        }
    }

    /// <summary>
    /// One circle on the field: body, approach ring that closes over the circle's lifetime, hidden-mod fade.
    /// </summary>
    internal partial class CircleRushCircle : CompositeDrawable
    {
        public readonly int Index;
        public readonly float Radius;

        private readonly int lifetime;
        private readonly bool hidden;
        private readonly Circle body;
        private readonly CircularContainer approach;
        private readonly CircularContainer ring;
        private readonly Color4 colour;

        private float visibility = 1;

        /// <summary>0..1, set by the flashlight mod depending on the distance to the cursor.</summary>
        public float Visibility
        {
            set
            {
                visibility = value;
                Alpha = visibility;
            }
        }

        public CircleRushCircle(int index, int radius, int lifetime, Color4 colour, bool hidden)
        {
            Index = index;
            Radius = radius;
            this.lifetime = lifetime;
            this.hidden = hidden;
            this.colour = colour;

            Origin = Anchor.Centre;
            Size = new Vector2(radius * 2);

            InternalChildren = new Drawable[]
            {
                approach = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 3,
                    BorderColour = colour.Lighten(0.4f),
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
                body = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(colour.Lighten(0.15f), colour.Darken(0.35f)),
                },
                ring = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = radius * 0.16f,
                    BorderColour = Color4.White,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            body.FadeInFromZero(Math.Min(80, lifetime / 4));
            ring.FadeInFromZero(Math.Min(80, lifetime / 4));

            if (hidden)
            {
                approach.Alpha = 0;
                // like osu!'s hidden: the circle fades away well before its time is up
                body.Delay(lifetime * 0.25).FadeOut(lifetime * 0.45);
                ring.Delay(lifetime * 0.25).FadeOut(lifetime * 0.45);
            }
            else
            {
                approach.ScaleTo(3.2f).ScaleTo(1, lifetime);
            }
        }

        public void Hit()
        {
            ClearTransforms();
            approach.FadeOut(80);
            body.FadeColour(colour.Lighten(0.6f), 60);
            this.FadeTo(Math.Max(visibility, 0.6f)).ScaleTo(1.45f, 220, Easing.OutQuint).FadeOut(220).Expire();
        }

        public void Miss()
        {
            ClearTransforms();
            approach.FadeOut(60);
            body.FadeColour(Color4Extensions.FromHex("c0392b"), 80);
            this.FadeTo(Math.Max(visibility, 0.5f)).ScaleTo(0.75f, 260, Easing.InQuint).FadeOut(260).Expire();
        }
    }
}

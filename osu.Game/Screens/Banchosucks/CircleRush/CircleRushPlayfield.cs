// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// The osu! ruleset's actions, with the same values, so the player's own osu! key bindings
    /// (left / right button, Z and X by default, mouse buttons) drive Circle Rush.
    /// </summary>
    public enum CircleRushAction
    {
        LeftButton = 0,
        RightButton = 1,
        Smoke = 2,
    }

    /// <summary>
    /// The field circles appear on. Coordinates are <see cref="CircleRushRules.PLAYFIELD_WIDTH"/> × <see cref="CircleRushRules.PLAYFIELD_HEIGHT"/>;
    /// the parent scales the field to fit. Circles come in the groups the schedule dictates: a single anywhere, a burst of
    /// two as a jump, a stream of three to five along a gently bent line, numbered and coloured with the skin's combo colours.
    /// Input is the osu! ruleset's left / right button (the player's own bindings, mouse buttons included, honouring
    /// "disable mouse buttons during gameplay") and touch; a press goes to the oldest circle under the cursor, anything
    /// else is a miss. Expiries are recorded at the exact deadline, before any click of the same frame, so the event log
    /// replays identically on the server. Only timing is verified there; where a circle sits is the client's business.
    /// </summary>
    internal partial class CircleRushPlayfield : Container
    {
        /// <summary>Points of the hit, the combo after it and how fast it came.</summary>
        public event Action<int, int, CircleRushJudgement>? Hit;

        public event Action? Missed;

        /// <summary>The run ended (clock or lives); <see cref="Run"/> holds the result.</summary>
        public event Action? Finished;

        /// <summary>An osu! button went down (true) or up (false), run or not; the skin cursor grows while one is held.</summary>
        public event Action<bool>? ButtonStateChanged;

        public CircleRushRun? Run { get; private set; }

        public bool Running => Run != null && !Run.Ended && !double.IsNaN(startTime) && Time.Current >= startTime;

        /// <summary>Run time in milliseconds, 0 before the start.</summary>
        public int RunTime => Running ? Math.Max(0, (int)Math.Floor(Time.Current - startTime)) : 0;

        private const float flashlight_radius = 150;
        private const float flashlight_edge = 50;

        /// <summary>Endless mode after the ramp: circles shrink to this share of their radius over <see cref="endless_shrink_ms"/>.</summary>
        private const float endless_min_scale = 0.7f;
        private const float endless_shrink_ms = 150_000;

        private static readonly Vector2 centre = new Vector2(CircleRushRules.PLAYFIELD_WIDTH / 2f, CircleRushRules.PLAYFIELD_HEIGHT / 2f);

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly Container circleLayer;
        private readonly Container effectLayer;
        private readonly Box background;
        private readonly Circle light;
        private readonly List<CircleRushCircle> active = new List<CircleRushCircle>();

        private OsuKeyBindings? bindings;
        private IReadOnlyList<Color4>? comboColours;

        private Random positions = new Random();
        private Vector2 lastPosition = centre;
        private Vector2 groupOrigin = centre;
        private Vector2 groupStep;
        private Vector2 groupBend;
        private int groupOrdinal = -1;
        private Color4 groupColour;
        private double startTime = double.NaN;
        private int nextIndex;
        private int hitCount;

        private ISample? hitNormal;
        private ISample? hitClap;
        private ISample? hitFinish;
        private ISample? comboBreak;
        private ISample? sectionPass;

        public CircleRushPlayfield()
        {
            Size = new Vector2(CircleRushRules.PLAYFIELD_WIDTH, CircleRushRules.PLAYFIELD_HEIGHT);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Masking = true;
            CornerRadius = 12;

            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4Extensions.FromHex("12141b"),
            };
            light = new Circle
            {
                Origin = Anchor.Centre,
                Size = new Vector2(flashlight_radius * 2 + flashlight_edge),
                Colour = Color4.White.Opacity(0.11f),
                Alpha = 0,
            };
            circleLayer = new Container { RelativeSizeAxes = Axes.Both };
            effectLayer = new Container { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets, ISkinSource skin)
        {
            // the skin's hitsounds and combo colours, falling back to the default skin like gameplay does
            hitNormal = skin.GetSample(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));
            hitClap = skin.GetSample(new HitSampleInfo(HitSampleInfo.HIT_CLAP));
            hitFinish = skin.GetSample(new HitSampleInfo(HitSampleInfo.HIT_FINISH));
            comboBreak = skin.GetSample(new SampleInfo(@"Gameplay/combobreak"));
            sectionPass = skin.GetSample(new SampleInfo(@"Gameplay/sectionpass"));
            comboColours = skin.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;

            var layers = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[] { background, light, circleLayer, effectLayer, new ActionReceiver(onAction, onRelease) },
            };

            // the osu! ruleset's bindings from the player's database; without the ruleset (never in this client) plain Z / X below
            var osuRuleset = rulesets.GetRuleset("osu");
            InternalChild = osuRuleset != null ? bindings = new OsuKeyBindings(osuRuleset) { Child = layers } : layers;
        }

        /// <summary>Clears the field and starts <paramref name="run"/> at the given clock time (now or a little later for the countdown).</summary>
        public void Begin(CircleRushRun run, double startAtClockTime)
        {
            Stop();
            Run = run;
            positions = new Random((int)(run.Seed & 0x7fffffff));
            startTime = startAtClockTime;
            nextIndex = 0;
            hitCount = 0;
            groupOrdinal = -1;
            lastPosition = centre;
            light.Alpha = run.Mods.HasFlag(CircleRushMods.Flashlight) ? 1 : 0;
            background.Colour = run.Mods.HasFlag(CircleRushMods.Flashlight) ? Color4Extensions.FromHex("07080c") : Color4Extensions.FromHex("12141b");
        }

        /// <summary>Removes every circle; a running run is aborted and not submitted.</summary>
        public void Stop()
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
                comboBreak?.Play();
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
            var info = run.Schedule.Circle(index);
            float radius = visualRadius(run, info.Spawn);

            if (info.GroupIndex == 0)
                startGroup(info.GroupSize, radius);

            var circle = new CircleRushCircle(index, radius, run.Schedule.Lifetime(index), groupColour, run.Mods.HasFlag(CircleRushMods.Hidden), info.GroupIndex + 1)
            {
                Position = positionInGroup(info.GroupIndex, info.GroupSize, radius),
            };

            active.Add(circle);
            circleLayer.Add(circle);
        }

        /// <summary>The rules' radius; in endless mode the circles also get smaller once the ramp is over.</summary>
        private static float visualRadius(CircleRushRun run, int spawnTime)
        {
            float radius = CircleRushRules.Radius(run.Difficulty, run.Mods);

            if (run.Mode != CircleRushMode.Endless || spawnTime <= CircleRushRules.RAMP_ENDLESS)
                return radius;

            float scale = Math.Max(endless_min_scale, 1 - (spawnTime - CircleRushRules.RAMP_ENDLESS) / endless_shrink_ms);
            return radius * scale;
        }

        /// <summary>Picks colour and placement of a new group: a free spot for a single, a line that fits the field for a burst or stream.</summary>
        private void startGroup(int size, float radius)
        {
            groupOrdinal++;
            IReadOnlyList<Color4> palette = comboColours is { Count: > 0 } ? comboColours : new[] { colours.Pink, colours.Blue, colours.Yellow, colours.Green };
            groupColour = palette[groupOrdinal % palette.Count];

            float margin = radius + 8;
            float clearance = radius * 2.3f;
            groupStep = Vector2.Zero;
            groupBend = Vector2.Zero;

            if (size == 1)
            {
                groupOrigin = freePosition(margin, clearance, radius * 2.5f);
                lastPosition = groupOrigin;
                return;
            }

            // a burst is a jump, a stream sits tight like in osu!
            float spacing = size == 2 ? radius * 3.4f : radius * 2.4f;
            var bestStart = centre;
            var bestDirection = new Vector2(1, 0);
            bool found = false;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                double angle = positions.NextDouble() * Math.PI * 2;
                var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                var offset = direction * (spacing * (size - 1));

                float minX = margin + Math.Max(0, -offset.X), maxX = CircleRushRules.PLAYFIELD_WIDTH - margin - Math.Max(0, offset.X);
                float minY = margin + Math.Max(0, -offset.Y), maxY = CircleRushRules.PLAYFIELD_HEIGHT - margin - Math.Max(0, offset.Y);

                if (maxX < minX || maxY < minY)
                {
                    // the line does not fit this way round: tighter spacing, another direction
                    spacing = Math.Max(radius * 2.1f, spacing * 0.85f);
                    continue;
                }

                var start = new Vector2(minX + (float)positions.NextDouble() * (maxX - minX), minY + (float)positions.NextDouble() * (maxY - minY));
                bool clear = Vector2.Distance(start, lastPosition) >= radius * 2.5f;

                for (int k = 0; k < size && clear; k++)
                {
                    var point = start + direction * (spacing * k);

                    foreach (var other in active)
                    {
                        if (Vector2.Distance(point, other.Position) < clearance)
                        {
                            clear = false;
                            break;
                        }
                    }
                }

                bestStart = start;
                bestDirection = direction;
                found = true;

                if (clear)
                    break;
            }

            if (!found)
            {
                // cannot happen with the field this size, but a stream in the centre beats an exception
                bestStart = centre - bestDirection * (spacing * (size - 1) / 2);
            }

            groupOrigin = bestStart;
            groupStep = bestDirection * spacing;

            if (size >= 3)
            {
                // streams bend a little, to one side or the other
                var perpendicular = new Vector2(-bestDirection.Y, bestDirection.X);
                groupBend = perpendicular * (radius * 0.9f) * (positions.Next(2) == 0 ? 1 : -1);
            }

            lastPosition = positionInGroup(size - 1, size, radius);
        }

        private Vector2 positionInGroup(int index, int size, float radius)
        {
            if (size == 1 || index == 0)
                return groupOrigin;

            var position = groupOrigin + groupStep * index + groupBend * (float)Math.Sin(Math.PI * index / (size - 1));
            float margin = radius + 8;

            return new Vector2(
                Math.Clamp(position.X, margin, CircleRushRules.PLAYFIELD_WIDTH - margin),
                Math.Clamp(position.Y, margin, CircleRushRules.PLAYFIELD_HEIGHT - margin));
        }

        private Vector2 freePosition(float margin, float clearance, float jump)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var candidate = new Vector2(
                    margin + (float)positions.NextDouble() * (CircleRushRules.PLAYFIELD_WIDTH - 2 * margin),
                    margin + (float)positions.NextDouble() * (CircleRushRules.PLAYFIELD_HEIGHT - 2 * margin));

                bool clear = Vector2.Distance(candidate, lastPosition) >= jump;

                foreach (var other in active)
                {
                    if (Vector2.Distance(candidate, other.Position) < clearance)
                        clear = false;
                }

                if (clear || attempt == 11)
                    return candidate;
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
                var judgement = run.Hit(target.Index, time);

                if (judgement != null)
                {
                    active.Remove(target);
                    target.Hit();
                    playHit(run, target.Index, run.Combo);
                    effectLayer.Add(new ScorePopup(target.Position, points, run.Combo, judgement.Value));
                    Hit?.Invoke(points, run.Combo, judgement.Value);
                }

                return true;
            }

            run.EmptyClick(time);
            effectLayer.Add(new MissMarker(position, colours.Red));
            comboBreak?.Play();
            Missed?.Invoke();

            if (run.Ended)
                finish();

            return true;
        }

        /// <summary>Hitnormal and clap alternate, the last circle of a stream adds a finish, every 25th combo a section pass.</summary>
        private void playHit(CircleRushRun run, int index, int combo)
        {
            var info = run.Schedule.Circle(index);

            (hitCount++ % 2 == 0 ? hitNormal : hitClap)?.Play();

            if (info.GroupSize >= 3 && info.GroupIndex == info.GroupSize - 1)
                hitFinish?.Play();

            if (combo > 0 && combo % 25 == 0)
                sectionPass?.Play();
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

        // ---------------------------------------------------------------- input

        /// <summary>The osu! left or right button was pressed (key or mouse, as bound in the osu! settings): a click at the cursor.</summary>
        private bool onAction(KeyBindingPressEvent<CircleRushAction> e)
        {
            if (e.Repeat) return false;
            if (e.Action != CircleRushAction.LeftButton && e.Action != CircleRushAction.RightButton) return false;

            ButtonStateChanged?.Invoke(true);

            if (!Running) return false;

            return clickAtCursor();
        }

        private void onRelease(KeyBindingReleaseEvent<CircleRushAction> e)
        {
            if (e.Action == CircleRushAction.LeftButton || e.Action == CircleRushAction.RightButton)
                ButtonStateChanged?.Invoke(false);
        }

        private bool clickAtCursor()
        {
            var mouse = GetContainingInputManager()?.CurrentState.Mouse.Position;
            if (mouse == null) return false;

            return click(ToLocalSpace(mouse.Value));
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            if (!Running) return false;

            return click(ToLocalSpace(e.ScreenSpaceTouch.Position));
        }

        // without the osu! ruleset's bindings (never the case in this client): plain mouse and Z / X
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (bindings != null || !Running) return false;

            return click(ToLocalSpace(e.ScreenSpaceMouseDownPosition));
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (bindings != null || !Running || e.Repeat) return false;
            if (e.Key != Key.Z && e.Key != Key.X) return false;

            return clickAtCursor();
        }

        /// <summary>
        /// The osu! ruleset's key bindings from the player's database (variant 0), exactly what osu!standard gameplay uses,
        /// including "disable mouse buttons during gameplay".
        /// </summary>
        private partial class OsuKeyBindings : DatabasedKeyBindingContainer<CircleRushAction>
        {
            private Bindable<bool>? mouseDisabled;

            public OsuKeyBindings(RulesetInfo ruleset)
                : base(ruleset, 0, SimultaneousBindingMode.Unique)
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader(true)]
            private void load(OsuConfigManager? config)
            {
                mouseDisabled = config?.GetBindable<bool>(OsuSetting.MouseDisableButtons);
            }

            protected override bool Handle(UIEvent e)
            {
                // like RulesetInputManager: a disabled mouse button is swallowed, not a miss
                if (e is MouseDownEvent && mouseDisabled?.Value == true)
                    return true;

                return base.Handle(e);
            }
        }

        /// <summary>Receives the bound actions inside <see cref="OsuKeyBindings"/> and hands them to the field.</summary>
        private partial class ActionReceiver : CompositeDrawable, IKeyBindingHandler<CircleRushAction>
        {
            private readonly Func<KeyBindingPressEvent<CircleRushAction>, bool> pressed;
            private readonly Action<KeyBindingReleaseEvent<CircleRushAction>> released;

            public ActionReceiver(Func<KeyBindingPressEvent<CircleRushAction>, bool> pressed, Action<KeyBindingReleaseEvent<CircleRushAction>> released)
            {
                this.pressed = pressed;
                this.released = released;
                RelativeSizeAxes = Axes.Both;
            }

            public bool OnPressed(KeyBindingPressEvent<CircleRushAction> e) => pressed(e);

            public void OnReleased(KeyBindingReleaseEvent<CircleRushAction> e) => released(e);
        }

        /// <summary>"+183" and the judgement drifting up from a hit circle, coloured like osu!'s 300 / 100 / 50; every 25th combo is called out.</summary>
        private partial class ScorePopup : CompositeDrawable
        {
            public ScorePopup(Vector2 position, int points, int combo, CircleRushJudgement judgement)
            {
                Position = position;
                Origin = Anchor.Centre;
                AutoSizeAxes = Axes.Both;
                bool milestone = combo > 0 && combo % 25 == 0;

                Color4 colour = judgement switch
                {
                    CircleRushJudgement.Perfect => Color4Extensions.FromHex("66ccff"),
                    CircleRushJudgement.Good => Color4Extensions.FromHex("88e55a"),
                    _ => Color4Extensions.FromHex("ffcc33"),
                };

                var label = judgement switch
                {
                    CircleRushJudgement.Perfect => ClickerStrings.Text("Perfect", "Perfekt"),
                    CircleRushJudgement.Good => ClickerStrings.Text("Good", "Gut"),
                    _ => ClickerStrings.Text("Late", "Spät"),
                };

                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, -3),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = milestone ? $"+{points}  {combo}x!" : $"+{points}",
                            Font = OsuFont.GetFont(size: milestone ? 30 : 22, weight: FontWeight.Bold),
                            Colour = milestone ? Color4.White : colour,
                            Shadow = true,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = label,
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                            Colour = colour,
                            Shadow = true,
                        },
                    },
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
    /// One circle on the field, drawn with the current skin like an osu! hit circle: <c>hitcircle</c> tinted in the group's
    /// combo colour, <c>hitcircleoverlay</c>, the number in the skin's hit circle font. Skins without those textures (Argon,
    /// Triangles) get a plain disc with a white rim. No approach ring on purpose: there is no timing window to wait for,
    /// the faster the hit the more it scores.
    /// </summary>
    internal partial class CircleRushCircle : CompositeDrawable
    {
        public readonly int Index;
        public readonly float Radius;

        /// <summary>osu!'s hit circle textures are drawn for this size and scaled from there.</summary>
        private const float legacy_size = 128;

        private readonly int lifetime;
        private readonly bool hidden;
        private readonly int numberInGroup;
        private readonly Color4 colour;
        private readonly Container pieces;

        private Drawable tinted = null!;

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

        public CircleRushCircle(int index, float radius, int lifetime, Color4 colour, bool hidden, int numberInGroup)
        {
            Index = index;
            Radius = radius;
            this.lifetime = lifetime;
            this.hidden = hidden;
            this.colour = colour;
            this.numberInGroup = numberInGroup;

            Origin = Anchor.Centre;
            Size = new Vector2(radius * 2);

            InternalChild = pieces = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(legacy_size),
                Scale = new Vector2(radius * 2 / legacy_size),
            };
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var circleTexture = skin.GetTexture("hitcircle");

            if (circleTexture != null)
            {
                bool overlayAboveNumber = skin.GetConfig<CircleRushSkinSetting, bool>(CircleRushSkinSetting.HitCircleOverlayAboveNumber)?.Value ?? true;

                var overlay = new Sprite
                {
                    Texture = skin.GetTexture("hitcircleoverlay"),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
                var number = new LegacySpriteText(LegacyFont.HitCircle)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(0.8f),
                    Text = numberInGroup.ToString(),
                };

                pieces.Children = new Drawable[]
                {
                    tinted = new Sprite
                    {
                        Texture = circleTexture,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = colour,
                    },
                    overlayAboveNumber ? (Drawable)number : overlay,
                    overlayAboveNumber ? overlay : (Drawable)number,
                };
            }
            else
            {
                pieces.Children = new Drawable[]
                {
                    tinted = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(colour.Lighten(0.15f), colour.Darken(0.35f)),
                    },
                    new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = legacy_size * 0.07f,
                        BorderColour = Color4.White,
                        Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = numberInGroup.ToString(),
                        Font = OsuFont.Numeric.With(size: legacy_size * 0.42f),
                        Colour = Color4.White,
                        Shadow = true,
                    },
                };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            int fade = Math.Min(80, lifetime / 4);
            this.ScaleTo(0.6f).ScaleTo(1, fade, Easing.OutQuint);
            pieces.FadeInFromZero(fade);

            if (hidden)
            {
                // like osu!'s hidden: the circle fades away well before its time is up
                pieces.Delay(lifetime * 0.25).FadeOut(lifetime * 0.45);
            }
        }

        public void Hit()
        {
            ClearTransforms();
            pieces.ClearTransforms();
            tinted.FadeColour(colour.Lighten(0.6f), 60);
            this.FadeTo(Math.Max(visibility, 0.6f)).ScaleTo(1.45f, 220, Easing.OutQuint).FadeOut(220).Expire();
        }

        public void Miss()
        {
            ClearTransforms();
            pieces.ClearTransforms();
            tinted.FadeColour(Color4Extensions.FromHex("c0392b"), 80);
            this.FadeTo(Math.Max(visibility, 0.5f)).ScaleTo(0.75f, 260, Easing.InQuint).FadeOut(260).Expire();
        }
    }
}

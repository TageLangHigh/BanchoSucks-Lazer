// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: the small daily goal line on the main menu: today's progress, a thin bar and the streak.
    /// Invisible (and not clickable) while the player has no goal set. A click opens the personal statistics.
    /// </summary>
    public partial class BanchosucksDailyGoalDisplay : OsuClickableContainer
    {
        private const float bar_width = 170;
        private const float idle_alpha = 0.75f;

        // absent in plain test scenes; the game always provides it
        [Resolved(CanBeNull = true)]
        private BanchosucksActivityLog? log { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private OsuSpriteText text = null!;
        private OsuSpriteText streakText = null!;
        private Box bar = null!;

        private DateOnly shownDay;

        /// <summary>
        /// Whether a goal is set and the line is therefore shown.
        /// </summary>
        public bool Shown { get; private set; }

        /// <summary>
        /// The progress currently on screen (tests).
        /// </summary>
        public BanchosucksDailyGoalProgress ShownProgress { get; private set; }

        public BanchosucksDailyGoalDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            TooltipText = ClickerStrings.Text("Your daily goal. Click for your personal statistics.", "Dein Tagesziel. Klick für deine eigene Statistik.");

            Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            text = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                            streakText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 14),
                                Shadow = true,
                                Colour = colours.Yellow,
                            },
                        },
                    },
                    new Container
                    {
                        Size = new Vector2(bar_width, 3),
                        Masking = true,
                        CornerRadius = 1.5f,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White.Opacity(0.25f),
                            },
                            bar = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Width = 0,
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (log == null)
                return;

            log.Changed += refresh;
            refresh();

            // a new day starts at local midnight, also while the menu is open
            Scheduler.AddDelayed(() =>
            {
                if (log.Today != shownDay)
                    refresh();
            }, 1000, true);
        }

        private void refresh()
        {
            if (log == null)
                return;

            shownDay = log.Today;
            var progress = ShownProgress = log.TodayProgress;

            Shown = progress.Enabled;
            Enabled.Value = Shown;
            this.FadeTo(Shown ? (IsHovered ? 1 : idle_alpha) : 0, 300, Easing.OutQuint);

            if (!Shown)
                return;

            text.Text = describe(progress);
            text.Colour = progress.Met ? colours.GreenLight : Color4.White;

            int streak = log.Streak;
            streakText.Text = streak switch
            {
                0 => default,
                1 => ClickerStrings.Text("streak: 1 day", "Serie: 1 Tag"),
                _ => ClickerStrings.Text("streak: {0} days", "Serie: {0} Tage", streak),
            };

            bar.Colour = progress.Met ? colours.GreenLight : colours.Blue;
            bar.ResizeWidthTo((float)progress.Fraction, 400, Easing.OutQuint);
        }

        private static LocalisableString describe(BanchosucksDailyGoalProgress progress)
        {
            int minutes = (int)Math.Floor(progress.MinutesPlayed);

            if (progress.Met)
            {
                return ClickerStrings.Text("Daily goal reached: {0} min, {1} maps", "Tagesziel erreicht: {0} min, {1} Maps", minutes, progress.MapsPassed);
            }

            if (progress.GoalMinutes > 0 && progress.GoalMaps > 0)
            {
                return ClickerStrings.Text("Daily goal: {0}/{1} min · {2}/{3} maps", "Tagesziel: {0}/{1} min · {2}/{3} Maps",
                    minutes, progress.GoalMinutes, progress.MapsPassed, progress.GoalMaps);
            }

            return progress.GoalMinutes > 0
                ? ClickerStrings.Text("Daily goal: {0}/{1} min", "Tagesziel: {0}/{1} min", minutes, progress.GoalMinutes)
                : ClickerStrings.Text("Daily goal: {0}/{1} maps", "Tagesziel: {0}/{1} Maps", progress.MapsPassed, progress.GoalMaps);
        }

        // invisible means not there: no hover sound, no tooltip, no click
        public override bool HandlePositionalInput => Shown && base.HandlePositionalInput;

        protected override bool OnHover(HoverEvent e)
        {
            if (Shown)
                this.FadeTo(1, 150, Easing.OutQuint);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (Shown)
                this.FadeTo(idle_alpha, 300, Easing.OutQuint);

            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (log != null)
                log.Changed -= refresh;

            base.Dispose(isDisposing);
        }
    }
}

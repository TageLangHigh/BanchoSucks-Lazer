// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: the player's own statistics from the local activity log: totals, bests and the last 30 days as
    /// bar charts. Opened from Settings > General > Daily goal, or by clicking the daily goal on the main menu.
    /// </summary>
    /// <remarks>
    /// The screen only reads <see cref="BanchosucksActivityLog"/>; every number comes from
    /// <see cref="BanchosucksActivityStats"/> and <see cref="BanchosucksDailyGoal"/>.
    /// </remarks>
    public partial class BanchosucksStatsScreen : OsuScreen
    {
        public const int DAYS_SHOWN = 30;

        private const float content_width = 900;
        private const float tile_spacing = 12;
        private const int tiles_per_row = 4;

        // absent in plain test scenes; the game always provides it
        [Resolved(CanBeNull = true)]
        private BanchosucksActivityLog? log { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private FillFlowContainer tiles = null!;
        private FillFlowContainer charts = null!;
        private OsuSpriteText sinceText = null!;
        private OsuTextFlowContainer noteText = null!;

        private BanchosucksBarChart timeChart = null!;
        private BanchosucksBarChart mapsChart = null!;
        private BanchosucksBarChart accuracyChart = null!;

        private DateOnly shownDay;

        /// <summary>
        /// Entries to show instead of the game's log (tests).
        /// </summary>
        public IReadOnlyList<BanchosucksActivityEntry>? EntriesOverride { get; init; }

        /// <summary>
        /// "Today" to show instead of the PC's date (tests).
        /// </summary>
        public DateOnly? TodayOverride { get; init; }

        /// <summary>
        /// The totals currently on screen (tests).
        /// </summary>
        public BanchosucksActivityStats.Totals? ShownTotals { get; private set; }

        public BanchosucksBarChart TimeChart => timeChart;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Width = content_width + 60,
                    AutoSizeAxes = Axes.Y,
                    Margin = new MarginPadding { Top = 50, Bottom = 40 },
                    Masking = true,
                    CornerRadius = 20,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.7f),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 18),
                            Padding = new MarginPadding(30),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = text("Personal statistics", "Eigene Statistik"),
                                    Font = OsuFont.GetFont(size: 40, weight: FontWeight.Bold),
                                },
                                sinceText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 16),
                                    Colour = colours.Gray9,
                                    Margin = new MarginPadding { Top = -12 },
                                },
                                tiles = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Full,
                                    Spacing = new Vector2(tile_spacing),
                                },
                                new OsuSpriteText
                                {
                                    Text = text("The last {0} days", "Die letzten {0} Tage", DAYS_SHOWN),
                                    Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                                    Margin = new MarginPadding { Top = 8 },
                                },
                                charts = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 16),
                                    Children = new Drawable[]
                                    {
                                        timeChart = new BanchosucksBarChart(text("Play time per day", "Spielzeit pro Tag"), colours.Blue, colours.Blue),
                                        mapsChart = new BanchosucksBarChart(text("Maps per day (light: played, dark: passed)", "Maps pro Tag (hell: gespielt, dunkel: bestanden)"), colours.GreenLight, colours.GreenDark),
                                        accuracyChart = new BanchosucksBarChart(text("Average accuracy per day (passed maps)", "Durchschnittliche Genauigkeit pro Tag (bestandene Maps)"), colours.Yellow, colours.Yellow),
                                    },
                                },
                                noteText = new OsuTextFlowContainer(t =>
                                {
                                    t.Font = OsuFont.GetFont(size: 14);
                                    t.Colour = colours.Gray9;
                                })
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (log != null)
                log.Changed += refresh;

            refresh();

            // the charts end with "today": follow the date when the screen stays open past midnight
            Scheduler.AddDelayed(() =>
            {
                if (today != shownDay)
                    refresh();
            }, 1000, true);
        }

        private DateOnly today => TodayOverride ?? log?.Today ?? DateOnly.FromDateTime(DateTime.Now);

        private void refresh()
        {
            var entries = EntriesOverride ?? log?.Entries ?? Array.Empty<BanchosucksActivityEntry>();
            shownDay = today;

            var totals = ShownTotals = BanchosucksActivityStats.Summarise(entries);
            var days = BanchosucksActivityStats.LastDays(entries, shownDay, DAYS_SHOWN);

            sinceText.Text = totals.FirstDay == null
                ? text("No plays recorded yet. Play a map and come back.", "Noch keine Plays aufgezeichnet. Spiel eine Map und schau wieder vorbei.")
                : text("Everything since {0:" + date_en + "}. Stored on this PC only, never sent anywhere.", "Alles seit dem {0:" + date_de + "}. Nur auf diesem PC gespeichert, wird nirgendwohin gesendet.", totals.FirstDay.Value);

            buildTiles(entries, totals);
            buildCharts(days);

            noteText.Text = totals.HasEstimated && log != null
                ? text("Days before {0:" + date_en + "} are estimated from your saved scores: passed maps only, play time taken from the map length. Play time counts active gameplay without breaks and pauses.",
                    "Tage vor dem {0:" + date_de + "} sind aus deinen gespeicherten Scores geschätzt: nur bestandene Maps, Spielzeit aus der Map-Länge. Als Spielzeit zählt aktives Spielen ohne Breaks und Pausen.",
                    DateOnly.FromDateTime(log.Created.DateTime))
                : text("Play time counts active gameplay without breaks and pauses. Replays, autoplay and the editor's test play are not recorded.",
                    "Als Spielzeit zählt aktives Spielen ohne Breaks und Pausen. Replays, Autoplay und das Testspiel im Editor werden nicht aufgezeichnet.");
        }

        private void buildTiles(IReadOnlyList<BanchosucksActivityEntry> entries, BanchosucksActivityStats.Totals totals)
        {
            var todaySummary = BanchosucksActivityStats.LastDays(entries, shownDay, 1)[0];

            int goalMinutes = log?.TodayProgress.GoalMinutes ?? 0;
            int goalMaps = log?.TodayProgress.GoalMaps ?? 0;
            bool goalSet = goalMinutes > 0 || goalMaps > 0;
            var goalDays = log?.GoalDays ?? Array.Empty<DateOnly>();

            LocalisableString none = "–";

            tiles.Children = new Drawable[]
            {
                new Tile(text("Play time", "Spielzeit"), Duration(totals.Seconds),
                    text("{0} active days", "{0} aktive Tage", totals.ActiveDays), colours.Blue),
                new Tile(text("Maps played", "Gespielte Maps"), text("{0:N0}", "{0:N0}", totals.Played),
                    totals.Played > 0
                        ? text("{0:N0} passed ({1:0}%)", "{0:N0} bestanden ({1:0} %)", totals.Passed, 100.0 * totals.Passed / totals.Played)
                        : none, colours.GreenLight),
                new Tile(text("Average accuracy", "Durchschnittliche Genauigkeit"), totals.Accuracy == null ? none : percent(totals.Accuracy.Value),
                    text("passed maps", "bestandene Maps"), colours.Yellow),
                new Tile(text("Today", "Heute"), Duration(todaySummary.Seconds),
                    text("{0} played, {1} passed", "{0} gespielt, {1} bestanden", todaySummary.Played, todaySummary.Passed), colours.Pink),
                new Tile(text("Longest session", "Längste Session"), totals.LongestSession == null ? none : Duration(totals.LongestSession.Value.Value),
                    totals.LongestSession == null ? none : date(totals.LongestSession.Value.Date), colours.Blue),
                new Tile(text("Most maps in a day", "Meiste Maps an einem Tag"), totals.MostMapsInDay == null ? none : text("{0:N0}", "{0:N0}", (int)totals.MostMapsInDay.Value.Value),
                    totals.MostMapsInDay == null ? none : date(totals.MostMapsInDay.Value.Date), colours.GreenLight),
                new Tile(text("Most play time in a day", "Meiste Spielzeit an einem Tag"), totals.MostTimeInDay == null ? none : Duration(totals.MostTimeInDay.Value.Value),
                    totals.MostTimeInDay == null ? none : date(totals.MostTimeInDay.Value.Date), colours.Yellow),
                new Tile(text("Daily goal streak", "Tagesziel-Serie"),
                    goalSet || goalDays.Count > 0 ? text("{0} days", "{0} Tage", BanchosucksDailyGoal.Streak(goalDays, shownDay)) : none,
                    goalSet || goalDays.Count > 0
                        ? text("best: {0}", "Rekord: {0}", BanchosucksDailyGoal.LongestStreak(goalDays))
                        : text("set one in Settings > General", "unter Einstellungen → Allgemein festlegen"), colours.Pink),
            };
        }

        private void buildCharts(BanchosucksActivityStats.Day[] days)
        {
            LocalisableString left = text("{0:" + short_en + "}", "{0:" + short_de + "}", days[0].Date);
            LocalisableString right = text("today", "heute");

            // play time, in minutes
            double mostMinutes = days.Max(d => d.Seconds) / 60;
            timeChart.SetBars(days.Select(d => new BanchosucksBarChart.Bar(d.Seconds / 60, null,
                    text("{0:" + short_en + "}: {1}", "{0:" + short_de + "}: {1}", d.Date, Duration(d.Seconds)), d.Estimated)).ToList(),
                mostMinutes, mostMinutes > 0 ? text("top: {0}", "oben: {0}", Duration(mostMinutes * 60)) : default, left, right);

            // maps played, the passed ones inside
            int mostMaps = days.Max(d => d.Played);
            mapsChart.SetBars(days.Select(d => new BanchosucksBarChart.Bar(d.Played, d.Passed,
                    text("{0:" + short_en + "}: {1} played, {2} passed", "{0:" + short_de + "}: {1} gespielt, {2} bestanden", d.Date, d.Played, d.Passed), d.Estimated)).ToList(),
                mostMaps, mostMaps > 0 ? text("top: {0} maps", "oben: {0} Maps", mostMaps) : default, left, right);

            // accuracy: nearly every day sits between 90 and 100 %, so the chart starts at a floor below the worst day instead of at zero
            var accuracies = days.Where(d => d.Accuracy != null).Select(d => d.Accuracy!.Value * 100).ToList();
            double floor = AccuracyFloor(accuracies);

            accuracyChart.SetBars(days.Select(d => new BanchosucksBarChart.Bar(d.Accuracy == null ? 0 : Math.Max(d.Accuracy.Value * 100 - floor, 0.0001), null,
                    d.Accuracy == null
                        ? text("{0:" + short_en + "}: no passed map", "{0:" + short_de + "}: keine bestandene Map", d.Date)
                        : text("{0:" + short_en + "}: {1:0.00}%", "{0:" + short_de + "}: {1:0.00} %", d.Date, d.Accuracy.Value * 100), d.Estimated)).ToList(),
                100 - floor, accuracies.Count > 0 ? text("bottom: {0:0}%, top: 100%", "unten: {0:0} %, oben: 100 %", floor) : default, left, right);
        }

        /// <summary>
        /// Where the accuracy chart starts: the next multiple of five at least two points below the worst day, never above 95.
        /// </summary>
        public static double AccuracyFloor(IReadOnlyCollection<double> accuracies)
        {
            if (accuracies.Count == 0)
                return 0;

            double floor = Math.Floor((accuracies.Min() - 2) / 5) * 5;
            return Math.Clamp(floor, 0, 95);
        }

        /// <summary>
        /// "42 min" or "3 h 05 min"; the same in both languages.
        /// </summary>
        public static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0)
                seconds = 0;

            long minutes = (long)Math.Round(seconds / 60);

            if (minutes < 1 && seconds > 0)
                return "< 1 min";

            return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60:00} min";
        }

        private static LocalisableString text(string en, string de, params object[] args) => ClickerStrings.Text(en, de, args);

        private static LocalisableString percent(double accuracy) => text("{0:0.00}%", "{0:0.00} %", accuracy * 100);

        // a text is formatted in one go with the language's culture, so dates go in as arguments with these formats, never as finished texts
        private const string date_en = "MMM d, yyyy";
        private const string date_de = "d. MMM yyyy";
        private const string short_en = "MMM d";
        private const string short_de = "d. MMM";

        private static LocalisableString date(DateOnly day) => text("{0:" + date_en + "}", "{0:" + date_de + "}", day);

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(250, Easing.OutQuint);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (log != null)
                log.Changed -= refresh;

            base.Dispose(isDisposing);
        }

        private partial class Tile : Container
        {
            public Tile(LocalisableString label, LocalisableString value, LocalisableString detail, Color4 accent)
            {
                Width = (content_width - (tiles_per_row - 1) * tile_spacing) / tiles_per_row;
                Height = 92;
                Masking = true;
                CornerRadius = 10;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.07f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 4,
                        Colour = accent,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 16, Right = 10, Top = 10 },
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = label,
                                Font = OsuFont.GetFont(size: 14),
                                Alpha = 0.7f,
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = value,
                                Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = detail,
                                Font = OsuFont.GetFont(size: 13),
                                Alpha = 0.7f,
                            },
                        },
                    },
                };
            }
        }
    }
}

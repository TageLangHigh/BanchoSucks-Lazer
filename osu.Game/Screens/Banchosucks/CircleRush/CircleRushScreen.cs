// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// Banchosucks: Circle Rush, an aim and reaction game. Circles appear faster and faster; click them before they vanish.
    /// One-minute mode scores a minute, endless mode ends after five misses. Four difficulties (osu! AR 8 to 11 at the
    /// start), the mods EZ, HD, HR and FL, and one leaderboard per mode and difficulty.
    /// </summary>
    public partial class CircleRushScreen : OsuScreen
    {
        private enum RunState
        {
            Menu,
            Countdown,
            Playing,
            Results,
        }

        private const double countdown_ms = 1800;
        private const float board_width = 400;

        // the last selection survives leaving the screen
        private static CircleRushMode lastMode = CircleRushMode.Minute;
        private static CircleRushDifficulty lastDifficulty = CircleRushDifficulty.Medium;
        private static CircleRushMods lastMods = CircleRushMods.None;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private RunState state = RunState.Menu;
        private CircleRushMode mode = lastMode;
        private CircleRushDifficulty difficulty = lastDifficulty;
        private CircleRushMods mods = lastMods;

        private Container menu = null!;
        private Container playArea = null!;
        private Container fieldHost = null!;
        private Container boardHost = null!;
        private Container results = null!;
        private CircleRushPlayfield playfield = null!;
        private CircleRushLeaderboardPanel board = null!;

        private readonly Dictionary<CircleRushMode, OptionButton> modeButtons = new Dictionary<CircleRushMode, OptionButton>();
        private readonly Dictionary<CircleRushDifficulty, OptionButton> difficultyButtons = new Dictionary<CircleRushDifficulty, OptionButton>();
        private readonly Dictionary<CircleRushMods, OptionButton> modButtons = new Dictionary<CircleRushMods, OptionButton>();

        private OsuSpriteText multiplierText = null!;
        private OsuSpriteText bestText = null!;
        private OsuSpriteText scoreText = null!;
        private OsuSpriteText comboText = null!;
        private OsuSpriteText clockText = null!;
        private OsuSpriteText speedText = null!;
        private OsuSpriteText countdownText = null!;
        private OsuSpriteText resultTitle = null!;
        private FillFlowContainer resultLines = null!;
        private OsuSpriteText resultSubmit = null!;
        private ClickerPillButton startButton = null!;

        private double countdownStart;
        private GetCircleRushBestRequest? bestRequest;
        private SubmitCircleRushRequest? submitRequest;
        private readonly Dictionary<string, CircleRushLeaderboardEntry> bests = new Dictionary<string, CircleRushLeaderboardEntry>();

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("0d0e12"), Color4Extensions.FromHex("1a1424")),
                },
                menu = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Right = board_width + 60 },
                    Child = createMenu(),
                },
                boardHost = new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = board_width,
                    RelativeSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Vertical = 40, Right = 30 },
                    Child = board = new CircleRushLeaderboardPanel(),
                },
                playArea = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        createHud(),
                        fieldHost = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 76, Bottom = 28, Horizontal = 28 },
                            Child = playfield = new CircleRushPlayfield(),
                        },
                        countdownText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.GetFont(size: 120, weight: FontWeight.Bold),
                            Alpha = 0,
                        },
                        results = createResults(),
                    },
                },
            };

            playfield.Hit += onHit;
            playfield.Missed += onMissed;
            playfield.Finished += onFinished;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            applySelection();
            board.SetBoard(mode, difficulty);
            loadBests();
        }

        // ---------------------------------------------------------------- layout

        private Drawable createMenu()
        {
            OsuSpriteText heading(LocalisableString text) => new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                Colour = colours.Gray9,
                Margin = new MarginPadding { Top = 6 },
            };

            FillFlowContainer row(IEnumerable<Drawable> children) => new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                ChildrenEnumerable = children,
            };

            var modeRow = new List<Drawable>();

            foreach (var m in new[] { CircleRushMode.Minute, CircleRushMode.Endless })
            {
                var value = m;
                modeRow.Add(modeButtons[m] = new OptionButton(CircleRushStrings.ModeName(m), colours.Pink, () => selectMode(value), 150));
            }

            var difficultyRow = new List<Drawable>();

            foreach (var d in new[] { CircleRushDifficulty.Easy, CircleRushDifficulty.Medium, CircleRushDifficulty.Hard, CircleRushDifficulty.Extreme })
            {
                var value = d;
                difficultyRow.Add(difficultyButtons[d] = new OptionButton(CircleRushStrings.DifficultyName(d), colours.Blue, () => selectDifficulty(value), 118));
            }

            var modRow = new List<Drawable>();

            foreach (var (flag, label) in new[]
                     {
                         (CircleRushMods.Easy, "EZ"), (CircleRushMods.Hidden, "HD"), (CircleRushMods.HardRock, "HR"), (CircleRushMods.Flashlight, "FL"),
                     })
            {
                var value = flag;
                modRow.Add(modButtons[flag] = new OptionButton(label, colours.Yellow, () => toggleMod(value), 70));
            }

            return new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Circle Rush",
                        Font = OsuFont.GetFont(size: 54, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText
                    {
                        Text = ClickerStrings.Text("Click the circles before they vanish. It only gets faster.", "Klick die Kreise, bevor sie verschwinden. Es wird nur schneller."),
                        Font = OsuFont.GetFont(size: 18),
                        Colour = colours.Gray9,
                        Margin = new MarginPadding { Bottom = 8 },
                    },
                    heading(ClickerStrings.Text("Mode", "Modus")),
                    row(modeRow),
                    heading(ClickerStrings.Text("Difficulty (start speed like osu! AR 8 / 9 / 10 / 11)", "Schwierigkeit (Starttempo wie osu! AR 8 / 9 / 10 / 11)")),
                    row(difficultyRow),
                    heading(ClickerStrings.Text("Mods (EZ ×1.20, HD ×1.06, HR ×1.06, FL ×1.12)", "Mods (EZ ×1,20, HD ×1,06, HR ×1,06, FL ×1,12)")),
                    row(modRow),
                    multiplierText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                        Colour = colours.Yellow,
                    },
                    bestText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                        Colour = colours.Gray9,
                    },
                    startButton = new ClickerPillButton(ClickerStrings.Text("Start", "Start"), colours.Pink, startRun)
                    {
                        Margin = new MarginPadding { Top = 10 },
                    },
                    new OsuSpriteText
                    {
                        Text = ClickerStrings.Text("Mouse, touch or Z / X at the cursor. Escape leaves the run. A click into nothing is a miss.",
                            "Maus, Touch oder Z / X am Cursor. Escape bricht den Lauf ab. Ein Klick ins Leere ist ein Miss."),
                        Font = OsuFont.GetFont(size: 13),
                        Colour = colours.Gray9,
                        Margin = new MarginPadding { Top = 10 },
                    },
                },
            };
        }

        private Drawable createHud()
        {
            OsuSpriteText big(Anchor anchor, Color4 colour) => new OsuSpriteText
            {
                Anchor = anchor,
                Origin = anchor,
                Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
                Colour = colour,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 70,
                Padding = new MarginPadding { Horizontal = 36, Top = 16 },
                Children = new Drawable[]
                {
                    scoreText = big(Anchor.TopLeft, colours.Pink),
                    comboText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Y = 34,
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                        Colour = colours.Gray9,
                    },
                    clockText = big(Anchor.TopCentre, Color4.White),
                    speedText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                        Colour = colours.Gray9,
                    },
                },
            };
        }

        private Container createResults()
        {
            return new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Alpha = 0,
                Masking = true,
                CornerRadius = 18,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("0d0e12").Opacity(0.94f) },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Padding = new MarginPadding(32),
                        Children = new Drawable[]
                        {
                            resultTitle = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 40, weight: FontWeight.Bold),
                            },
                            resultLines = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                            },
                            resultSubmit = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                                Colour = colours.Yellow,
                                Margin = new MarginPadding { Top = 6 },
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Margin = new MarginPadding { Top = 10 },
                                Children = new Drawable[]
                                {
                                    new ClickerPillButton(ClickerStrings.Text("Again", "Nochmal"), colours.Pink, startRun),
                                    new ClickerPillButton(ClickerStrings.Text("Back to the menu", "Zurück zum Menü"), colours.Blue, showMenu),
                                },
                            },
                        },
                    },
                },
            };
        }

        // ---------------------------------------------------------------- selection

        private void selectMode(CircleRushMode value)
        {
            mode = value;
            applySelection();
        }

        private void selectDifficulty(CircleRushDifficulty value)
        {
            difficulty = value;
            applySelection();
        }

        private void toggleMod(CircleRushMods flag)
        {
            mods ^= flag;

            // EZ and HR exclude each other: the one just chosen wins
            if (flag == CircleRushMods.Easy && mods.HasFlag(CircleRushMods.Easy)) mods &= ~CircleRushMods.HardRock;
            if (flag == CircleRushMods.HardRock && mods.HasFlag(CircleRushMods.HardRock)) mods &= ~CircleRushMods.Easy;

            applySelection();
        }

        private void applySelection()
        {
            lastMode = mode;
            lastDifficulty = difficulty;
            lastMods = mods;

            foreach (var (value, button) in modeButtons) button.Active = value == mode;
            foreach (var (value, button) in difficultyButtons) button.Active = value == difficulty;
            foreach (var (flag, button) in modButtons) button.Active = mods.HasFlag(flag);

            var culture = ClickerStrings.IsGerman ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
            string acronyms = CircleRushRules.ModsAcronyms(mods);
            multiplierText.Text = ClickerStrings.Pick(
                $"Score multiplier ×{CircleRushRules.ModMultiplier(mods).ToString("0.00", culture)}{(acronyms.Length > 0 ? $" ({acronyms})" : string.Empty)} · circle {CircleRushRules.Radius(difficulty, mods) * 2} px · {CircleRushRules.Lifetime(mode, difficulty, mods, 0)} ms at the start",
                $"Score-Multiplikator ×{CircleRushRules.ModMultiplier(mods).ToString("0.00", culture)}{(acronyms.Length > 0 ? $" ({acronyms})" : string.Empty)} · Kreis {CircleRushRules.Radius(difficulty, mods) * 2} px · {CircleRushRules.Lifetime(mode, difficulty, mods, 0)} ms am Anfang");

            updateBestText();

            if (IsLoaded)
                board.SetBoard(mode, difficulty);
        }

        private static string boardKey(CircleRushMode mode, CircleRushDifficulty difficulty) => $"{CircleRushRules.ModeKey(mode)}:{CircleRushRules.DifficultyKey(difficulty)}";

        private void updateBestText()
        {
            var culture = ClickerStrings.IsGerman ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;

            if (!api.IsLoggedIn)
            {
                bestText.Text = ClickerStrings.Text("Log in to save your runs on the leaderboard.", "Melde dich an, damit deine Läufe auf der Rangliste landen.");
                return;
            }

            if (bests.TryGetValue(boardKey(mode, difficulty), out var best))
            {
                string acronyms = CircleRushRules.ModsAcronyms((CircleRushMods)best.Mods);
                bestText.Text = ClickerStrings.Pick(
                    $"Your best: {best.Score.ToString("N0", culture)}{(acronyms.Length > 0 ? $" (+{acronyms})" : string.Empty)} · rank #{best.Rank}",
                    $"Deine Bestleistung: {best.Score.ToString("N0", culture)}{(acronyms.Length > 0 ? $" (+{acronyms})" : string.Empty)} · Rang #{best.Rank}");
            }
            else
                bestText.Text = ClickerStrings.Text("No run on this board yet.", "Noch kein Lauf auf dieser Rangliste.");
        }

        private void loadBests()
        {
            if (!api.IsLoggedIn) return;

            bestRequest?.Cancel();
            var request = bestRequest = new GetCircleRushBestRequest();
            request.Success += response =>
            {
                if (IsDisposed) return;

                bests.Clear();
                foreach (var (key, entry) in response.Best) bests[key] = entry;
                updateBestText();
            };
            api.Queue(request);
        }

        // ---------------------------------------------------------------- run

        private void startRun()
        {
            if (state == RunState.Countdown || state == RunState.Playing) return;

            long seed = Random.Shared.NextInt64(1, long.MaxValue);
            var run = new CircleRushRun(mode, difficulty, mods, seed);

            state = RunState.Countdown;
            countdownStart = Time.Current;
            playfield.Begin(run, countdownStart + countdown_ms);

            menu.FadeOut(200);
            boardHost.FadeOut(200);
            results.FadeOut(150);
            playArea.FadeIn(200);
            countdownText.FadeIn(100);
            updateHud(run);
        }

        private void showMenu()
        {
            playfield.Clear();
            state = RunState.Menu;
            results.FadeOut(150);
            playArea.FadeOut(200);
            menu.FadeIn(200);
            boardHost.FadeIn(200);
            board.Refresh();
            loadBests();
        }

        protected override void Update()
        {
            base.Update();

            // the field scales to whatever space the window leaves, keeping its 4:3 shape
            float scale = Math.Min(fieldHost.DrawWidth / CircleRushRules.PLAYFIELD_WIDTH, fieldHost.DrawHeight / CircleRushRules.PLAYFIELD_HEIGHT);
            if (scale > 0) playfield.Scale = new Vector2(scale);

            switch (state)
            {
                case RunState.Countdown:
                    double remaining = countdownStart + countdown_ms - Time.Current;

                    if (remaining <= 0)
                    {
                        state = RunState.Playing;
                        countdownText.FadeOut(150);
                    }
                    else
                        countdownText.Text = Math.Ceiling(remaining / 600).ToString(CultureInfo.InvariantCulture);

                    break;

                case RunState.Playing:
                    if (playfield.Run != null)
                        updateHud(playfield.Run);
                    break;
            }
        }

        private void updateHud(CircleRushRun run)
        {
            var culture = ClickerStrings.IsGerman ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
            int time = playfield.RunTime;

            scoreText.Text = run.Score.ToString("N0", culture);
            comboText.Text = ClickerStrings.Pick($"{run.Combo}x combo · {run.Hits} hits · {run.Misses} misses", $"{run.Combo}x Combo · {run.Hits} Treffer · {run.Misses} Misses");

            if (run.Mode == CircleRushMode.Minute)
            {
                int left = Math.Max(0, CircleRushRules.MINUTE_DURATION - time);
                clockText.Text = $"{left / 1000}.{left % 1000 / 100}";
            }
            else
            {
                clockText.Text = ClickerStrings.Pick($"{run.LivesLeft} lives · {time / 1000} s", $"{run.LivesLeft} Leben · {time / 1000} s");
            }

            string acronyms = CircleRushRules.ModsAcronyms(run.Mods);
            speedText.Text = ClickerStrings.Pick(
                $"{CircleRushStrings.DifficultyName(run.Difficulty)}{(acronyms.Length > 0 ? " +" + acronyms : string.Empty)} · {CircleRushRules.Lifetime(run.Mode, run.Difficulty, run.Mods, time)} ms per circle",
                $"{CircleRushStrings.DifficultyName(run.Difficulty)}{(acronyms.Length > 0 ? " +" + acronyms : string.Empty)} · {CircleRushRules.Lifetime(run.Mode, run.Difficulty, run.Mods, time)} ms pro Kreis");
        }

        private void onHit(int points, int combo)
        {
            scoreText.ScaleTo(1.12f, 40).Then().ScaleTo(1, 180, Easing.OutQuint);
        }

        private void onMissed()
        {
            comboText.FlashColour(colours.Red, 300);
        }

        private void onFinished()
        {
            var run = playfield.Run;
            if (run == null) return;

            state = RunState.Results;
            updateHud(run);
            showResults(run);
            submit(run);
        }

        private void showResults(CircleRushRun run)
        {
            var culture = ClickerStrings.IsGerman ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
            string acronyms = CircleRushRules.ModsAcronyms(run.Mods);

            resultTitle.Text = run.Mode == CircleRushMode.Minute
                ? ClickerStrings.Text("Time's up!", "Zeit um!")
                : ClickerStrings.Text("Out of lives!", "Keine Leben mehr!");

            resultLines.Clear();

            foreach (string line in new[]
                     {
                         ClickerStrings.Pick($"Score {run.Score.ToString("N0", culture)}{(acronyms.Length > 0 ? $" (+{acronyms}, ×{CircleRushRules.ModMultiplier(run.Mods).ToString("0.00", culture)})" : string.Empty)}",
                             $"Score {run.Score.ToString("N0", culture)}{(acronyms.Length > 0 ? $" (+{acronyms}, ×{CircleRushRules.ModMultiplier(run.Mods).ToString("0.00", culture)})" : string.Empty)}"),
                         ClickerStrings.Pick($"{run.Hits} hits · {run.Misses} misses · {run.MaxCombo}x best combo", $"{run.Hits} Treffer · {run.Misses} Misses · {run.MaxCombo}x beste Combo"),
                         ClickerStrings.Pick($"{run.AverageReaction} ms average reaction · {run.DurationMs / 1000.0:0.0} s", $"{run.AverageReaction} ms mittlere Reaktion · {run.DurationMs / 1000.0:0.0} s"),
                         $"{CircleRushStrings.ModeName(run.Mode)} · {CircleRushStrings.DifficultyName(run.Difficulty)}",
                     })
            {
                resultLines.Add(new OsuSpriteText { Text = line, Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold) });
            }

            resultSubmit.Text = api.IsLoggedIn
                ? ClickerStrings.Text("Submitting …", "Wird eingetragen …")
                : ClickerStrings.Text("Not logged in: the run is not saved.", "Nicht angemeldet: der Lauf wird nicht gespeichert.");

            results.FadeIn(250);
        }

        private void submit(CircleRushRun run)
        {
            if (!api.IsLoggedIn) return;

            submitRequest?.Cancel();
            var request = submitRequest = new SubmitCircleRushRequest(CircleRushSubmission.FromRun(run));
            var culture = ClickerStrings.IsGerman ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;

            request.Success += response =>
            {
                if (IsDisposed || request != submitRequest) return;

                if (!response.Accepted)
                {
                    resultSubmit.Text = ClickerStrings.Pick($"Not counted ({response.Reason}).", $"Nicht gewertet ({response.Reason}).");
                    return;
                }

                resultSubmit.Text = response.PersonalBest
                    ? ClickerStrings.Pick($"New personal best! Rank #{response.Rank}.", $"Neue Bestleistung! Rang #{response.Rank}.")
                    : ClickerStrings.Pick($"Saved. Your best on this board: {response.BestScore.ToString("N0", culture)} (rank #{response.Rank}).",
                        $"Gespeichert. Deine Bestleistung hier: {response.BestScore.ToString("N0", culture)} (Rang #{response.Rank}).");
                loadBests();
            };
            request.Failure += e =>
            {
                if (IsDisposed || request != submitRequest) return;

                Logger.Log($"Circle Rush submission failed: {e.Message}", LoggingTarget.Network);
                resultSubmit.Text = ClickerStrings.Text("The server could not be reached; the run is not saved.", "Der Server war nicht erreichbar; der Lauf wurde nicht gespeichert.");
            };
            api.Queue(request);
        }

        // ---------------------------------------------------------------- screen

        public override bool OnBackButton()
        {
            switch (state)
            {
                case RunState.Countdown:
                case RunState.Playing:
                case RunState.Results:
                    showMenu();
                    return true;

                default:
                    return base.OnBackButton();
            }
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(250, Easing.OutQuint);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            this.FadeIn(250, Easing.OutQuint);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);
            playfield.Clear();
            this.FadeOut(200);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            playfield.Clear();
            bestRequest?.Cancel();
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        /// <summary>A pill that shows whether it is the selected option.</summary>
        private partial class OptionButton : OsuClickableContainer
        {
            private readonly Box background;
            private readonly OsuSpriteText label;
            private readonly Color4 accent;
            private bool active;

            public bool Active
            {
                set
                {
                    active = value;
                    background.FadeColour(active ? accent : Color4.White.Opacity(0.08f), 120);
                    label.FadeColour(active ? Color4.Black : Color4.White, 120);
                }
            }

            public OptionButton(LocalisableString text, Color4 accent, Action action, float width)
            {
                this.accent = accent;
                Action = action;
                Size = new Vector2(width, 38);
                Masking = true;
                CornerRadius = 19;
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.08f) },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                    },
                };
            }
        }
    }
}

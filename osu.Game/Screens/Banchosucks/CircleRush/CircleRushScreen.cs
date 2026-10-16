// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

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

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RealmKeyBindingStore keyBindingStore { get; set; } = null!;

        private Bindable<float> cursorSize = null!;
        private bool bestBeaten;

        private RunState state = RunState.Menu;
        private CircleRushMode mode = lastMode;
        private CircleRushDifficulty difficulty = lastDifficulty;
        private CircleRushMods mods = lastMods;

        private Container menu = null!;
        private CircleRushCursorHost playArea = null!;
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
        private FillFlowContainer livesFlow = null!;
        private readonly List<SpriteIcon> hearts = new List<SpriteIcon>();
        private OsuSpriteText speedText = null!;
        private OsuSpriteText chaseText = null!;
        private OsuTextFlowContainer hintText = null!;
        private OsuSpriteText countdownText = null!;
        private OsuSpriteText resultTitle = null!;
        private FillFlowContainer resultLines = null!;
        private OsuSpriteText resultSubmit = null!;
        private ClickerPillButton startButton = null!;

        private double countdownStart;
        private int? previousBest;
        private GetCircleRushBestRequest? bestRequest;
        private SubmitCircleRushRequest? submitRequest;
        private readonly Dictionary<string, CircleRushLeaderboardEntry> bests = new Dictionary<string, CircleRushLeaderboardEntry>();

        [BackgroundDependencyLoader]
        private void load()
        {
            cursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize);

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
                playArea = new CircleRushCursorHost
                {
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
            playfield.ButtonStateChanged += pressed => playArea.SetButtonPressed(pressed);

            for (int i = 0; i < CircleRushRules.ENDLESS_LIVES; i++)
            {
                var heart = new SpriteIcon { Icon = FontAwesome.Solid.Heart, Size = new Vector2(16), Colour = colours.Red };
                hearts.Add(heart);
                livesFlow.Add(heart);
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Enter or Space starts (or restarts) a run from the menu and the results
            if (!e.Repeat && (e.Key == Key.Enter || e.Key == Key.KeypadEnter || e.Key == Key.Space) && (state == RunState.Menu || state == RunState.Results))
            {
                startRun();
                return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            applySelection();
            updateKeysHint();
            board.SetBoard(mode, difficulty);
            loadBests();
        }

        /// <summary>The player's actual osu! bindings for left and right button, as the settings show them.</summary>
        private void updateKeysHint()
        {
            string left = string.Join(", ", keyBindingStore.GetReadableKeyCombinationsFor("osu", 0, (int)CircleRushAction.LeftButton));
            string right = string.Join(", ", keyBindingStore.GetReadableKeyCombinationsFor("osu", 0, (int)CircleRushAction.RightButton));
            if (left.Length == 0) left = "Z";
            if (right.Length == 0) right = "X";

            hintText.Text = ClickerStrings.Pick(
                $"Your osu! keys: left button {left}, right button {right} (change them in the osu! settings), or touch. Numbered circles belong to one group. Escape leaves the run. A click into nothing is a miss.",
                $"Deine osu!-Tasten: linke Taste {left}, rechte Taste {right} (änderbar in den osu!-Einstellungen), oder Touch. Nummerierte Kreise gehören zu einer Gruppe. Escape bricht den Lauf ab. Ein Klick ins Leere ist ein Miss.");
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
                    new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 18))
                    {
                        Width = 640,
                        AutoSizeAxes = Axes.Y,
                        Colour = colours.Gray9,
                        Margin = new MarginPadding { Bottom = 8 },
                        Text = ClickerStrings.Text("Click the circles before they vanish: singles, bursts and streams, faster and faster.", "Klick die Kreise, bevor sie verschwinden: einzeln, als Burst und als Stream, immer schneller."),
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
                    hintText = new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 13))
                    {
                        Width = 640,
                        AutoSizeAxes = Axes.Y,
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
                    livesFlow = new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 38,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Alpha = 0,
                    },
                    speedText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                        Colour = colours.Gray9,
                    },
                    chaseText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Y = 24,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
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
            // like gameplay: the toolbar and every other overlay stay out of the way until the menu is back, the skin cursor takes over
            OverlayActivationMode.Value = OverlayActivation.Disabled;
            playArea.ProvideCursor = true;
            countdownStart = Time.Current;
            playfield.Begin(run, countdownStart + countdown_ms);

            previousBest = bests.TryGetValue(boardKey(mode, difficulty), out var best) ? best.Score : (int?)null;
            bestBeaten = false;
            chaseText.ClearTransforms();
            chaseText.Colour = colours.Gray9;
            chaseText.Scale = Vector2.One;
            menu.FadeOut(200);
            boardHost.FadeOut(200);
            results.FadeOut(150);
            playArea.FadeIn(200);
            countdownText.ClearTransforms();
            countdownText.Alpha = 1;
            countdownText.Colour = Color4.White;
            livesFlow.Alpha = run.Mode == CircleRushMode.Endless ? 1 : 0;
            updateHud(run);
        }

        private void showMenu()
        {
            playfield.Stop();
            state = RunState.Menu;
            playArea.ProvideCursor = false;
            OverlayActivationMode.Value = OverlayActivation.All;
            game?.Toolbar.Show();
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

            // the field scales to the space inside the host's padding (below the HUD), keeping its 4:3 shape
            float scale = Math.Min(fieldHost.ChildSize.X / CircleRushRules.PLAYFIELD_WIDTH, fieldHost.ChildSize.Y / CircleRushRules.PLAYFIELD_HEIGHT);

            if (scale > 0)
            {
                playfield.Scale = new Vector2(scale);
                // the skin cursor as big as in osu!: 1 skin pixel = 1 / 480 of the window height, times the cursor size setting
                playArea.CursorScale = scale * 1.25f * cursorSize.Value;
            }

            switch (state)
            {
                case RunState.Countdown:
                    double remaining = countdownStart + countdown_ms - Time.Current;

                    if (remaining <= 0)
                    {
                        state = RunState.Playing;
                        countdownText.Text = "GO!";
                        countdownText.Colour = colours.Pink;
                        countdownText.ScaleTo(1).ScaleTo(1.3f, 350, Easing.OutQuint).FadeOut(350);
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
                clockText.Colour = left <= 10_000 ? colours.Red : Color4.White;
            }
            else
            {
                clockText.Text = $"{time / 1000} s";
                clockText.Colour = Color4.White;

                for (int i = 0; i < hearts.Count; i++)
                    hearts[i].Colour = i < run.LivesLeft ? colours.Red : Color4.White.Opacity(0.18f);
            }

            string acronyms = CircleRushRules.ModsAcronyms(run.Mods);
            speedText.Text = ClickerStrings.Pick(
                $"{CircleRushStrings.DifficultyName(run.Difficulty)}{(acronyms.Length > 0 ? " +" + acronyms : string.Empty)} · {CircleRushRules.Lifetime(run.Mode, run.Difficulty, run.Mods, time)} ms per circle",
                $"{CircleRushStrings.DifficultyName(run.Difficulty)}{(acronyms.Length > 0 ? " +" + acronyms : string.Empty)} · {CircleRushRules.Lifetime(run.Mode, run.Difficulty, run.Mods, time)} ms pro Kreis");

            // the chase: how far the personal best is, and a one-time cheer when it falls
            if (previousBest is int before)
            {
                if (run.Score <= before)
                    chaseText.Text = ClickerStrings.Pick($"Best {before.ToString("N0", culture)} · {(before - run.Score + 1).ToString("N0", culture)} to go", $"Bestleistung {before.ToString("N0", culture)} · noch {(before - run.Score + 1).ToString("N0", culture)}");
                else if (!bestBeaten)
                {
                    bestBeaten = true;
                    chaseText.Text = ClickerStrings.Text("New personal best!", "Neue Bestleistung!");
                    chaseText.Colour = colours.Yellow;
                    chaseText.ScaleTo(1.35f).ScaleTo(1, 500, Easing.OutQuint);
                }
            }
            else
                chaseText.Text = ClickerStrings.Text("First run on this board", "Erster Lauf auf dieser Rangliste");
        }

        private void onHit(int points, int combo, CircleRushJudgement judgement)
        {
            scoreText.ScaleTo(1.12f, 40).Then().ScaleTo(1, 180, Easing.OutQuint);

            if (combo > 0 && combo % 25 == 0)
                comboText.FlashColour(colours.Yellow, 400);
        }

        private void onMissed()
        {
            comboText.FlashColour(colours.Red, 300);

            if (playfield.Run?.Mode == CircleRushMode.Endless)
                livesFlow.ScaleTo(1.25f, 60).Then().ScaleTo(1, 250, Easing.OutQuint);
        }

        private void onFinished()
        {
            var run = playfield.Run;
            if (run == null) return;

            state = RunState.Results;
            playArea.ProvideCursor = false;
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
                         ClickerStrings.Pick($"{run.Perfect} perfect · {run.Good} good · {run.Late} late", $"{run.Perfect} perfekt · {run.Good} gut · {run.Late} spät"),
                         ClickerStrings.Pick($"{run.AverageReaction} ms average reaction · {run.DurationMs / 1000.0:0.0} s", $"{run.AverageReaction} ms mittlere Reaktion · {run.DurationMs / 1000.0:0.0} s"),
                         $"{CircleRushStrings.ModeName(run.Mode)} · {CircleRushStrings.DifficultyName(run.Difficulty)}",
                         previousBest is int before
                             ? ClickerStrings.Pick($"Previous best: {before.ToString("N0", culture)} ({(run.Score >= before ? "+" : string.Empty)}{(run.Score - before).ToString("N0", culture)})",
                                 $"Bisherige Bestleistung: {before.ToString("N0", culture)} ({(run.Score >= before ? "+" : string.Empty)}{(run.Score - before).ToString("N0", culture)})")
                             : ClickerStrings.Pick("First run on this board", "Erster Lauf auf dieser Rangliste"),
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
            // the player may have changed the osu! bindings in the settings meanwhile
            updateKeysHint();
            this.FadeIn(250, Easing.OutQuint);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);

            // leaving in the middle of a run (a dialog, another screen): back to the menu, the run is not submitted
            if (state != RunState.Menu)
                showMenu();
            else
                playfield.Stop();

            this.FadeOut(200);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            playfield.Stop();
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

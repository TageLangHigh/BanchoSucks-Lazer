// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks.Guess
{
    /// <summary>Something to guess: a label shown as an answer and whatever the prompt needs to present it.</summary>
    public class GuessItem
    {
        public readonly string Key;
        public readonly string Label;
        public readonly object Payload;

        public GuessItem(string key, string label, object payload)
        {
            Key = key;
            Label = label;
            Payload = payload;
        }
    }

    /// <summary>
    /// Banchosucks: the frame of the guessing games. Four answers each round, fifteen seconds per round; faster correct
    /// answers and streaks score more. Library mode asks ten rounds about the player's own content
    /// (<see cref="LoadItems"/>), popular mode asks about the server's pool until the first mistake. What is presented
    /// (<see cref="CreatePrompt"/>, <see cref="CreatePopularPrompt"/>) comes from the subclass. Overlays are closed during
    /// a round so nothing spoils the answer.
    /// </summary>
    public abstract partial class GuessScreen : OsuScreen
    {
        /// <summary>"song" or "skin".</summary>
        protected abstract string GameKey { get; }

        protected abstract LocalisableString GameTitle { get; }
        protected abstract LocalisableString GameDescription { get; }
        protected abstract LocalisableString TooFewItemsText { get; }
        protected abstract LocalisableString PopularDescription { get; }
        protected abstract IconUsage GameIcon { get; }
        protected abstract Color4 Accent { get; }

        /// <summary>Everything the library mode can ask, at least four with different labels, else the game cannot start.</summary>
        protected abstract List<GuessItem> LoadItems();

        /// <summary>What the player sees (and hears) while guessing a library item; loaded asynchronously, it starts in its own LoadComplete.</summary>
        protected abstract GuessPrompt CreatePrompt(GuessItem item);

        /// <summary>The same for an entry of the server's pool.</summary>
        protected abstract GuessPrompt CreatePopularPrompt(GuessPoolItem item);

        protected virtual void OnRunStarted(GuessMode mode)
        {
        }

        protected virtual void OnRunEnded(GuessMode mode)
        {
        }

        private enum State
        {
            Menu,
            Loading,
            Round,
            Reveal,
            Results,
        }

        private const double reveal_ms = 1800;
        private const float board_width = 380;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved]
        protected OsuColour Colours { get; private set; } = null!;

        private State state = State.Menu;
        private GuessMode mode = GuessMode.Library;
        private readonly Random rng = new Random();
        private List<GuessItem> items = new List<GuessItem>();
        private readonly List<GuessItem> remaining = new List<GuessItem>();
        private GuessRun run = new GuessRun();
        private GuessItem? current;
        private int roundId;
        private double roundStart;
        private GuessPrompt? prompt;
        private int? previousBest;
        private GetGuessBestRequest? bestRequest;
        private GetGuessPoolRequest? poolRequest;
        private SubmitGuessRequest? submitRequest;
        private readonly Dictionary<string, int> bests = new Dictionary<string, int>();
        private List<GuessPoolItem>? pool;

        private Container menu = null!;
        private Container boardHost = null!;
        private Container playArea = null!;
        private Container promptHost = null!;
        private LoadingSpinner promptLoading = null!;
        private Container resultsPanel = null!;
        private OsuSpriteText roundText = null!;
        private OsuSpriteText scoreText = null!;
        private OsuSpriteText streakText = null!;
        private OsuSpriteText bestText = null!;
        private OsuSpriteText feedbackText = null!;
        private Box timerBar = null!;
        private readonly List<ChoiceButton> choiceButtons = new List<ChoiceButton>();
        private ModeButton libraryButton = null!;
        private ModeButton popularButton = null!;
        private OsuSpriteText resultTitle = null!;
        private FillFlowContainer resultLines = null!;
        private OsuSpriteText resultSubmit = null!;
        private GuessLeaderboardPanel board = null!;

        private string kind => GuessRules.KindFor(GameKey, mode);

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("0d0e12"), Accent.Darken(2.2f)),
                },
                new Triangles
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = Accent.Darken(1.2f),
                    ColourDark = Accent.Darken(2.4f),
                    TriangleScale = 2.5f,
                    Velocity = 0.4f,
                    Alpha = 0.35f,
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
                    Padding = new MarginPadding { Vertical = 60, Right = 30 },
                    Child = board = new GuessLeaderboardPanel(kind, Accent),
                },
                playArea = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Padding = new MarginPadding { Top = 60, Bottom = 30, Horizontal = 40 },
                    Children = new Drawable[]
                    {
                        createHud(),
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 60 },
                            Child = new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                RowDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.Absolute, 14),
                                    new Dimension(GridSizeMode.Absolute, 8),
                                    new Dimension(GridSizeMode.Absolute, 14),
                                    new Dimension(GridSizeMode.Absolute, 150),
                                    new Dimension(GridSizeMode.Absolute, 30),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Children = new Drawable[]
                                            {
                                                promptHost = new Container { RelativeSizeAxes = Axes.Both },
                                                promptLoading = new LoadingSpinner { Anchor = Anchor.Centre, Origin = Anchor.Centre },
                                            },
                                        },
                                    },
                                    new Drawable[] { Empty() },
                                    new Drawable[]
                                    {
                                        new CircularContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Masking = true,
                                            Children = new Drawable[]
                                            {
                                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f) },
                                                timerBar = new Box { RelativeSizeAxes = Axes.Both, Colour = Accent },
                                            },
                                        },
                                    },
                                    new Drawable[] { Empty() },
                                    new Drawable[] { createChoices() },
                                    new Drawable[]
                                    {
                                        feedbackText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                        },
                                    },
                                },
                            },
                        },
                        resultsPanel = createResults(),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            selectMode(GuessMode.Library);
            loadBests();
        }

        // ---------------------------------------------------------------- layout

        private Drawable createMenu() => new FillFlowContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Width = 580,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 12),
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(14, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon { Icon = GameIcon, Size = new Vector2(44), Colour = Accent, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                        new OsuSpriteText { Text = GameTitle, Font = OsuFont.GetFont(size: 48, weight: FontWeight.Bold), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                    },
                },
                new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 17))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = Colours.Gray9,
                    Text = GameDescription,
                },
                new OsuSpriteText
                {
                    Text = ClickerStrings.Text("Mode", "Modus"),
                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    Colour = Colours.Gray9,
                    Margin = new MarginPadding { Top = 6 },
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        libraryButton = new ModeButton(ClickerStrings.Text("My library", "Meine Bibliothek"),
                            ClickerStrings.Text("{0} rounds about your own files", "{0} Runden über deine eigenen Dateien", GuessRules.ROUNDS),
                            FontAwesome.Solid.Folder, Accent, () => selectMode(GuessMode.Library)),
                        popularButton = new ModeButton(ClickerStrings.Text("Popular, endless", "Beliebt, endlos"),
                            PopularDescription, FontAwesome.Solid.Fire, Accent, () => selectMode(GuessMode.Popular)),
                    },
                },
                new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 13))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = Colours.Gray9,
                    Text = ClickerStrings.Text("Four answers, {0} seconds per round. A correct answer gives 100 points plus 10 per second left, streaks add up to 50. In endless mode the first mistake ends the run. Keys 1 to 4 answer too.",
                        "Vier Antworten, {0} Sekunden pro Runde. Eine richtige Antwort gibt 100 Punkte plus 10 je übriger Sekunde, Serien bringen bis zu 50 dazu. Im Endlos-Modus beendet der erste Fehler den Lauf. Die Tasten 1 bis 4 antworten auch.",
                        GuessRules.ROUND_MS / 1000),
                },
                bestText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                    Colour = Colours.Yellow,
                },
                new ClickerPillButton(ClickerStrings.Text("Start", "Start"), Accent, startRun, FontAwesome.Solid.Play)
                {
                    Margin = new MarginPadding { Top = 8 },
                },
            },
        };

        private Drawable createHud()
        {
            OsuSpriteText text(Anchor anchor, float size, Color4 colour) => new OsuSpriteText
            {
                Anchor = anchor,
                Origin = anchor,
                Font = OsuFont.GetFont(size: size, weight: FontWeight.Bold),
                Colour = colour,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 50,
                Children = new Drawable[]
                {
                    roundText = text(Anchor.TopLeft, 26, Color4.White),
                    scoreText = text(Anchor.TopCentre, 30, Accent),
                    streakText = text(Anchor.TopRight, 18, Colours.Yellow),
                },
            };
        }

        private GridContainer createChoices()
        {
            choiceButtons.Clear();
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                choiceButtons.Add(new ChoiceButton(index, () => answer(index), Accent));
            }

            return new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 12), new Dimension() },
                RowDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 12), new Dimension() },
                Content = new[]
                {
                    new Drawable[] { choiceButtons[0], Empty(), choiceButtons[1] },
                    new Drawable[] { Empty(), Empty(), Empty() },
                    new Drawable[] { choiceButtons[2], Empty(), choiceButtons[3] },
                },
            };
        }

        private Container createResults() => new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Alpha = 0,
            Masking = true,
            CornerRadius = 18,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("0d0e12").Opacity(0.95f) },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding(32),
                    Children = new Drawable[]
                    {
                        resultTitle = new OsuSpriteText { Font = OsuFont.GetFont(size: 40, weight: FontWeight.Bold) },
                        resultLines = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                        },
                        resultSubmit = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                            Colour = Colours.Yellow,
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
                                new ClickerPillButton(ClickerStrings.Text("Again", "Nochmal"), Accent, startRun),
                                new ClickerPillButton(ClickerStrings.Text("Back to the menu", "Zurück zum Menü"), Colours.Blue, showMenu),
                            },
                        },
                    },
                },
            },
        };

        // ---------------------------------------------------------------- flow

        private void selectMode(GuessMode value)
        {
            mode = value;
            libraryButton.Active = value == GuessMode.Library;
            popularButton.Active = value == GuessMode.Popular;
            board.SetKind(kind);
            updateBestText();
        }

        private void startRun()
        {
            if (state is State.Round or State.Reveal or State.Loading) return;

            if (mode == GuessMode.Library)
            {
                try
                {
                    items = LoadItems().GroupBy(i => i.Label).Select(g => g.First()).ToList();
                }
                catch (Exception e)
                {
                    Logger.Log($"{GameKey} guesser could not load its items: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                    items = new List<GuessItem>();
                }

                if (items.Count < 4)
                {
                    bestText.Text = TooFewItemsText;
                    bestText.FlashColour(Colours.Red, 600);
                    return;
                }

                beginRun();
                return;
            }

            // popular mode: the pool comes from the server, once per visit
            if (pool != null && pool.Count >= 4)
            {
                items = pool.Select(p => new GuessItem(p.Id.ToString(), p.Name, p)).GroupBy(i => i.Label).Select(g => g.First()).ToList();
                beginRun();
                return;
            }

            state = State.Loading;
            bestText.Text = ClickerStrings.Text("Fetching the popular pool …", "Beliebter Pool wird geholt …");
            poolRequest?.Cancel();
            var request = poolRequest = new GetGuessPoolRequest(GameKey);

            request.Success += response =>
            {
                if (IsDisposed || request != poolRequest) return;

                state = State.Menu;
                pool = response.Items;

                if (pool.Count < 4)
                {
                    bestText.Text = ClickerStrings.Text("The server has no popular pool yet (fewer than four entries).", "Der Server hat noch keinen beliebten Pool (weniger als vier Einträge).");
                    bestText.FlashColour(Colours.Red, 600);
                    return;
                }

                startRun();
            };
            request.Failure += e =>
            {
                if (IsDisposed || request != poolRequest) return;

                state = State.Menu;
                Logger.Log($"{GameKey} guesser could not fetch the pool: {e.Message}", LoggingTarget.Network);
                bestText.Text = ClickerStrings.Text("The server could not be reached.", "Der Server war nicht erreichbar.");
                bestText.FlashColour(Colours.Red, 600);
            };
            api.Queue(request);
        }

        private void beginRun()
        {
            run = new GuessRun(mode == GuessMode.Popular);
            remaining.Clear();
            remaining.AddRange(items);
            previousBest = bests.TryGetValue(kind, out int best) ? best : null;
            OverlayActivationMode.Value = OverlayActivation.Disabled;
            OnRunStarted(mode);

            menu.FadeOut(200);
            boardHost.FadeOut(200);
            resultsPanel.FadeOut(150);
            playArea.FadeIn(200);
            nextRound();
        }

        private void nextRound()
        {
            if (run.Finished || remaining.Count == 0)
            {
                finishRun();
                return;
            }

            current = remaining[rng.Next(remaining.Count)];
            remaining.Remove(current);

            var distractors = items.Where(i => i.Label != current.Label).OrderBy(_ => rng.Next()).Take(3).ToList();
            var options = distractors.Append(current).OrderBy(_ => rng.Next()).ToList();

            for (int i = 0; i < 4; i++)
                choiceButtons[i].Set(options[i]);

            feedbackText.Text = string.Empty;
            promptHost.Clear();
            prompt = null;
            promptLoading.Show();

            int thisRound = ++roundId;
            state = State.Loading;
            roundStart = Time.Current;
            updateHud();

            GuessPrompt created;

            try
            {
                created = current.Payload is GuessPoolItem poolItem ? CreatePopularPrompt(poolItem) : CreatePrompt(current);
            }
            catch (Exception e)
            {
                Logger.Log($"{GameKey} guesser could not present {current.Label}: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                created = new GuessPrompt.Broken();
            }

            // the prompt may read files or fetch things, so it loads off the update thread; the clock starts when it is there
            LoadComponentAsync(created, loaded =>
            {
                if (IsDisposed || thisRound != roundId || state != State.Loading) return;

                promptLoading.Hide();
                promptHost.Add(prompt = loaded);
                loaded.FadeInFromZero(250).ScaleTo(0.97f).ScaleTo(1, 350, Easing.OutQuint);
                state = State.Round;
                roundStart = Time.Current;
                updateHud();
            });
        }

        private void answer(int index)
        {
            if (state != State.Round || current == null) return;

            var chosen = choiceButtons[index].Item;
            int elapsed = (int)Math.Clamp(Time.Current - roundStart, 0, GuessRules.ROUND_MS);
            resolve(chosen != null && chosen.Label == current.Label, index, elapsed);
        }

        private void timeout()
        {
            if (state != State.Round) return;

            resolve(false, -1, GuessRules.ROUND_MS);
        }

        private void resolve(bool correct, int chosenIndex, int elapsedMs)
        {
            if (current == null) return;

            int points = run.Answer(correct, elapsedMs);
            state = State.Reveal;

            for (int i = 0; i < 4; i++)
            {
                var button = choiceButtons[i];
                if (button.Item?.Label == current.Label) button.Mark(ChoiceButton.Outcome.Correct);
                else if (i == chosenIndex) button.Mark(ChoiceButton.Outcome.Wrong);
                else button.Mark(ChoiceButton.Outcome.Dim);
            }

            feedbackText.Text = correct
                ? ClickerStrings.Pick($"+{points}", $"+{points}")
                : chosenIndex < 0 ? ClickerStrings.Text("Time's up!", "Zeit um!") : ClickerStrings.Text("Wrong.", "Falsch.");
            feedbackText.Colour = correct ? Colours.Green : Colours.Red;
            feedbackText.ScaleTo(1.3f).ScaleTo(1, 400, Easing.OutQuint);

            prompt?.Reveal(correct);
            updateHud();

            Scheduler.AddDelayed(() =>
            {
                if (state == State.Reveal)
                    nextRound();
            }, run.Ended ? reveal_ms + 600 : reveal_ms);
        }

        private void finishRun()
        {
            state = State.Results;
            promptHost.Clear();
            promptLoading.Hide();
            prompt = null;
            roundId++;
            OnRunEnded(mode);
            showResults();
            submit();
        }

        private void showMenu()
        {
            if (state is State.Round or State.Reveal or State.Loading)
            {
                promptHost.Clear();
                promptLoading.Hide();
                prompt = null;
                roundId++;
                poolRequest?.Cancel();
                OnRunEnded(mode);
            }

            state = State.Menu;
            // the toolbar was closed with the overlays for the run; bring it back
            OverlayActivationMode.Value = OverlayActivation.All;
            game?.Toolbar.Show();
            resultsPanel.FadeOut(150);
            playArea.FadeOut(200);
            menu.FadeIn(200);
            boardHost.FadeIn(200);
            board.Refresh();
            loadBests();
        }

        protected override void Update()
        {
            base.Update();

            if (state == State.Round)
            {
                double elapsed = Time.Current - roundStart;
                timerBar.Width = (float)Math.Clamp(1 - elapsed / GuessRules.ROUND_MS, 0, 1);
                timerBar.Colour = elapsed > GuessRules.ROUND_MS * 0.7 ? Colours.Red : Accent;

                if (elapsed >= GuessRules.ROUND_MS)
                    timeout();
            }
            else if (state == State.Loading)
                timerBar.Width = 1;
        }

        private void updateHud()
        {
            int shown = Math.Min(run.Endless ? GuessRules.ENDLESS_MAX_ROUNDS : GuessRules.ROUNDS, run.Rounds + (state is State.Round or State.Loading ? 1 : 0));
            roundText.Text = run.Endless
                ? ClickerStrings.Pick($"Round {shown}", $"Runde {shown}")
                : ClickerStrings.Pick($"Round {shown}/{GuessRules.ROUNDS}", $"Runde {shown}/{GuessRules.ROUNDS}");
            scoreText.Text = run.Score.ToString("N0");
            streakText.Text = run.Streak >= 2 ? ClickerStrings.Pick($"{run.Streak} in a row", $"{run.Streak} in Folge") : string.Empty;
        }

        private void showResults()
        {
            if (run.Endless)
            {
                resultTitle.Text = run.Correct >= 25
                    ? ClickerStrings.Text("Impressive!", "Beeindruckend!")
                    : run.Correct >= 10 ? ClickerStrings.Text("Nice run!", "Guter Lauf!") : ClickerStrings.Text("Run over.", "Lauf vorbei.");
            }
            else
            {
                resultTitle.Text = run.Correct == GuessRules.ROUNDS
                    ? ClickerStrings.Text("Perfect!", "Perfekt!")
                    : run.Correct >= GuessRules.ROUNDS * 0.7 ? ClickerStrings.Text("Well done!", "Gut gemacht!") : ClickerStrings.Text("Done.", "Fertig.");
            }

            resultLines.Clear();

            foreach (string line in new[]
                     {
                         ClickerStrings.Pick($"Score {run.Score:N0}", $"Score {run.Score:N0}"),
                         run.Endless
                             ? ClickerStrings.Pick($"{run.Correct} in a row", $"{run.Correct} in Folge")
                             : ClickerStrings.Pick($"{run.Correct} of {run.Rounds} correct · best streak {run.BestStreak}", $"{run.Correct} von {run.Rounds} richtig · beste Serie {run.BestStreak}"),
                         ClickerStrings.Pick($"{run.AverageAnswerMs / 1000.0:0.0} s per correct answer", $"{run.AverageAnswerMs / 1000.0:0.0} s je richtiger Antwort"),
                         previousBest is int before
                             ? ClickerStrings.Pick($"Previous best: {before:N0} ({(run.Score >= before ? "+" : string.Empty)}{run.Score - before:N0})", $"Bisherige Bestleistung: {before:N0} ({(run.Score >= before ? "+" : string.Empty)}{run.Score - before:N0})")
                             : ClickerStrings.Pick("First run on this board", "Erster Lauf auf dieser Rangliste"),
                     })
            {
                resultLines.Add(new OsuSpriteText { Text = line, Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold) });
            }

            resultSubmit.Text = api.IsLoggedIn
                ? ClickerStrings.Text("Submitting …", "Wird eingetragen …")
                : ClickerStrings.Text("Not logged in: the run is not saved.", "Nicht angemeldet: der Lauf wird nicht gespeichert.");
            resultsPanel.FadeIn(250);
        }

        // ---------------------------------------------------------------- server

        private void updateBestText()
        {
            if (!api.IsLoggedIn)
                bestText.Text = ClickerStrings.Text("Log in to save your runs on the leaderboard.", "Melde dich an, damit deine Läufe auf der Rangliste landen.");
            else if (bests.TryGetValue(kind, out int best))
                bestText.Text = ClickerStrings.Pick($"Your best on this board: {best:N0}", $"Deine Bestleistung auf dieser Rangliste: {best:N0}");
            else
                bestText.Text = ClickerStrings.Text("No run on this board yet.", "Noch kein Lauf auf dieser Rangliste.");
        }

        private void loadBests()
        {
            if (!api.IsLoggedIn) return;

            bestRequest?.Cancel();
            var request = bestRequest = new GetGuessBestRequest();
            request.Success += response =>
            {
                if (IsDisposed || request != bestRequest) return;

                bests.Clear();
                foreach (var (key, entry) in response.Best) bests[key] = entry.Score;
                updateBestText();
            };
            api.Queue(request);
        }

        private void submit()
        {
            if (!api.IsLoggedIn || run.Score <= 0) return;

            string submittedKind = kind;
            submitRequest?.Cancel();
            var request = submitRequest = new SubmitGuessRequest(new GuessSubmission
            {
                Kind = submittedKind,
                Score = run.Score,
                Correct = run.Correct,
                Rounds = run.Rounds,
                AverageMs = run.AverageAnswerMs,
            });

            request.Success += response =>
            {
                if (IsDisposed || request != submitRequest) return;

                if (!response.Accepted)
                {
                    resultSubmit.Text = ClickerStrings.Pick($"Not counted ({response.Reason}).", $"Nicht gewertet ({response.Reason}).");
                    return;
                }

                bests[submittedKind] = response.BestScore;
                resultSubmit.Text = response.PersonalBest
                    ? ClickerStrings.Pick($"New personal best! Rank #{response.Rank}.", $"Neue Bestleistung! Rang #{response.Rank}.")
                    : ClickerStrings.Pick($"Saved. Your best: {response.BestScore:N0} (rank #{response.Rank}).", $"Gespeichert. Deine Bestleistung: {response.BestScore:N0} (Rang #{response.Rank}).");
            };
            request.Failure += e =>
            {
                if (IsDisposed || request != submitRequest) return;

                Logger.Log($"{GameKey} guesser submission failed: {e.Message}", LoggingTarget.Network);
                resultSubmit.Text = ClickerStrings.Text("The server could not be reached; the run is not saved.", "Der Server war nicht erreichbar; der Lauf wurde nicht gespeichert.");
            };
            api.Queue(request);
        }

        // ---------------------------------------------------------------- input and screen

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat) return base.OnKeyDown(e);

            if (state == State.Round)
            {
                int? index = e.Key switch
                {
                    Key.Number1 or Key.Keypad1 => 0,
                    Key.Number2 or Key.Keypad2 => 1,
                    Key.Number3 or Key.Keypad3 => 2,
                    Key.Number4 or Key.Keypad4 => 3,
                    _ => null,
                };

                if (index != null)
                {
                    answer(index.Value);
                    return true;
                }
            }

            if ((state == State.Menu || state == State.Results) && (e.Key == Key.Enter || e.Key == Key.KeypadEnter))
            {
                startRun();
                return true;
            }

            return base.OnKeyDown(e);
        }

        public override bool OnBackButton()
        {
            if (state != State.Menu)
            {
                showMenu();
                return true;
            }

            return base.OnBackButton();
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
            if (state != State.Menu) showMenu();
            this.FadeOut(200);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (state != State.Menu) showMenu();
            bestRequest?.Cancel();
            poolRequest?.Cancel();
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        /// <summary>A mode card: icon, name, one line of explanation, highlighted when selected.</summary>
        private partial class ModeButton : OsuClickableContainer
        {
            private readonly Box background;
            private readonly Color4 accent;

            public bool Active
            {
                set
                {
                    background.FadeColour(value ? accent.Opacity(0.45f) : Color4.White.Opacity(0.08f), 150);
                    BorderThickness = value ? 2 : 0;
                }
            }

            public ModeButton(LocalisableString title, LocalisableString description, IconUsage icon, Color4 accent, Action action)
            {
                this.accent = accent;
                Action = action;
                Width = 285;
                Height = 64;
                Masking = true;
                CornerRadius = 12;
                BorderColour = accent;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.08f) },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 14,
                        Size = new Vector2(22),
                        Icon = icon,
                        Colour = accent,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 48,
                        Width = 225,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText { Text = title, Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold) },
                            new TruncatingSpriteText { Text = description, Font = OsuFont.GetFont(size: 12), Colour = Color4.White.Opacity(0.75f), MaxWidth = 225 },
                        },
                    },
                };
            }
        }

        /// <summary>An answer: a wide pill with the label, coloured after the round.</summary>
        private partial class ChoiceButton : OsuClickableContainer
        {
            public enum Outcome
            {
                Correct,
                Wrong,
                Dim,
            }

            public GuessItem? Item { get; private set; }

            private readonly Box background;
            private readonly TruncatingSpriteText label;
            private readonly Color4 accent;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public ChoiceButton(int index, Action action, Color4 accent)
            {
                this.accent = accent;
                Action = action;
                RelativeSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 12;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f) },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Text = (index + 1).ToString(),
                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                        Colour = accent,
                    },
                    label = new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 44,
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                    },
                };
            }

            protected override void Update()
            {
                base.Update();
                label.MaxWidth = Math.Max(10, DrawWidth - 60);
            }

            public void Set(GuessItem item)
            {
                Item = item;
                label.Text = item.Label;
                background.FadeColour(Color4.White.Opacity(0.1f), 100);
                this.FadeTo(1, 100);
                Enabled.Value = true;
            }

            public void Mark(Outcome outcome)
            {
                Enabled.Value = false;

                switch (outcome)
                {
                    case Outcome.Correct:
                        background.FadeColour(colours.Green.Opacity(0.6f), 150);
                        break;

                    case Outcome.Wrong:
                        background.FadeColour(colours.Red.Opacity(0.6f), 150);
                        break;

                    default:
                        this.FadeTo(0.4f, 150);
                        break;
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value) background.FadeColour(accent.Opacity(0.35f), 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (Enabled.Value) background.FadeColour(Color4.White.Opacity(0.1f), 100);
                base.OnHoverLost(e);
            }
        }
    }

    /// <summary>What the player sees while guessing; subclasses present a song or a skin and may reveal more once answered.</summary>
    public abstract partial class GuessPrompt : CompositeDrawable
    {
        protected GuessPrompt()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 16;
        }

        /// <summary>The round is over: show what it was.</summary>
        public virtual void Reveal(bool correct)
        {
        }

        /// <summary>Shown when an item could not be presented; the round still counts.</summary>
        internal partial class Broken : GuessPrompt
        {
            public Broken()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.06f) },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = ClickerStrings.Text("This one could not be loaded. Guess anyway!", "Das konnte nicht geladen werden. Rate trotzdem!"),
                        Font = OsuFont.GetFont(size: 18),
                    },
                };
            }
        }
    }

    /// <summary>Top 50 of one guessing board plus the player's own row.</summary>
    internal partial class GuessLeaderboardPanel : CompositeDrawable
    {
        private const double refresh_interval = 45_000;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private string kind;
        private readonly Color4 accent;
        private GetGuessLeaderboardRequest? request;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText ownText = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer rows = null!;
        private LoadingSpinner loading = null!;
        private double lastRefresh = double.MinValue;

        public GuessLeaderboardPanel(string kind, Color4 accent)
        {
            this.kind = kind;
            this.accent = accent;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 16,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("0d0e12").Opacity(0.75f) },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(14),
                        Spacing = new Vector2(0, 8),
                        Children = new Drawable[]
                        {
                            titleText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                Colour = accent,
                            },
                            ownText = new OsuSpriteText { Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold), Colour = colours.Yellow },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Bottom = 60 },
                                Children = new Drawable[]
                                {
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ScrollbarVisible = false,
                                        Child = rows = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 4),
                                        },
                                    },
                                    loading = new LoadingSpinner { Anchor = Anchor.Centre, Origin = Anchor.Centre },
                                    statusText = new OsuSpriteText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Y = 20,
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colours.Gray9,
                                    },
                                },
                            },
                        },
                    },
                },
            };

            updateTitle();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (IsPresent && Time.Current - lastRefresh > refresh_interval)
                Refresh();
        }

        public void SetKind(string newKind)
        {
            if (kind == newKind) return;

            kind = newKind;
            updateTitle();
            if (IsLoaded) Refresh();
        }

        private void updateTitle()
        {
            titleText.Text = kind.EndsWith("_endless", StringComparison.Ordinal)
                ? ClickerStrings.Text("Leaderboard · popular, endless", "Rangliste · beliebt, endlos")
                : ClickerStrings.Text("Leaderboard · my library", "Rangliste · meine Bibliothek");
        }

        public void Refresh()
        {
            lastRefresh = Time.Current;
            request?.Cancel();
            loading.Show();
            statusText.Text = string.Empty;

            var requested = request = new GetGuessLeaderboardRequest(kind);
            requested.Success += response =>
            {
                if (IsDisposed || requested != request) return;

                loading.Hide();
                rows.Clear();
                if (response.Entries.Count == 0)
                    statusText.Text = ClickerStrings.Text("No runs yet. Be the first.", "Noch keine Läufe. Sei der Erste.");

                foreach (var entry in response.Entries)
                    rows.Add(new Row(entry, entry.UserId == api.LocalUser.Value.OnlineID, colours));

                ownText.Text = response.Own != null
                    ? ClickerStrings.Pick($"You: rank #{response.Own.Rank} · {response.Own.Score:N0} points", $"Du: Rang #{response.Own.Rank} · {response.Own.Score:N0} Punkte")
                    : api.IsLoggedIn ? ClickerStrings.Text("You are not on this board yet.", "Du bist noch nicht auf dieser Rangliste.") : string.Empty;
            };
            requested.Failure += e =>
            {
                if (IsDisposed || e is OperationCanceledException || requested != request) return;

                loading.Hide();
                statusText.Text = ClickerStrings.Text("The server could not be reached.", "Der Server war nicht erreichbar.");
            };
            api.Queue(requested);
        }

        private partial class Row : CompositeDrawable
        {
            public Row(GuessLeaderboardEntry entry, bool own, OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;
                Height = 38;
                Masking = true;
                CornerRadius = 8;
                Enum.TryParse(entry.CountryCode, true, out CountryCode country);

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = own ? colours.Yellow.Opacity(0.18f) : Color4.White.Opacity(0.05f) },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 10,
                        Text = $"#{entry.Rank}",
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                        Colour = entry.Rank <= 3 ? colours.Yellow : Color4.White,
                    },
                    new UpdateableFlag(country) { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 46, Size = new Vector2(22, 15) },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 76,
                        Text = entry.Username,
                        Font = OsuFont.GetFont(size: 14, weight: own ? FontWeight.Bold : FontWeight.SemiBold),
                        MaxWidth = 130,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -70,
                        Text = $"{entry.Correct}/{entry.Rounds}",
                        Font = OsuFont.GetFont(size: 12),
                        Colour = colours.Gray9,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -10,
                        Text = entry.Score.ToString("N0"),
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold, fixedWidth: true),
                    },
                };
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Banchosucks;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Banchosucks: the weekly tile on the main menu (top left, under the daily goal): one line that names the map of the week
    /// and the community goal's progress; a click opens a small panel with the map (show it in song select or download it)
    /// and the goal with a progress bar. Invisible while there is neither a map nor a goal, and while logged out.
    /// </summary>
    /// <remarks>
    /// Two small requests (<c>banchosucks_motw/current</c>, <c>banchosucks_goal/current</c>) when the main menu shows the
    /// tile, then every five minutes while it stays on screen; opening the panel refreshes, but never sooner than a minute
    /// after the last request. Nothing is sent to the server about the player.
    /// </remarks>
    public partial class BanchosucksWeeklyCard : CompositeDrawable
    {
        /// <summary>Milliseconds between two regular refreshes.</summary>
        public const double REFRESH_INTERVAL = 300_000;

        private const float panel_width = 380;
        private const float idle_alpha = 0.75f;

        private readonly BanchosucksRefreshGate gate = new BanchosucksRefreshGate(REFRESH_INTERVAL);

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        // absent in plain test scenes; the game always provides them
        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private BeatmapModelDownloader? downloader { get; set; }

        private IBindable<APIState> apiState = null!;
        private Bindable<bool> preferNoVideo = null!;

        private HeaderLine header = null!;
        private Container panel = null!;
        private Container trackerContainer = null!;

        private FillFlowContainer mapSection = null!;
        private TruncatingSpriteText mapTitle = null!;
        private OsuSpriteText mapDetails = null!;
        private RoundedButton mapButton = null!;
        private OsuSpriteText mapMessage = null!;

        private FillFlowContainer goalSection = null!;
        private OsuSpriteText goalNumbers = null!;
        private OsuSpriteText goalDetails = null!;
        private Box goalBar = null!;

        private BeatmapDownloadTracker? tracker;
        private IBindable<DownloadState>? downloadState;
        private int trackedSetId;

        private APIRequest? lookupRequest;
        private IScreen? screen;

        private bool refreshWanted;
        private int pendingRequests;
        private bool anySucceeded;
        private double lastTextUpdate = double.MinValue;

        /// <summary>The map of the week as the server last sent it, or null.</summary>
        public BanchosucksMapOfTheWeek? Map { get; private set; }

        /// <summary>The community goal as the server last sent it, or null.</summary>
        public BanchosucksCommunityGoal? Goal { get; private set; }

        /// <summary>Whether the tile is shown at all (there is a map or a goal).</summary>
        public bool Shown { get; private set; }

        /// <summary>Whether the panel under the line is open.</summary>
        public bool Expanded { get; private set; }

        /// <summary>The one-line summary (tests).</summary>
        public string HeaderText => header.Text;

        /// <summary>The text on the map button (tests).</summary>
        public string MapButtonText => mapButton.Text.ToString();

        /// <summary>The line with the goal's numbers (tests).</summary>
        public string GoalText => goalNumbers.Text.ToString();

        /// <summary>The width of the goal bar from 0 to 1 the tile is heading for (tests).</summary>
        public double GoalFraction { get; private set; }

        /// <summary>The clock the "time left" texts run on (tests replace it).</summary>
        public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

        public BanchosucksWeeklyCard()
        {
            AutoSizeAxes = Axes.Both;
            Alpha = 0;

            // invisible until there is something to show, but updated all the same: the first request is made from Update
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            preferNoVideo = config.GetBindable<bool>(OsuSetting.PreferNoVideo);

            InternalChildren = new Drawable[]
            {
                trackerContainer = new Container(),
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        header = new HeaderLine
                        {
                            Alpha = idle_alpha,
                            Action = toggle,
                        },
                        panel = new Container
                        {
                            Width = panel_width,
                            AutoSizeAxes = Axes.Y,
                            Masking = true,
                            CornerRadius = 8,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.75f),
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 12),
                                    Padding = new MarginPadding(12),
                                    Children = new Drawable[]
                                    {
                                        mapSection = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 4),
                                            Children = new Drawable[]
                                            {
                                                sectionTitle(ClickerStrings.Text("Map of the week", "Map der Woche")),
                                                mapTitle = new TruncatingSpriteText
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                                },
                                                mapDetails = new OsuSpriteText
                                                {
                                                    Font = OsuFont.GetFont(size: 13),
                                                    Colour = Color4.White.Opacity(0.8f),
                                                },
                                                mapButton = new RoundedButton
                                                {
                                                    Size = new Vector2(220, 30),
                                                    Margin = new MarginPadding { Top = 4 },
                                                    Action = mapButtonClicked,
                                                },
                                                mapMessage = new OsuSpriteText
                                                {
                                                    Font = OsuFont.GetFont(size: 13),
                                                    Colour = colours.Orange1,
                                                    Alpha = 0,
                                                },
                                            },
                                        },
                                        goalSection = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 4),
                                            Children = new Drawable[]
                                            {
                                                sectionTitle(ClickerStrings.Text("Community goal of the week", "Community-Ziel der Woche")),
                                                goalNumbers = new OsuSpriteText
                                                {
                                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                                },
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 8,
                                                    Masking = true,
                                                    CornerRadius = 4,
                                                    Children = new Drawable[]
                                                    {
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Colour = Color4.White.Opacity(0.2f),
                                                        },
                                                        goalBar = new Box
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Width = 0,
                                                        },
                                                    },
                                                },
                                                goalDetails = new OsuSpriteText
                                                {
                                                    Font = OsuFont.GetFont(size: 13),
                                                    Colour = Color4.White.Opacity(0.8f),
                                                },
                                            },
                                        },

                                        // MOD OF THE WEEK GOES HERE.
                                        // The plugin banchosucks_modweek has no HTTP endpoint yet (only the chat command !modweek).
                                        // Once GET /api/plugins/banchosucks_modweek/current exists, add a third section here:
                                        // the required mod, the three maps of the playlist room and the time left.
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private static Drawable sectionTitle(LocalisableString text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
            Colour = Color4Extensions.FromHex(@"ff4964"),
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screen = this.FindClosestParent<IScreen>();

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(_ => Schedule(updateDisplay), true);
        }

        protected override void Update()
        {
            base.Update();

            // a menu that is not the current screen keeps its children alive; the tile is quiet then
            bool current = screen?.IsCurrentScreen() ?? true;

            if (!current)
            {
                if (Expanded)
                    collapse();

                return;
            }

            if (apiState.Value == APIState.Online && gate.ShouldRefresh(Time.Current, refreshWanted))
                refresh();

            // "time left" moves on without a request
            if (Time.Current - lastTextUpdate >= 30_000 || Time.Current < lastTextUpdate)
                updateDisplay();
        }

        #region Data

        private void refresh()
        {
            refreshWanted = false;
            gate.Started(Time.Current);

            pendingRequests = 2;
            anySucceeded = false;

            var mapRequest = new GetBanchosucksMapOfTheWeekRequest();
            mapRequest.Success += response => Schedule(() =>
            {
                Map = response.Current;
                finished(true);
            });
            mapRequest.Failure += _ => Schedule(() => finished(false));

            var goalRequest = new GetBanchosucksCommunityGoalRequest();
            goalRequest.Success += response => Schedule(() =>
            {
                Goal = response.Goal;
                finished(true);
            });
            goalRequest.Failure += _ => Schedule(() => finished(false));

            api.Queue(mapRequest);
            api.Queue(goalRequest);
        }

        private void finished(bool success)
        {
            anySucceeded |= success;

            if (--pendingRequests > 0)
                return;

            if (anySucceeded)
                gate.Succeeded(Time.Current);
            else
                gate.Failed();

            updateDisplay();
        }

        #endregion

        #region Display

        private void updateDisplay()
        {
            lastTextUpdate = Time.Current;

            var now = Now();
            bool german = ClickerStrings.IsGerman;

            var map = BanchosucksWeekly.IsRunning(Map, now) ? Map : null;
            var goal = Goal != null && Goal.Target > 0 ? Goal : null;

            Shown = apiState.Value == APIState.Online && (map != null || goal != null);

            if (!Shown)
            {
                if (Expanded)
                    collapse();

                this.FadeOut(300, Easing.OutQuint);
                return;
            }

            this.FadeIn(300, Easing.OutQuint);

            // the one-line summary
            string summary = map != null ? ClickerStrings.Pick($"Map of the week: {map.Title}", $"Map der Woche: {map.Title}") : string.Empty;

            if (goal != null)
            {
                string goalPart = BanchosucksWeekly.Reached(goal)
                    ? ClickerStrings.Pick("community goal reached", "Community-Ziel geschafft")
                    : ClickerStrings.Pick($"community goal {BanchosucksWeekly.Percent(goal)} %", $"Community-Ziel {BanchosucksWeekly.Percent(goal)} %");

                summary = summary.Length > 0 ? $"{summary} · {goalPart}" : char.ToUpperInvariant(goalPart[0]) + goalPart.Substring(1);
            }

            header.Text = summary;

            // the map
            mapSection.Alpha = map != null ? 1 : 0;

            if (map != null)
            {
                mapTitle.Text = map.Title;

                string details = $"{map.Stars.ToString("0.00", ClickerStrings.IsGerman ? ClickerFormat.GermanCulture : ClickerFormat.EnglishCulture)}★";

                details += string.IsNullOrEmpty(map.NominatedBy)
                    ? ClickerStrings.Pick(" · most played map of last week", " · meistgespielte Map der letzten Woche")
                    : ClickerStrings.Pick($" · nominated by {map.NominatedBy}", $" · nominiert von {map.NominatedBy}");

                if (BanchosucksWeekly.Remaining(map.EndsAt, now) is TimeSpan left)
                    details += ClickerStrings.Pick($" · {BanchosucksWeekly.FormatRemaining(left, german)} left", $" · noch {BanchosucksWeekly.FormatRemaining(left, german)}");

                mapDetails.Text = details;
                track(map.BeatmapSetId);
                updateMapButton();
            }

            // the goal
            goalSection.Alpha = goal != null ? 1 : 0;

            if (goal != null)
            {
                bool reached = BanchosucksWeekly.Reached(goal);

                goalNumbers.Text = ClickerStrings.Pick(
                    $"{ClickerFormat.Count(goal.Total)} / {ClickerFormat.Count(goal.Target)} plays ({BanchosucksWeekly.Percent(goal)} %)",
                    $"{ClickerFormat.Count(goal.Total)} / {ClickerFormat.Count(goal.Target)} Plays ({BanchosucksWeekly.Percent(goal)} %)");
                goalNumbers.Colour = reached ? colours.GreenLight : Color4.White;

                string details = ClickerStrings.Pick(
                    $"stable {ClickerFormat.Count(goal.Stable)} + lazer {ClickerFormat.Count(goal.Lazer)} · {goal.Participants} players",
                    $"Stable {ClickerFormat.Count(goal.Stable)} + Lazer {ClickerFormat.Count(goal.Lazer)} · {goal.Participants} Spieler");

                if (reached)
                    details += ClickerStrings.Pick(" · reached, medal for everyone who plays", " · geschafft, Medaille für alle Mitspieler");
                else if (BanchosucksWeekly.Remaining(goal.EndsAt, now) is TimeSpan left)
                    details += ClickerStrings.Pick($" · {BanchosucksWeekly.FormatRemaining(left, german)} left", $" · noch {BanchosucksWeekly.FormatRemaining(left, german)}");

                goalDetails.Text = details;

                GoalFraction = BanchosucksWeekly.Fraction(goal);
                goalBar.Colour = reached ? colours.GreenLight : colours.Blue;
                goalBar.ResizeWidthTo((float)GoalFraction, 400, Easing.OutQuint);
            }
        }

        private void toggle()
        {
            if (Expanded)
                collapse();
            else
                expand();
        }

        private void expand()
        {
            if (!Shown)
                return;

            Expanded = true;
            panel.FadeIn(200, Easing.OutQuint);

            // opening the panel asks for fresh numbers (the gate keeps a minute between requests)
            refreshWanted = true;

            // a click anywhere else takes the focus away and closes the panel again
            GetContainingFocusManager()?.ChangeFocus(this);
        }

        private void collapse()
        {
            Expanded = false;
            panel.FadeOut(200, Easing.OutQuint);
            mapMessage.FadeOut();
        }

        public override bool AcceptsFocus => Expanded;

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);

            if (Expanded)
                collapse();
        }

        // clicks inside the open panel are the tile's own and must not reach the menu behind it
        protected override bool OnClick(ClickEvent e) => Expanded;

        public override bool HandlePositionalInput => Shown && base.HandlePositionalInput;

        #endregion

        #region Map button

        private void track(int setId)
        {
            if (setId == trackedSetId)
                return;

            trackedSetId = setId;
            downloadState?.UnbindAll();
            trackerContainer.Clear();

            if (setId <= 0)
            {
                tracker = null;
                downloadState = null;
                return;
            }

            trackerContainer.Add(tracker = new BeatmapDownloadTracker(new APIBeatmapSet { OnlineID = setId }));
            downloadState = tracker.State.GetBoundCopy();
            downloadState.BindValueChanged(_ => Schedule(updateMapButton), true);
        }

        private void updateMapButton()
        {
            bool looking = lookupRequest != null;

            switch (downloadState?.Value)
            {
                case DownloadState.LocallyAvailable:
                    mapButton.Text = ClickerStrings.Text("Show in song select", "In der Songauswahl zeigen");
                    mapButton.Enabled.Value = game != null;
                    break;

                case DownloadState.Downloading:
                case DownloadState.Importing:
                    mapButton.Text = ClickerStrings.Text("Downloading...", "Lädt...");
                    mapButton.Enabled.Value = false;
                    break;

                default:
                    mapButton.Text = looking ? ClickerStrings.Text("Downloading...", "Lädt...") : ClickerStrings.Text("Download", "Herunterladen");
                    mapButton.Enabled.Value = !looking && downloader != null;
                    break;
            }
        }

        private void mapButtonClicked()
        {
            if (Map is not BanchosucksMapOfTheWeek map)
                return;

            mapMessage.FadeOut();

            if (downloadState?.Value == DownloadState.LocallyAvailable)
            {
                int beatmapId = map.BeatmapId;

                collapse();
                game?.PresentBeatmap(new APIBeatmapSet { OnlineID = map.BeatmapSetId }, b => b.OnlineID == beatmapId);
                return;
            }

            if (downloader == null || lookupRequest != null)
                return;

            // the standard beatmap set lookup gives the download its title and tells whether the set exists at all
            var request = new GetBeatmapSetRequest(map.BeatmapSetId);
            lookupRequest = request;

            request.Success += set => Schedule(() =>
            {
                lookupRequest = null;
                downloader.Download(set, preferNoVideo.Value);
                updateMapButton();
            });

            request.Failure += _ => Schedule(() =>
            {
                lookupRequest = null;
                mapMessage.Text = ClickerStrings.Text("The map could not be found on the server.", "Die Map wurde auf dem Server nicht gefunden.");
                mapMessage.FadeIn(200);
                updateMapButton();
            });

            updateMapButton();
            api.Queue(request);
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            lookupRequest?.Cancel();
            base.Dispose(isDisposing);
        }

        /// <summary>
        /// The clickable one-line summary.
        /// </summary>
        private partial class HeaderLine : OsuClickableContainer
        {
            private TruncatingSpriteText text = null!;

            public string Text
            {
                get => text.Text.ToString();
                set => text.Text = value;
            }

            public HeaderLine()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                TooltipText = ClickerStrings.Text("This week on Banchosucks. Click for the map of the week and the community goal.",
                    "Diese Woche auf Banchosucks. Klick für die Map der Woche und das Community-Ziel.");

                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.CalendarWeek,
                            Size = new Vector2(12),
                            Shadow = true,
                        },
                        text = new TruncatingSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                            Shadow = true,
                            MaxWidth = 420,
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeTo(1, 150, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeTo(idle_alpha, 300, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}

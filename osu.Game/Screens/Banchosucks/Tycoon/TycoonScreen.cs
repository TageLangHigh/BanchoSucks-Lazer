// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    /// <summary>
    /// Banchosucks: the Private Server Tycoon, built like a tycoon game. The plot is the game: buy pads on the floor
    /// show what the next building costs and turn green when the cash is there; marketing buildings are the droppers,
    /// players walk out of them along the walkway into the server block, the server block grows with every hardware
    /// upgrade, donations pile up in the collector box until the player clicks it (or buys the auto collector). Too many
    /// players for the hardware means lag: reputation falls and players leave. Events (cheater waves, DDoS, broken
    /// deploys, a streamer) shake things up; features make the bad ones harmless. A season restart trades the run for
    /// legacy points. The save lives on the lazer server (plugin <c>banchosucks_tycoon</c>, which checks it for
    /// plausibility) and as a local file for offline play.
    /// </summary>
    public partial class TycoonScreen : OsuScreen
    {
        private const double autosave_interval = 30_000;
        private const double local_save_interval = 10_000;
        private const string local_file = "banchosucks-tycoon.json";
        private const float side_width = 440;
        private const int log_lines = 6;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private TycoonState state = new TycoonState();
        private readonly TycoonBoosts boosts = new TycoonBoosts();
        private readonly Random rng = new Random();

        private bool ready;
        private double nextEventIn = 45;
        private TycoonEvent? pending;
        private double lastCloudSave;
        private double lastLocalSave;
        private bool dirty;
        private int logCounter;
        private double statsRefresh;
        private bool firstMilestonePass = true;
        private readonly HashSet<string> reachedMilestones = new HashSet<string>();
        private GetTycoonSaveRequest? loadRequest;
        private SubmitTycoonSaveRequest? saveRequest;

        private Container plotHost = null!;
        private TycoonPlot plot = null!;
        private Container effectLayer = null!;
        private OsuSpriteText serverNameText = null!;
        private OsuSpriteText seasonText = null!;
        private OsuSpriteText cashText = null!;
        private OsuSpriteText rateText = null!;
        private OsuSpriteText boxText = null!;
        private OsuSpriteText playersText = null!;
        private OsuSpriteText capacityText = null!;
        private Box loadBar = null!;
        private OsuSpriteText rufText = null!;
        private Box rufBar = null!;
        private OsuSpriteText boostText = null!;
        private OsuSpriteText nextEventText = null!;
        private PostButton postButton = null!;
        private EventCard eventCard = null!;
        private FillFlowContainer logFlow = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText prestigeText = null!;
        private ClickerPillButton prestigeButton = null!;
        private FillFlowContainer statsFlow = null!;
        private QuestCard questCard = null!;
        private TycoonLeaderboardPanel board = null!;
        private readonly Dictionary<Tab, ClickerTabButton> tabButtons = new Dictionary<Tab, ClickerTabButton>();
        private readonly Dictionary<Tab, Drawable> tabContents = new Dictionary<Tab, Drawable>();
        private Tab tab = Tab.Stats;

        private enum Tab
        {
            Stats,
            Board,
        }

        private static string n(double value) => ClickerFormat.Number(value);

        private static LocalisableString t(string en, string de, params object[] args) => ClickerStrings.Text(en, de, args);

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("0b1020"), Color4Extensions.FromHex("151a2e")),
                },
                new Triangles
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = Color4Extensions.FromHex("2a3060"),
                    ColourDark = Color4Extensions.FromHex("12162b"),
                    TriangleScale = 2.5f,
                    Velocity = 0.4f,
                    Alpha = 0.5f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 50, Bottom = 20, Left = 24, Right = side_width + 40 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(12, 0),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = t("Private Server Tycoon", "Privatserver-Tycoon"),
                                    Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                serverNameText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold),
                                    Colour = colours.Pink,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                seasonText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 15),
                                    Colour = colours.Gray9,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                            },
                        },
                        plotHost = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 44 },
                            Child = plot = new TycoonPlot(this, colours),
                        },
                    },
                },
                new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = side_width,
                    RelativeSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Top = 50, Bottom = 20, Right = 24 },
                    Child = createSidePanel(),
                },
                effectLayer = new Container { RelativeSizeAxes = Axes.Both },
            };

            selectTab(Tab.Stats);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            serverNameText.Text = serverName();
            plot.SetOwner(serverName());
            beginLoading();
        }

        // ---------------------------------------------------------------- side panel

        private Drawable createSidePanel()
        {
            OsuSpriteText label(LocalisableString text) => new OsuSpriteText { Text = text, Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold), Colour = colours.Gray9 };
            OsuSpriteText small() => new OsuSpriteText { Font = OsuFont.GetFont(size: 12), Colour = colours.Gray9 };

            Drawable bar(out Box fill, Color4 colour) => new CircularContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 7,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f) },
                    fill = new Box { RelativeSizeAxes = Axes.Both, Width = 0, Colour = colour },
                },
            };

            var loadBarDrawable = bar(out loadBar, colours.Blue);
            var rufBarDrawable = bar(out rufBar, colours.Green);

            var tabRow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                Height = 26,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
            };

            foreach (var (value, text, icon) in new[]
                     {
                         (Tab.Stats, t("Stats", "Statistik"), FontAwesome.Solid.ChartBar),
                         (Tab.Board, t("Ranking", "Rangliste"), FontAwesome.Solid.Trophy),
                     })
            {
                var captured = value;
                tabRow.Add(tabButtons[value] = new ClickerTabButton(text, icon, colours.Pink, () => selectTab(captured), 14, 12));
            }

            tabContents[Tab.Stats] = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = true,
                Child = statsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Padding = new MarginPadding { Right = 6 },
                },
            };
            tabContents[Tab.Board] = board = new TycoonLeaderboardPanel();

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 16,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("0d0e12").Opacity(0.78f) },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(14),
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 10),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 10),
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 10),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 3),
                                    Children = new Drawable[]
                                    {
                                        label(t("Cash", "Kasse")),
                                        cashText = new OsuSpriteText { Font = OsuFont.GetFont(size: 34, weight: FontWeight.Bold), Colour = colours.Yellow },
                                        rateText = small(),
                                        boxText = new OsuSpriteText { Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold), Colour = Color4Extensions.FromHex("ffd54f") },
                                        label(t("Players online", "Spieler online")).With(l => l.Margin = new MarginPadding { Top = 8 }),
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(8, 0),
                                            Children = new Drawable[]
                                            {
                                                playersText = new OsuSpriteText { Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold), Colour = colours.Blue, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                                capacityText = new OsuSpriteText { Font = OsuFont.GetFont(size: 12), Colour = colours.Gray9, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                            },
                                        },
                                        loadBarDrawable,
                                        rufText = label(string.Empty).With(l => l.Margin = new MarginPadding { Top = 6 }),
                                        rufBarDrawable,
                                        boostText = new OsuSpriteText { Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold), Colour = colours.Yellow },
                                        nextEventText = small(),
                                    },
                                },
                            },
                            new Drawable[] { Empty() },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 8),
                                    Children = new Drawable[]
                                    {
                                        postButton = new PostButton(post),
                                        questCard = new QuestCard(),
                                        eventCard = new EventCard(),
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Masking = true,
                                            CornerRadius = 10,
                                            Children = new Drawable[]
                                            {
                                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.04f) },
                                                logFlow = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Padding = new MarginPadding(8),
                                                    Spacing = new Vector2(0, 2),
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            new Drawable[] { Empty() },
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Absolute, 6), new Dimension() },
                                    Content = new[]
                                    {
                                        new Drawable[] { tabRow },
                                        new Drawable[] { Empty() },
                                        new Drawable[] { new Container { RelativeSizeAxes = Axes.Both, Children = tabContents.Values.ToArray() } },
                                    },
                                },
                            },
                            new Drawable[] { Empty() },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5),
                                    Children = new Drawable[]
                                    {
                                        prestigeText = small(),
                                        prestigeButton = new ClickerPillButton(t("Start a new season", "Neue Saison starten"), colours.Purple, askPrestige, FontAwesome.Solid.Redo),
                                        statusText = small(),
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private void selectTab(Tab value)
        {
            tab = value;
            foreach (var (key, button) in tabButtons) button.Active = key == value;
            foreach (var (key, content) in tabContents) content.Alpha = key == value ? 1 : 0;

            if (IsLoaded && value == Tab.Board) board.Refresh();
            if (IsLoaded && value == Tab.Stats) refreshStats();
        }

        // ---------------------------------------------------------------- game loop

        protected override void Update()
        {
            base.Update();

            // the plot keeps its 20:13 shape and fills whatever the window leaves
            float scale = Math.Min(plotHost.ChildSize.X / TycoonPlot.WIDTH, plotHost.ChildSize.Y / TycoonPlot.HEIGHT);
            if (scale > 0) plot.Scale = new Vector2(scale);

            if (!ready) return;

            double dt = Math.Min(0.25, Clock.ElapsedFrameTime / 1000.0);
            if (dt <= 0) return;

            boosts.Tick(dt);
            double before = state.Pending + state.Donations;
            TycoonRules.Step(state, dt, boosts.Inflow, boosts.Donation);
            plot.Earned(state.Pending + state.Donations - before);
            plot.Flow(TycoonRules.Inflow(state, boosts.Inflow), dt);
            dirty = true;
            tickSupply(dt);

            if (pending == null)
            {
                nextEventIn -= dt;
                if (nextEventIn <= 0) fireEvent();
            }

            if (Time.Current - lastLocalSave > local_save_interval)
                saveLocal();

            if (Time.Current - lastCloudSave > autosave_interval)
                saveCloud();

            if (tab == Tab.Stats && Time.Current - statsRefresh > 1000)
                refreshStats();

            refreshTexts();
        }

        private void refreshTexts()
        {
            int cap = TycoonRules.Capacity(state.Hardware);
            double load = state.Players / cap;
            bool lagging = load > TycoonRules.OVERLOAD_TOLERANCE;
            var tier = TycoonRules.HardwareAt(state.Hardware);

            cashText.Text = n(state.Donations);
            rateText.Text = ClickerStrings.Pick($"+{n(TycoonRules.DonationRate(state, boosts.Donation))}/s · {n(state.RunTotal)} this season", $"+{n(TycoonRules.DonationRate(state, boosts.Donation))}/s · {n(state.RunTotal)} diese Saison");
            boxText.Text = state.Has("autocollect")
                ? t("Auto collector: donations go straight to the cash", "Auto-Kasse: Spenden landen direkt in der Kasse")
                : ClickerStrings.Pick($"{n(state.Pending)} waiting in the collector box, click it", $"{n(state.Pending)} warten in der Spendenbox, klick sie an");
            playersText.Text = n(state.Players);
            playersText.Colour = lagging ? colours.Red : colours.Blue;
            capacityText.Text = ClickerStrings.Pick($"of {ClickerFormat.Count(cap)} · {ClickerStrings.Pick(tier.NameEn, tier.NameDe)} · +{n(TycoonRules.Inflow(state, boosts.Inflow))}/s",
                $"von {ClickerFormat.Count(cap)} · {ClickerStrings.Pick(tier.NameEn, tier.NameDe)} · +{n(TycoonRules.Inflow(state, boosts.Inflow))}/s");
            loadBar.Width = (float)Math.Clamp(load, 0, 1);
            loadBar.Colour = lagging ? colours.Red : load > 0.8 ? colours.Yellow : colours.Blue;
            rufText.Text = ClickerStrings.Pick($"REPUTATION {state.Ruf:0}/100{(lagging ? " · THE SERVER LAGS" : string.Empty)}", $"RUF {state.Ruf:0}/100{(lagging ? " · DER SERVER LAGGT" : string.Empty)}");
            rufBar.Width = (float)(state.Ruf / 100);
            rufBar.Colour = state.Ruf > 60 ? colours.Green : state.Ruf > 30 ? colours.Yellow : colours.Red;
            seasonText.Text = ClickerStrings.Pick($"season {state.Prestige + 1} · {state.Legacy} legacy (x{TycoonRules.LegacyMult(state.Legacy):0.00})", $"Saison {state.Prestige + 1} · {state.Legacy} Legacy (x{TycoonRules.LegacyMult(state.Legacy):0.00})");
            postButton.Detail = ClickerStrings.Pick($"+{n(TycoonRules.ClickValue(state))} players · Space", $"+{n(TycoonRules.ClickValue(state))} Spieler · Leertaste");

            var boostParts = new List<string>();
            if (boosts.InflowRemaining > 0) boostParts.Add(ClickerStrings.Pick($"inflow x{boosts.Inflow:0.#} ({boosts.InflowRemaining:0} s)", $"Zulauf x{boosts.Inflow:0.#} ({boosts.InflowRemaining:0} s)"));
            if (boosts.DonationRemaining > 0) boostParts.Add(ClickerStrings.Pick($"donations x{boosts.Donation:0.#} ({boosts.DonationRemaining:0} s)", $"Spenden x{boosts.Donation:0.#} ({boosts.DonationRemaining:0} s)"));
            boostText.Text = string.Join(" · ", boostParts);

            nextEventText.Text = pending == null
                ? t("Next event in {0} s", "Nächstes Ereignis in {0} s", Math.Max(0, (int)Math.Ceiling(nextEventIn)))
                : t("An event waits for your decision.", "Ein Ereignis wartet auf deine Entscheidung.");

            int gain = TycoonRules.LegacyGain(state.RunTotal);
            prestigeButton.SetEnabled(gain > 0);
            prestigeText.Text = gain > 0
                ? ClickerStrings.Pick($"A new season gives +{gain} legacy (everything else starts over).", $"Eine neue Saison bringt +{gain} Legacy (alles andere beginnt von vorn).")
                : ClickerStrings.Pick($"New season from {n(TycoonRules.PRESTIGE_MIN_TOTAL)} donations in a season ({n(state.RunTotal)} so far).", $"Neue Saison ab {n(TycoonRules.PRESTIGE_MIN_TOTAL)} Spenden in einer Saison (bisher {n(state.RunTotal)}).");

            plot.Refresh(state, load, ready, TycoonRules.DonationRate(state, boosts.Donation));
            questCard.Set(TycoonQuests.Current(state), state.Quest, TycoonQuests.All.Length, state);
            checkMilestones();
            checkQuest();
        }

        /// <summary>A post in the Discord: players join at once (button, Space or the Discord booth on the plot).</summary>
        private void post()
        {
            if (!ready) return;

            double added = TycoonRules.Click(state);
            postButton.Pulse(ClickerStrings.Pick($"+{n(added)}", $"+{n(added)}"));
            plot.Burst("discord", Math.Min(5, (int)Math.Ceiling(added)));
        }

        private void collect()
        {
            if (!ready || state.Pending < 0.5) return;

            double amount = TycoonRules.Collect(state);
            dirty = true;
            flyCoins(plot.CollectorScreenPosition, cashText.ScreenSpaceDrawQuad.Centre, Math.Clamp((int)Math.Log10(amount + 1) * 2 + 3, 3, 12));
            cashText.ScaleTo(1.12f, 60).Then().ScaleTo(1, 250, Easing.OutQuint);
            log(t("Collected {0} donations", "{0} Spenden eingesammelt", n(amount)), colours.Yellow);
        }

        private bool buy(Func<bool> purchase, LocalisableString? logLine)
        {
            if (!ready || !purchase()) return false;

            dirty = true;
            if (logLine != null) log(logLine.Value, colours.Green);
            refreshTexts();
            return true;
        }

        private void buyHardware()
        {
            int next = state.Hardware + 1;
            if (next >= TycoonRules.Hardware.Length) return;

            var tier = TycoonRules.Hardware[next];
            buy(() => TycoonRules.BuyHardware(state), t("Upgraded to {0}: room for {1} players", "Aufgerüstet auf {0}: Platz für {1} Spieler", ClickerStrings.Pick(tier.NameEn, tier.NameDe), ClickerFormat.Count(tier.Capacity)));
        }

        private void buyMarketing(string key)
        {
            var kind = TycoonRules.MarketingOf(key);
            int before = state.MarketingCount(key);
            if (buy(() => TycoonRules.BuyMarketing(state, key), before == 0 ? t("{0} built", "{0} gebaut", ClickerStrings.Pick(kind.NameEn, kind.NameDe)) : (LocalisableString?)null))
                plot.Burst(key, 2);
        }

        private void buyFeature(string key)
        {
            var feature = TycoonRules.FeatureOf(key);
            if (feature == null) return;

            buy(() => TycoonRules.BuyFeature(state, key), t("{0} is live", "{0} ist live", ClickerStrings.Pick(feature.Value.NameEn, feature.Value.NameDe)));
        }

        private void placeDecoration(int x, int y, string key)
        {
            buy(() => TycoonRules.Place(state, x, y, key), null);
        }

        private void removeDecoration(int x, int y)
        {
            if (!ready) return;

            if (TycoonRules.Remove(state, x, y))
            {
                dirty = true;
                refreshTexts();
            }
        }

        private void fireEvent()
        {
            var ev = TycoonEvents.Roll(state, rng, colours);
            state.Events++;
            plot.PlayEvent(ev.Key, ev.Colour == colours.Red || ev.Colour == colours.Yellow);
            nextEventIn = rng.Next((int)TycoonEvents.MIN_INTERVAL, (int)TycoonEvents.MAX_INTERVAL);

            if (ev.Choices.Count == 0)
            {
                var result = ev.Auto?.Invoke(state, boosts);
                eventCard.Show(ev, null);
                log(ev.Title, ev.Colour);
                if (result != null) log(result.Value, colours.Gray9);
                return;
            }

            pending = ev;
            eventCard.Show(ev, choice =>
            {
                var result = choice.Apply(state, boosts);
                pending = null;
                log(ev.Title, ev.Colour);
                log(result, colours.Gray9);
                eventCard.Dismiss();
            });
        }

        private void log(LocalisableString line, Color4 colour)
        {
            logFlow.Insert(++logCounter, new TruncatingSpriteText
            {
                Text = line,
                Font = OsuFont.GetFont(size: 12),
                Colour = colour,
                RelativeSizeAxes = Axes.X,
            });

            while (logFlow.Children.Count > log_lines)
                logFlow.Remove(logFlow.Children.First(), true);
        }

        private void flyCoins(Vector2 fromScreen, Vector2 toScreen, int count)
        {
            if (!effectLayer.IsLoaded) return;

            var from = effectLayer.ToLocalSpace(fromScreen);
            var to = effectLayer.ToLocalSpace(toScreen);

            for (int i = 0; i < count; i++)
            {
                var coin = new SpriteIcon
                {
                    Origin = Anchor.Centre,
                    Position = from + new Vector2((float)(rng.NextDouble() - 0.5) * 30, (float)(rng.NextDouble() - 0.5) * 30),
                    Size = new Vector2(16),
                    Icon = FontAwesome.Solid.Coins,
                    Colour = colours.Yellow,
                };
                effectLayer.Add(coin);
                coin.Delay(i * 40).MoveTo(to, 550, Easing.InQuad).FadeOut(550, Easing.In).Expire();
            }
        }

        private void askPrestige()
        {
            int gain = TycoonRules.LegacyGain(state.RunTotal);
            if (gain <= 0) return;

            void doPrestige()
            {
                int gained = TycoonRules.Prestige(state);
                if (gained <= 0) return;

                boosts.Clear();
                pending = null;
                eventCard.Dismiss();
                plot.Reset();
                log(t("Season {0} started with {1} legacy", "Saison {0} gestartet mit {1} Legacy", state.Prestige + 1, state.Legacy), colours.Purple);
                dirty = true;
                saveCloud(true);
            }

            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new ClickerConfirmDialog(
                    t("Start a new season?", "Neue Saison starten?"),
                    t("Hardware, marketing, features, the team, the plot and the cash start over. You keep +{0} legacy: +{1} % on donations and players for ever.",
                        "Hardware, Marketing, Features, Team, Bauplatz und Kasse beginnen von vorn. Du behältst +{0} Legacy: dauerhaft +{1} % auf Spenden und Zulauf.", gain, gain * 5),
                    t("Start the season", "Saison starten"), FontAwesome.Solid.Redo, doPrestige));
            }
            else
                doPrestige();
        }

        // ---------------------------------------------------------------- statistics and milestones

        private static readonly (string key, string en, string de, Func<TycoonState, bool> reached)[] milestones =
        {
            ("first_player", "First player online", "Erster Spieler online", s => s.MaxPlayers >= 1),
            ("hundred", "100 players at once", "100 Spieler gleichzeitig", s => s.MaxPlayers >= 100),
            ("thousand", "1,000 players at once", "1.000 Spieler gleichzeitig", s => s.MaxPlayers >= 1000),
            ("ten_thousand", "10,000 players at once", "10.000 Spieler gleichzeitig", s => s.MaxPlayers >= 10_000),
            ("vps", "Rented a real server", "Einen richtigen Server gemietet", s => s.Hardware >= 3),
            ("datacenter", "Own rack row in a data centre", "Eigene Rack-Reihe im Rechenzentrum", s => s.Hardware >= 7),
            ("features", "Six features live", "Sechs Features live", s => s.Features.Count >= 6),
            ("all_features", "Every feature live", "Alle Features live", s => s.Features.Count >= TycoonRules.Features.Length),
            ("furnished", "Five things on the plot", "Fünf Dinge auf dem Platz", s => s.Layout.Count >= 5),
            ("events", "Survived 25 events", "25 Ereignisse überstanden", s => s.Events >= 25),
            ("million", "A million donations", "Eine Million Spenden", s => s.LifetimeTotal >= 1_000_000),
            ("billion", "A billion donations", "Eine Milliarde Spenden", s => s.LifetimeTotal >= 1_000_000_000),
            ("season", "Second season", "Zweite Saison", s => s.Prestige >= 1),
            ("clicks", "1,000 Discord posts", "1.000 Discord-Posts", s => s.Clicks >= 1000),
            ("team", "Three people on the team", "Drei Leute im Team", s => TycoonRules.StaffTotal(s) >= 3),
            ("quests", "Ten quests done", "Zehn Aufträge erledigt", s => s.Quest >= 10),
        };

        private void checkMilestones()
        {
            for (int i = 0; i < milestones.Length; i++)
            {
                var (key, en, de, reached) = milestones[i];
                if (reachedMilestones.Contains(key) || !reached(state)) continue;

                reachedMilestones.Add(key);
                plot.LightTrophy(i, !firstMilestonePass);

                if (!firstMilestonePass)
                {
                    log(t("Milestone: {0}", "Meilenstein: {0}", ClickerStrings.Pick(en, de)), colours.Yellow);
                    plot.Announce(FontAwesome.Solid.Trophy, t("Milestone: {0}", "Meilenstein: {0}", ClickerStrings.Pick(en, de)), colours.Yellow);
                }
            }

            firstMilestonePass = false;
        }

        private void refreshStats()
        {
            statsRefresh = Time.Current;
            statsFlow.Clear();

            void row(LocalisableString label, string value) => statsFlow.Add(new StatRow(label, value));

            row(t("Lifetime donations", "Spenden insgesamt"), n(state.LifetimeTotal));
            row(t("This season", "Diese Saison"), n(state.RunTotal));
            row(t("Spent this season", "Diese Saison ausgegeben"), n(TycoonRules.Spent(state)));
            row(t("Comfort", "Gemütlichkeit"), $"+{(TycoonRules.ComfortMult(state.Layout) - 1) * 100:0.0} %");
            row(t("Player record", "Spieler-Rekord"), n(state.MaxPlayers));
            row(t("Seasons", "Saisons"), ClickerFormat.Count(state.Prestige + 1));
            row(t("Legacy", "Legacy"), $"{state.Legacy} (x{TycoonRules.LegacyMult(state.Legacy):0.00})");
            row(t("Events survived", "Ereignisse überstanden"), ClickerFormat.Count(state.Events));
            row(t("Discord posts", "Discord-Posts"), ClickerFormat.Count(state.Clicks));
            row(t("Team", "Team"), ClickerFormat.Count(TycoonRules.StaffTotal(state)));
            row(t("Quests done", "Aufträge erledigt"), $"{Math.Min(state.Quest, TycoonQuests.All.Length)}/{TycoonQuests.All.Length}");
            row(t("Supply drops caught", "Lieferungen gefangen"), ClickerFormat.Count(supplies));
            row(t("Play time", "Spielzeit"), ClickerFormat.Duration(state.PlaySeconds));

            statsFlow.Add(new OsuSpriteText
            {
                Text = t("Milestones", "Meilensteine"),
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                Colour = colours.Yellow,
                Margin = new MarginPadding { Top = 8 },
            });

            foreach (var (_, en, de, reached) in milestones)
                statsFlow.Add(new MilestoneRow(t(en, de), reached(state), colours));
        }

        // ---------------------------------------------------------------- saving and loading

        private class LocalSave
        {
            [JsonProperty("saved_at")] public long SavedAt { get; set; }
            [JsonProperty("state")] public TycoonState? State { get; set; }
        }

        private static long nowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private string serverName()
        {
            string name = api.IsLoggedIn ? api.LocalUser.Value.Username : ClickerStrings.Pick("guest", "gast");
            return $"{name.ToLowerInvariant()}.cc";
        }

        private LocalSave? readLocal()
        {
            try
            {
                if (!storage.Exists(local_file)) return null;

                using var stream = storage.GetStream(local_file);
                using var reader = new StreamReader(stream);
                return JsonConvert.DeserializeObject<LocalSave>(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Logger.Log($"Tycoon local save could not be read: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }
        }

        private void saveLocal()
        {
            lastLocalSave = Time.Current;
            if (!ready || !dirty) return;

            try
            {
                using var stream = storage.CreateFileSafely(local_file);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonConvert.SerializeObject(new LocalSave { SavedAt = nowUnix(), State = state }));
            }
            catch (Exception e)
            {
                Logger.Log($"Tycoon local save could not be written: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        private void beginLoading()
        {
            var local = readLocal();

            if (!api.IsLoggedIn)
            {
                begin(local?.State, local?.SavedAt);
                statusText.Text = t("Not logged in: the server is saved on this computer only.", "Nicht angemeldet: Der Server wird nur auf diesem Rechner gespeichert.");
                return;
            }

            statusText.Text = t("Loading the cloud save …", "Cloud-Save wird geladen …");
            var request = loadRequest = new GetTycoonSaveRequest();

            request.Success += response =>
            {
                if (IsDisposed || request != loadRequest) return;

                // the save that got further wins; a local one that is ahead goes straight to the cloud
                bool useLocal = local?.State != null && (response.Save == null || local.State.LifetimeTotal > response.Save.LifetimeTotal);
                begin(useLocal ? local!.State : response.Save, useLocal ? local!.SavedAt : response.UpdatedAt);
                statusText.Text = useLocal ? t("Local save loaded, syncing to the cloud …", "Lokalen Spielstand geladen, wird in die Cloud gesynct …") : t("Cloud save loaded.", "Cloud-Save geladen.");
                if (useLocal) saveCloud(true);
            };

            request.Failure += e =>
            {
                if (IsDisposed || request != loadRequest) return;

                Logger.Log($"Tycoon cloud save could not be loaded: {e.Message}", LoggingTarget.Network);
                begin(local?.State, local?.SavedAt);
                statusText.Text = t("Cloud not reachable, playing on the local save.", "Cloud nicht erreichbar, es läuft der lokale Spielstand.");
            };

            api.Queue(request);
        }

        private void begin(TycoonState? loaded, long? savedAt)
        {
            state = loaded ?? new TycoonState();
            state.Marketing ??= TycoonRules.Marketing.ToDictionary(m => m.Key, _ => 0);
            state.Features ??= new List<string>();
            state.Layout ??= new List<TycoonPlacement>();
            state.Staff ??= TycoonRules.Staff.ToDictionary(s => s.Key, _ => 0);

            if (loaded == null)
            {
                log(t("Welcome. A Raspberry Pi, no players, big plans. Buy the Discord pad, click the booth to post, empty the collector box, follow the quests.",
                    "Willkommen. Ein Raspberry Pi, keine Spieler, große Pläne. Kauf das Discord-Pad, klick die Bude zum Posten, leer die Spendenbox, folge den Aufträgen."), colours.Pink);
            }
            else if (savedAt != null)
            {
                double away = Math.Min(TycoonRules.OFFLINE_CAP, Math.Max(0, nowUnix() - savedAt.Value));

                if (away >= 10)
                {
                    double gained = TycoonRules.Simulate(state, away);
                    log(t("While you were away ({0}): +{1} donations, {2} players online now",
                        "Während du weg warst ({0}): +{1} Spenden, jetzt {2} Spieler online", ClickerFormat.Duration(away), n(gained), n(state.Players)), colours.Yellow);
                    eventCard.Show(new TycoonEvent("welcome", t("Welcome back", "Willkommen zurück"),
                        state.Has("autocollect")
                            ? t("{0} away: your server took {1} donations, {2} players are online.", "{0} weg: Dein Server hat {1} Spenden eingenommen, {2} Spieler sind online.", ClickerFormat.Duration(away), n(gained), n(state.Players))
                            : t("{0} away: {1} donations wait in the collector box, {2} players are online.", "{0} weg: {1} Spenden warten in der Spendenbox, {2} Spieler sind online.", ClickerFormat.Duration(away), n(state.Pending), n(state.Players)),
                        FontAwesome.Solid.Clock, colours.Yellow), null);
                }
            }

            ready = true;
            dirty = true;
            firstMilestonePass = true;
            reachedMilestones.Clear();
            lastCloudSave = Time.Current;
            lastLocalSave = Time.Current;
            nextEventIn = rng.Next(40, 90);
            plot.Reset();
            refreshTexts();
        }

        private void saveCloud(bool force = false)
        {
            lastCloudSave = Time.Current;
            if (!ready || !api.IsLoggedIn || (!dirty && !force) || saveRequest != null) return;

            var request = saveRequest = new SubmitTycoonSaveRequest(state);

            request.Success += response =>
            {
                if (IsDisposed) return;

                saveRequest = null;

                if (!response.Accepted)
                {
                    statusText.Text = ClickerStrings.Pick($"Cloud save refused ({response.Reason}).", $"Cloud-Save abgelehnt ({response.Reason}).");
                    return;
                }

                dirty = false;

                if (response.Clamped && response.LifetimeTotal != null)
                {
                    state.LifetimeTotal = Math.Min(state.LifetimeTotal, response.LifetimeTotal.Value);
                    statusText.Text = t("Saved; the server trimmed implausible earnings.", "Gespeichert; der Server hat unplausible Einnahmen gekürzt.");
                }
                else
                {
                    statusText.Text = ClickerStrings.Pick($"Saved · rank #{response.RankTotal} by donations, #{response.RankPlayers} by players",
                        $"Gespeichert · Rang #{response.RankTotal} nach Spenden, #{response.RankPlayers} nach Spielern");
                }
            };

            request.Failure += e =>
            {
                if (IsDisposed) return;

                saveRequest = null;
                if (e is OperationCanceledException) return;

                Logger.Log($"Tycoon cloud save failed: {e.Message}", LoggingTarget.Network);
                statusText.Text = t("Cloud save failed, trying again later.", "Cloud-Save fehlgeschlagen, nächster Versuch später.");
            };

            api.Queue(request);
        }

        // ---------------------------------------------------------------- screen

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!e.Repeat && e.Key == Key.Space)
            {
                post();
                return true;
            }

            if (!e.Repeat && (e.Key == Key.C || e.Key == Key.Enter))
            {
                collect();
                return true;
            }

            return base.OnKeyDown(e);
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
            saveLocal();
            saveCloud();
            this.FadeOut(200);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            dirty = true;
            saveLocal();
            saveCloud(true);
            loadRequest?.Cancel();
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        // ================================================================ the plot

        /// <summary>
        /// The tycoon plot: 20 x 13 cells. The gate on the left, the walkway in the middle, the droppers (marketing
        /// buildings) above and below it, the server block and the collector on the right, feature kiosks along the
        /// bottom, decoration pads along the top. Pads appear in order as the player progresses.
        /// </summary>
        private partial class TycoonPlot : CompositeDrawable
        {
            public const float CELL = 40;
            public const float WIDTH = CELL * TycoonRules.OFFICE_WIDTH;
            public const float HEIGHT = CELL * TycoonRules.OFFICE_HEIGHT;

            private const float walk_y = 6.5f;
            private const float walk_end_x = 15.6f;

            private static readonly (string key, int x, int y, bool above)[] dropper_slots =
            {
                ("discord", 2, 3, true),
                ("reddit", 5, 3, true),
                ("youtube", 8, 3, true),
                ("streamer", 11, 3, true),
                ("tournament", 14, 3, true),
                ("campaign", 2, 8, false),
                ("influencer", 5, 8, false),
            };

            private readonly TycoonScreen screen;
            private readonly Container floor;
            private readonly Container structures;
            private readonly Container figures;
            private readonly Container coins;
            private readonly Container pads;
            private readonly Collector collector;
            private readonly Dictionary<string, Dropper> droppers = new Dictionary<string, Dropper>();
            private readonly Dictionary<string, BuyPad> marketingPads = new Dictionary<string, BuyPad>();
            private readonly Dictionary<string, BuyPad> featurePads = new Dictionary<string, BuyPad>();
            private readonly Dictionary<string, FeatureKiosk> kiosks = new Dictionary<string, FeatureKiosk>();
            private readonly Dictionary<(int x, int y), BuyPad> decoPads = new Dictionary<(int, int), BuyPad>();
            private readonly Dictionary<(int x, int y), DecoItem> decoItems = new Dictionary<(int, int), DecoItem>();
            private readonly BuyPad hardwarePad;
            private readonly ServerBlock servers;
            private readonly Random rng = new Random();
            private double figureAccumulator;
            private double coinAccumulator;
            private int lastHardware = -1;
            private int lastFeatureCount = -1;
            private readonly Dictionary<string, int> lastMarketing = new Dictionary<string, int>();

            private readonly OsuColour colours;

            public Vector2 CollectorScreenPosition => collector.ScreenSpaceDrawQuad.Centre;

            // colours come in through the constructor: the pads are built here, before dependencies are injected
            public TycoonPlot(TycoonScreen screen, OsuColour colours)
            {
                this.screen = screen;
                this.colours = colours;
                Size = new Vector2(WIDTH, HEIGHT);
                Anchor = Anchor.TopCentre;
                Origin = Anchor.TopCentre;
                Masking = true;
                CornerRadius = 14;
                BorderThickness = 5;
                BorderColour = Color4Extensions.FromHex("4B974B");

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("4B974B") },
                    floor = new Container { RelativeSizeAxes = Axes.Both },
                    new Walkway { Position = cell(2, 6), Size = new Vector2(CELL * 14, CELL) },
                    new Gate { Position = cell(0, 5), Size = new Vector2(CELL * 2, CELL * 3) },
                    structures = new Container { RelativeSizeAxes = Axes.Both },
                    pads = new Container { RelativeSizeAxes = Axes.Both },
                    figures = new Container { RelativeSizeAxes = Axes.Both },
                    coins = new Container { RelativeSizeAxes = Axes.Both },
                };

                for (int y = 0; y < TycoonRules.OFFICE_HEIGHT; y++)
                {
                    for (int x = 0; x < TycoonRules.OFFICE_WIDTH; x++)
                    {
                        floor.Add(new Box
                        {
                            Position = cell(x, y),
                            Size = new Vector2(CELL),
                            Colour = (x + y) % 2 == 0 ? Color4Extensions.FromHex("222838") : Color4Extensions.FromHex("1e2434"),
                        });
                    }
                }

                structures.Add(servers = new ServerBlock { Position = cell(16, 2), Size = new Vector2(CELL * 4, CELL * 7) });
                structures.Add(collector = new Collector(() => screen.collect()) { Position = cell(16, 10), Size = new Vector2(CELL * 4, CELL * 2) });
                pads.Add(hardwarePad = new BuyPad(FontAwesome.Solid.Server, colours.Blue, () => screen.buyHardware()) { Position = cell(16, 9), Size = new Vector2(CELL * 4, CELL) });

                foreach (var (key, x, y, above) in dropper_slots)
                {
                    var captured = key;
                    var dropper = new Dropper(key, above, () => { if (captured == "discord") screen.post(); }) { Position = cell(x, y), Size = new Vector2(CELL * 2, CELL * 2) };
                    droppers[key] = dropper;
                    structures.Add(dropper);
                    var pad = new BuyPad(marketingIcon(key), colours.Green, () => screen.buyMarketing(captured)) { Position = cell(x, above ? y - 1 : y + 2), Size = new Vector2(CELL * 2, CELL) };
                    marketingPads[key] = pad;
                    pads.Add(pad);
                }

                for (int i = 0; i < TycoonRules.Features.Length; i++)
                {
                    var feature = TycoonRules.Features[i];
                    int x = (i % 8) * 2, y = 11 + i / 8;
                    var kiosk = new FeatureKiosk(feature.Key) { Position = cell(x, y), Size = new Vector2(CELL * 2, CELL), Alpha = 0 };
                    kiosks[feature.Key] = kiosk;
                    structures.Add(kiosk);
                    var captured = feature.Key;
                    var pad = new BuyPad(featureIcon(feature.Key), colours.Purple, () => screen.buyFeature(captured)) { Position = cell(x, y), Size = new Vector2(CELL * 2, CELL) };
                    featurePads[feature.Key] = pad;
                    pads.Add(pad);
                }

                for (int i = 0; i < TycoonRules.Decorations.Length && i < 10; i++)
                {
                    var deco = TycoonRules.Decorations[i];
                    int x = i * 2, y = 0;
                    var pad = new BuyPad(DecoItem.IconOf(deco.Key), colours.Yellow, () => screen.placeDecoration(x, y, deco.Key)) { Position = cell(x, y), Size = new Vector2(CELL * 2, CELL) };
                    decoPads[(x, y)] = pad;
                    pads.Add(pad);
                }

                buildExtras();
            }

            private static Vector2 cell(float x, float y) => new Vector2(x * CELL, y * CELL);

            /// <summary>Forgets figures and coins and redraws every building from the state on the next refresh (a new season, a loaded save).</summary>
            public void Reset()
            {
                figures.Clear();
                coins.Clear();
                lastHardware = -1;
                lastFeatureCount = -1;
                lastMarketing.Clear();
                resetExtras();
            }

            /// <summary>Brings pads, buildings and the collector in line with the state.</summary>
            public void Refresh(TycoonState state, double load, bool ready, double rate)
            {
                // hardware
                int next = state.Hardware + 1;

                if (next < TycoonRules.Hardware.Length)
                {
                    var tier = TycoonRules.Hardware[next];
                    hardwarePad.Set(ClickerStrings.Pick(tier.NameEn, tier.NameDe), ClickerStrings.Pick($"{ClickerFormat.Count(tier.Capacity)} players", $"{ClickerFormat.Count(tier.Capacity)} Spieler"), tier.Cost, ready && TycoonRules.CanAfford(state, tier.Cost), true);
                }
                else
                    hardwarePad.Set(string.Empty, string.Empty, 0, false, false);

                if (state.Hardware != lastHardware)
                {
                    lastHardware = state.Hardware;
                    servers.SetTier(state.Hardware);
                }

                servers.SetLoad(load);

                // droppers and their pads: the next kind appears once the previous one stands
                for (int i = 0; i < dropper_slots.Length; i++)
                {
                    string key = dropper_slots[i].key;
                    var kind = TycoonRules.MarketingOf(key);
                    int count = state.MarketingCount(key);
                    bool unlocked = i == 0 || state.MarketingCount(dropper_slots[i - 1].key) > 0;
                    long cost = TycoonRules.MarketingCost(key, count);

                    string shortName = padName(key);
                    marketingPads[key].Set(
                        count == 0 ? shortName : $"{shortName} Lv {count + 1}",
                        ClickerStrings.Pick($"{kind.NameEn}, +{ClickerFormat.Number(kind.Rate)} players/s each", $"{kind.NameDe}, +{ClickerFormat.Number(kind.Rate)} Spieler/s je Stück"),
                        cost, ready && TycoonRules.CanAfford(state, cost), unlocked && count < TycoonRules.MAX_COUNT);

                    if (!lastMarketing.TryGetValue(key, out int shown) || shown != count)
                    {
                        lastMarketing[key] = count;
                        droppers[key].SetLevel(count);
                    }
                }

                // features: kiosks in order
                if (state.Features.Count != lastFeatureCount)
                {
                    lastFeatureCount = state.Features.Count;

                    foreach (var feature in TycoonRules.Features)
                        kiosks[feature.Key].FadeTo(state.Has(feature.Key) ? 1 : 0, 300);
                }

                for (int i = 0; i < TycoonRules.Features.Length; i++)
                {
                    var feature = TycoonRules.Features[i];
                    bool owned = state.Has(feature.Key);
                    bool unlocked = !owned && (i == 0 || state.Has(TycoonRules.Features[i - 1].Key));
                    featurePads[feature.Key].Set(featureShort(feature), $"{ClickerStrings.Pick(feature.NameEn, feature.NameDe)}. {ClickerStrings.Pick(feature.DescriptionEn, feature.DescriptionDe)}", feature.Cost, ready && TycoonRules.CanAfford(state, feature.Cost), unlocked);
                }

                // decorations: pads along the top once the first upgrade is bought, items where placed
                foreach (var ((x, y), pad) in decoPads)
                {
                    var placed = state.Layout.FirstOrDefault(p => p.X == x && p.Y == y);
                    var deco = TycoonRules.Decorations[x / 2];
                    pad.Set(ClickerStrings.Pick(deco.NameEn, deco.NameDe), ClickerStrings.Pick($"comfort +{deco.Comfort}", $"Gemütlichkeit +{deco.Comfort}"), deco.Cost, ready && TycoonRules.CanAfford(state, deco.Cost), placed == null && state.Hardware >= 1);

                    bool hasItem = decoItems.ContainsKey((x, y));

                    if (placed != null && !hasItem)
                    {
                        var item = new DecoItem(placed.Key, () => screen.removeDecoration(x, y)) { Position = cell(x, y), Size = new Vector2(CELL * 2, CELL) };
                        decoItems[(x, y)] = item;
                        structures.Add(item);
                        item.ScaleTo(0.6f).ScaleTo(1, 300, Easing.OutBack);
                    }
                    else if (placed == null && hasItem)
                    {
                        decoItems[(x, y)].Expire();
                        decoItems.Remove((x, y));
                    }
                }

                collector.SetAmount(state.Pending, state.Has("autocollect"), rate > 0 ? state.Pending / (rate * 90) : 0);
                refreshExtras(state, load, ready);
            }

            /// <summary>Players joining: figures leave the droppers and walk to the servers, about one per player, at most eight a second.</summary>
            public void Flow(double inflow, double dt)
            {
                if (inflow <= 0) return;

                double perFigure = Math.Max(1, inflow / 8);
                figureAccumulator += inflow * dt;

                while (figureAccumulator >= perFigure)
                {
                    figureAccumulator -= perFigure;
                    spawnFigure(pickDropper());
                }
            }

            /// <summary>Donations came in: every so often a coin drops from the servers into the collector.</summary>
            public void Earned(double amount)
            {
                coinAccumulator += amount;
                double perCoin = Math.Max(1, amount * 40); // roughly one coin per 1.5 seconds at any income

                if (coinAccumulator >= perCoin && coins.Children.Count < 12)
                {
                    coinAccumulator = 0;
                    var coin = new SpriteIcon
                    {
                        Origin = Anchor.Centre,
                        Position = cell(18, 5.5f) + new Vector2((float)(rng.NextDouble() - 0.5) * 60, 0),
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.Coins,
                        Colour = colours.Yellow,
                    };
                    coins.Add(coin);
                    coin.MoveTo(cell(18, 11), 700, Easing.InQuad).FadeOut(700, Easing.In).Expire();
                    collector.Pulse();
                }
            }

            /// <summary>A handful of figures at once, from one dropper (a post, a purchase).</summary>
            public void Burst(string dropperKey, int count)
            {
                for (int i = 0; i < count; i++)
                    Scheduler.AddDelayed(() => spawnFigure(dropperKey), i * 90);
            }

            private string pickDropper()
            {
                double total = 0;
                var weights = new List<(string key, double w)>();

                foreach (var (key, _, _, _) in dropper_slots)
                {
                    double w = droppers[key].Level * TycoonRules.MarketingOf(key).Rate;
                    if (w <= 0) continue;

                    weights.Add((key, w));
                    total += w;
                }

                if (weights.Count == 0) return "discord";

                double roll = rng.NextDouble() * total;

                foreach (var (key, w) in weights)
                {
                    if (roll < w) return key;

                    roll -= w;
                }

                return weights[^1].key;
            }

            private void spawnFigure(string dropperKey)
            {
                if (figures.Children.Count > 60) return;

                var slot = dropper_slots.First(s => s.key == dropperKey);
                var start = cell(slot.x + 1, slot.above ? slot.y + 2 : slot.y) + new Vector2((float)(rng.NextDouble() - 0.5) * 30, 0);
                var figure = new Figure(rng) { Position = start };
                figures.Add(figure);
                if (rng.Next(14) == 0) figure.Say(chatter());

                var onWalkway = new Vector2(start.X, walk_y * CELL + (float)(rng.NextDouble() - 0.5) * 16);
                double walk = Math.Max(300, (walk_end_x * CELL - start.X) * 9);
                figure.MoveTo(onWalkway, 350, Easing.OutQuad)
                      .Then().MoveToX(walk_end_x * CELL, walk)
                      .Then().MoveTo(cell(17.5f, 6.5f), 500, Easing.InQuad).FadeOut(500).Expire();
            }

            private static IconUsage marketingIcon(string key) => key switch
            {
                "discord" => FontAwesome.Brands.Discord,
                "reddit" => FontAwesome.Brands.Reddit,
                "youtube" => FontAwesome.Brands.Youtube,
                "streamer" => FontAwesome.Brands.Twitch,
                "tournament" => FontAwesome.Solid.Trophy,
                "campaign" => FontAwesome.Solid.Bullhorn,
                _ => FontAwesome.Solid.Star,
            };

            private static IconUsage featureIcon(string key) => key switch
            {
                "website" => FontAwesome.Solid.Globe,
                "autocollect" => FontAwesome.Solid.Magnet,
                "leaderboards" => FontAwesome.Solid.ListOl,
                "medals" => FontAwesome.Solid.Medal,
                "discord_bot" => FontAwesome.Solid.Robot,
                "anticheat" => FontAwesome.Solid.UserShield,
                "backups" => FontAwesome.Solid.Database,
                "cloudflare" => FontAwesome.Solid.ShieldAlt,
                "lazer" => FontAwesome.Solid.Rocket,
                "multiplayer" => FontAwesome.Solid.Users,
                "supporter" => FontAwesome.Solid.Heart,
                "mirror" => FontAwesome.Solid.Download,
                _ => FontAwesome.Solid.Vial,
            };

            /// <summary>A buy pad like in a tycoon: name and price float over a pill button that is green when affordable and red when not; locked pads are hidden.</summary>
            private partial class BuyPad : OsuClickableContainer
            {
                private static readonly Color4 green = Color4Extensions.FromHex("4B974B");
                private static readonly Color4 red = Color4Extensions.FromHex("C4281C");
                private static readonly Color4 gold = Color4Extensions.FromHex("F5CD30");

                private readonly Container cap;
                private readonly Box capTop;
                private readonly Box capSide;
                private readonly OsuSpriteText nameText;
                private readonly OsuSpriteText priceText;
                private bool affordable;
                private bool shown = true;

                public BuyPad(IconUsage iconUsage, Color4 accent, Action action)
                {
                    Action = action;

                    Children = new Drawable[]
                    {
                        cap = new Container
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            RelativeSizeAxes = Axes.X,
                            Height = 13,
                            Padding = new MarginPadding { Horizontal = 8 },
                            Children = new Drawable[]
                            {
                                new CircularContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Y = 2.5f,
                                    Masking = true,
                                    Child = capSide = new Box { RelativeSizeAxes = Axes.Both, Colour = red.Darken(0.5f) },
                                },
                                new CircularContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    Child = capTop = new Box { RelativeSizeAxes = Axes.Both, Colour = red },
                                },
                            },
                        },
                        nameText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = -1,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                            Shadow = true,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 11,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(3, 0),
                            Children = new Drawable[]
                            {
                                new SpriteIcon { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Size = new Vector2(10), Icon = iconUsage, Colour = accent },
                                priceText = new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold), Colour = gold, Shadow = true },
                            },
                        },
                    };
                }

                public void Set(string name, string detail, long cost, bool canAfford, bool visible)
                {
                    if (visible != shown)
                    {
                        shown = visible;
                        this.FadeTo(visible ? 1 : 0, 250);
                        if (visible) this.ScaleTo(0.9f).ScaleTo(1, 300, Easing.OutBack);
                    }

                    nameText.Text = name;
                    TooltipText = string.IsNullOrEmpty(detail) ? name : $"{name}: {detail}";
                    priceText.Text = cost == 0 ? ClickerStrings.Pick("free", "gratis") : $"$ {ClickerFormat.Number(cost)}";
                    Enabled.Value = visible && canAfford;

                    if (canAfford != affordable)
                    {
                        affordable = canAfford;
                        var colour = canAfford ? green : red;
                        capTop.FadeColour(colour, 200);
                        capSide.FadeColour(colour.Darken(0.5f), 200);
                        priceText.FadeColour(canAfford ? gold : Color4.White.Opacity(0.7f), 200);
                        if (!canAfford) capTop.Alpha = 1;
                    }
                }

                protected override void Update()
                {
                    base.Update();

                    // long names shrink to the pad instead of being cut off
                    nameText.Scale = new Vector2(Math.Min(1, (DrawWidth - 8) / Math.Max(1, nameText.DrawWidth)));

                    if (affordable)
                        capTop.Alpha = 0.78f + 0.22f * (float)Math.Sin(Time.Current / 300);
                }

                protected override bool OnHover(HoverEvent e)
                {
                    if (Enabled.Value) cap.MoveToY(-2, 100, Easing.OutQuint);
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    cap.MoveToY(0, 100, Easing.OutQuint);
                    base.OnHoverLost(e);
                }
            }

            /// <summary>Where the players come from: a bright blue block with a globe and a sign.</summary>
            private partial class Gate : CompositeDrawable
            {
                public Gate()
                {
                    InternalChild = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(4),
                        Masking = true,
                        CornerRadius = 6,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("0D69AC") },
                            new Box { RelativeSizeAxes = Axes.X, Height = 10, Colour = Color4Extensions.FromHex("3f8fd0") },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 6, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = Color4Extensions.FromHex("084a7a") },
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Y = -6,
                                Size = new Vector2(30),
                                Icon = FontAwesome.Solid.Globe,
                                Colour = Color4.White,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                Y = -8,
                                Text = "INTERNET",
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                Shadow = true,
                            },
                        },
                    };
                }
            }

            /// <summary>The conveyor the players ride on: a dark belt with hazard stripes along the edges and chevrons moving right.</summary>
            private partial class Walkway : CompositeDrawable
            {
                private readonly Container chevrons;

                public Walkway()
                {
                    var stripes = new Container { RelativeSizeAxes = Axes.Both };

                    InternalChildren = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("1B2A35") },
                        stripes,
                        chevrons = new Container { RelativeSizeAxes = Axes.Both, Masking = true, Padding = new MarginPadding { Vertical = 4 } },
                    };

                    for (int i = 0; i < 70; i++)
                    {
                        var colour = Color4Extensions.FromHex(i % 2 == 0 ? "F5CD30" : "1B2A35");
                        stripes.Add(new Box { X = i * 8, Size = new Vector2(8, 4), Colour = colour });
                        stripes.Add(new Box { X = i * 8, Size = new Vector2(8, 4), Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = colour });
                    }

                    for (int i = 0; i < 16; i++)
                    {
                        chevrons.Add(new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.Centre,
                            X = i * 40,
                            Size = new Vector2(12),
                            Icon = FontAwesome.Solid.ChevronRight,
                            Colour = Color4.White.Opacity(0.22f),
                        });
                    }
                }

                protected override void Update()
                {
                    base.Update();
                    float offset = (float)(Time.Current / 25 % 40);

                    for (int i = 0; i < chevrons.Children.Count; i++)
                        chevrons.Children[i].X = (i * 40 + offset) % (chevrons.Children.Count * 40) - 20;
                }
            }

            /// <summary>A marketing building, a blocky dropper: lighter top face, darker side, a nozzle the players come out of; it grows a little with every level.</summary>
            private partial class Dropper : OsuClickableContainer
            {
                public int Level { get; private set; }

                private readonly Container building;
                private readonly OsuSpriteText levelText;
                private readonly Box light;

                public Dropper(string key, bool above, Action click)
                {
                    Action = () =>
                    {
                        // the booth only works once it stands
                        if (Level > 0) click();
                    };
                    Padding = new MarginPadding(3);

                    Color4 colour = key switch
                    {
                        "discord" => Color4Extensions.FromHex("5865F2"),
                        "reddit" => Color4Extensions.FromHex("ff4500"),
                        "youtube" => Color4Extensions.FromHex("d32f2f"),
                        "streamer" => Color4Extensions.FromHex("9146ff"),
                        "tournament" => Color4Extensions.FromHex("c9a227"),
                        "campaign" => Color4Extensions.FromHex("00897b"),
                        _ => Color4Extensions.FromHex("ec407a"),
                    };

                    Child = building = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Masking = true,
                        CornerRadius = 5,
                        Alpha = 0,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.4f), Radius = 6, Offset = new Vector2(0, 3) },
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                            new Box { RelativeSizeAxes = Axes.X, Height = 11, Colour = colour.Lighten(0.35f) },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 7, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = colour.Darken(0.45f) },
                            light = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.12f), Alpha = 0 },
                            new Box
                            {
                                Anchor = above ? Anchor.BottomCentre : Anchor.TopCentre,
                                Origin = above ? Anchor.BottomCentre : Anchor.TopCentre,
                                Size = new Vector2(16, 7),
                                Colour = Color4Extensions.FromHex("1B2A35"),
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Y = 1,
                                Size = new Vector2(24),
                                Icon = marketingIcon(key),
                                Colour = Color4.White,
                            },
                            levelText = new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Position = new Vector2(5, -3),
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                Shadow = true,
                            },
                        },
                    };
                }

                public void SetLevel(int level)
                {
                    bool appeared = Level == 0 && level > 0;
                    Level = level;
                    levelText.Text = level > 0 ? $"Lv {level}" : string.Empty;
                    building.FadeTo(level > 0 ? 1 : 0, 300);

                    if (appeared)
                        building.ScaleTo(0.5f).ScaleTo(1, 400, Easing.OutBack);
                    else
                        building.ScaleTo(level > 0 ? 1 + Math.Min(level, 25) * 0.006f : 0.6f, 350, Easing.OutBack);

                    if (level > 0) light.FadeTo(0.5f, 60).Then().FadeTo(0, 400);
                }

                protected override void Update()
                {
                    base.Update();
                    if (Level > 0 && !light.Transforms.Any())
                        light.Alpha = 0.05f + 0.05f * (float)Math.Sin(Time.Current / 600);
                }
            }

            /// <summary>The server block: from a Pi on a desk to rows of racks; LEDs blink faster with load, red when lagging.</summary>
            private partial class ServerBlock : CompositeDrawable
            {
                private readonly Container racks;
                private readonly OsuSpriteText nameText;
                private readonly Box glow;
                private readonly List<Box> leds = new List<Box>();
                private double load;

                [Resolved]
                private OsuColour colours { get; set; } = null!;

                public ServerBlock()
                {
                    Padding = new MarginPadding(4);

                    InternalChild = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 10,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.4f), Radius = 6, Offset = new Vector2(0, 3) },
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("151926") },
                            new Box { RelativeSizeAxes = Axes.X, Height = 20, Colour = Color4Extensions.FromHex("343c5c") },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 6, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = Color4Extensions.FromHex("0b0e18") },
                            glow = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("ff4040").Opacity(0.2f), Alpha = 0 },
                            racks = new Container { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Top = 22, Bottom = 8, Horizontal = 8 } },
                            nameText = new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Y = 5,
                                Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                                Colour = Color4.White.Opacity(0.85f),
                            },
                        },
                    };
                }

                public void SetTier(int tier)
                {
                    racks.Clear();
                    leds.Clear();
                    var hardware = TycoonRules.HardwareAt(tier);
                    nameText.Text = ClickerStrings.Pick(hardware.NameEn, hardware.NameDe);

                    // how many rack units stand in the room, and how they are arranged
                    int units = tier switch { 0 => 1, 1 => 1, 2 => 1, 3 => 2, 4 => 3, 5 => 4, 6 => 6, 7 => 8, _ => 8 };
                    int columns = units <= 1 ? 1 : units <= 4 ? 2 : units <= 6 ? 3 : 4;
                    int rows = (int)Math.Ceiling(units / (double)columns);
                    float unitW = (CELL * 4 - 24 - (columns - 1) * 6) / columns;
                    float unitH = Math.Min(70, (CELL * 7 - 38 - (rows - 1) * 6) / rows);

                    for (int i = 0; i < units; i++)
                    {
                        int c = i % columns, r = i / columns;
                        var unit = new Container
                        {
                            Position = new Vector2(c * (unitW + 6), r * (unitH + 6)),
                            Size = new Vector2(unitW, unitH),
                            Masking = true,
                            CornerRadius = 4,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = tier <= 2 ? Color4Extensions.FromHex("3b4256") : Color4Extensions.FromHex("2a2f42") },
                            },
                        };

                        if (tier == 0)
                            unit.Add(new SpriteIcon { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(22), Icon = FontAwesome.Solid.Microchip, Colour = Color4Extensions.FromHex("7fd47f") });
                        else if (tier == 1)
                            unit.Add(new SpriteIcon { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(24), Icon = FontAwesome.Solid.Laptop, Colour = Color4.White.Opacity(0.9f) });
                        else if (tier == 2)
                            unit.Add(new SpriteIcon { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(24), Icon = FontAwesome.Solid.Hdd, Colour = Color4.White.Opacity(0.9f) });
                        else
                        {
                            // a rack: horizontal slots with an LED each
                            int slots = Math.Max(2, (int)(unitH / 12));

                            for (int s = 0; s < slots; s++)
                            {
                                unit.Add(new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 8,
                                    Y = 3 + s * 12,
                                    Padding = new MarginPadding { Horizontal = 3 },
                                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("1a1e2b") },
                                });
                                var led = new Box { Size = new Vector2(4), Position = new Vector2(unitW - 9, 5 + s * 12), Colour = colours.Green };
                                leds.Add(led);
                                unit.Add(led);
                            }

                            if (tier >= 8)
                                unit.Add(new SpriteIcon { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(18), Icon = FontAwesome.Solid.Cloud, Colour = Color4.White.Opacity(0.35f) });
                        }

                        racks.Add(unit);
                        unit.ScaleTo(0.7f).ScaleTo(1, 300 + i * 40, Easing.OutBack);
                    }
                }

                public void SetLoad(double value)
                {
                    load = value;
                    glow.FadeTo(value > TycoonRules.OVERLOAD_TOLERANCE ? 1 : 0, 300);
                }

                protected override void Update()
                {
                    base.Update();
                    double speed = 1.5 + Math.Min(3, load) * 2.5;
                    var colour = load > TycoonRules.OVERLOAD_TOLERANCE ? colours.Red : load > 0.8 ? colours.Yellow : colours.Green;

                    for (int i = 0; i < leds.Count; i++)
                    {
                        leds[i].Alpha = Math.Sin(Time.Current / 1000 * speed * (1 + i % 5 * 0.31) + i * 1.3) > 0 ? 1 : 0.25f;
                        leds[i].Colour = colour;
                    }
                }
            }

            /// <summary>The collector: a bright green cash block, donations pile up in it until the player clicks it.</summary>
            private partial class Collector : OsuClickableContainer
            {
                private readonly OsuSpriteText amountText;
                private readonly OsuSpriteText hintText;
                private readonly Box glow;
                private readonly SpriteIcon icon;
                private readonly Box gauge;
                private bool auto;

                public Collector(Action collect)
                {
                    Action = collect;
                    Padding = new MarginPadding(3);

                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 6,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.4f), Radius = 6, Offset = new Vector2(0, 3) },
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("3d7a3d") },
                            new Box { RelativeSizeAxes = Axes.X, Height = 10, Colour = Color4Extensions.FromHex("5fb65f") },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 6, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = Color4Extensions.FromHex("2d5c2d") },
                            glow = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("F5CD30").Opacity(0.3f), Alpha = 0 },
                            icon = new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 12,
                                Y = 3,
                                Size = new Vector2(26),
                                Icon = FontAwesome.Solid.DollarSign,
                                Colour = Color4Extensions.FromHex("F5CD30"),
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 46,
                                Y = 3,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    amountText = new OsuSpriteText { Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold), Colour = Color4Extensions.FromHex("F5CD30"), Shadow = true },
                                    hintText = new OsuSpriteText { Font = OsuFont.GetFont(size: 9, weight: FontWeight.Bold), Colour = Color4.White.Opacity(0.9f), Shadow = true },
                                },
                            },
                            new Container
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -12,
                                Y = 3,
                                Size = new Vector2(10, 48),
                                Masking = true,
                                CornerRadius = 4,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.4f) },
                                    gauge = new Box { RelativeSizeAxes = Axes.Both, Height = 0, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = Color4Extensions.FromHex("F5CD30") },
                                },
                            },
                        },
                    };
                }

                /// <param name="fill">How full the box looks; 1 = about 90 seconds of income waiting.</param>
                public void SetAmount(double pending, bool autoCollect, double fill)
                {
                    auto = autoCollect;
                    amountText.Text = autoCollect ? "AUTO" : $"$ {ClickerFormat.Number(pending)}";
                    hintText.Text = autoCollect ? ClickerStrings.Text("COLLECTOR, AUTOMATIC", "SPENDENBOX, AUTOMATISCH") : ClickerStrings.Text("COLLECTOR, CLICK TO COLLECT", "SPENDENBOX, KLICKEN");
                    TooltipText = autoCollect ? ClickerStrings.Text("The auto collector empties the box for you", "Die Auto-Kasse leert die Box für dich") : ClickerStrings.Text("Click, C or Enter: collect the donations", "Klick, C oder Enter: Spenden einsammeln");
                    gauge.Height = (float)Math.Clamp(autoCollect ? 0 : fill, 0, 1);
                    Enabled.Value = !autoCollect && pending >= 0.5;
                }

                public void Pulse()
                {
                    icon.ScaleTo(1.15f, 80).Then().ScaleTo(1, 250, Easing.OutQuint);
                }

                protected override void Update()
                {
                    base.Update();
                    glow.Alpha = !auto && Enabled.Value ? 0.6f + 0.4f * (float)Math.Sin(Time.Current / 400) : 0;
                }
            }

            /// <summary>A feature, once bought: a small kiosk with its icon along the bottom.</summary>
            private partial class FeatureKiosk : CompositeDrawable
            {
                public FeatureKiosk(string key)
                {
                    Padding = new MarginPadding(3);
                    var feature = TycoonRules.FeatureOf(key);

                    InternalChild = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 8,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.35f), Radius = 4, Offset = new Vector2(0, 2) },
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("3d2a6b") },
                            new Box { RelativeSizeAxes = Axes.X, Height = 6, Colour = Color4Extensions.FromHex("9c7cf5") },
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 8,
                                Y = 2,
                                Size = new Vector2(14),
                                Icon = featureIcon(key),
                                Colour = Color4.White,
                            },
                            new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 28,
                                Y = 2,
                                Text = feature == null ? key : ClickerStrings.Text(feature.Value.NameEn, feature.Value.NameDe),
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                MaxWidth = 48,
                            },
                        },
                    };
                }
            }

            /// <summary>A decoration on its pad; right-click sells it for half.</summary>
            private partial class DecoItem : CompositeDrawable
            {
                private readonly Action remove;

                public static IconUsage IconOf(string key) => key switch
                {
                    "carpet" => FontAwesome.Solid.Th,
                    "plant" => FontAwesome.Solid.Seedling,
                    "desk" => FontAwesome.Solid.Desktop,
                    "poster" => FontAwesome.Solid.Image,
                    "coffee" => FontAwesome.Solid.Coffee,
                    "sofa" => FontAwesome.Solid.Couch,
                    "fridge" => FontAwesome.Solid.Snowflake,
                    "aircon" => FontAwesome.Solid.Wind,
                    _ => FontAwesome.Solid.Gamepad,
                };

                public static Color4 ColourOf(string key) => key switch
                {
                    "carpet" => Color4Extensions.FromHex("4a3b5c"),
                    "plant" => Color4Extensions.FromHex("2e7d32"),
                    "desk" => Color4Extensions.FromHex("8d6e63"),
                    "poster" => Color4Extensions.FromHex("5e35b1"),
                    "coffee" => Color4Extensions.FromHex("6d4c41"),
                    "sofa" => Color4Extensions.FromHex("ad1457"),
                    "fridge" => Color4Extensions.FromHex("78909c"),
                    "aircon" => Color4Extensions.FromHex("0288d1"),
                    _ => Color4Extensions.FromHex("ff7043"),
                };

                public DecoItem(string key, Action remove)
                {
                    this.remove = remove;
                    Padding = new MarginPadding(3);
                    var deco = TycoonRules.DecorationOf(key);

                    InternalChild = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 8,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.35f), Radius = 4, Offset = new Vector2(0, 2) },
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = ColourOf(key) },
                            new Box { RelativeSizeAxes = Axes.Both, Height = 0.35f, Colour = Color4.White.Opacity(0.08f) },
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 8,
                                Size = new Vector2(16),
                                Icon = IconOf(key),
                                Colour = Color4.White.Opacity(key == "carpet" ? 0.35f : 0.95f),
                            },
                            new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 30,
                                Text = deco == null ? key : ClickerStrings.Text(deco.Value.NameEn, deco.Value.NameDe),
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                MaxWidth = 44,
                            },
                        },
                    };
                }

                protected override bool OnMouseDown(MouseDownEvent e)
                {
                    if (e.Button != MouseButton.Right) return false;

                    remove();
                    return true;
                }
            }

            /// <summary>A player: a blocky little avatar (yellow head, coloured shirt, dark legs); it can say a word.</summary>
            private partial class Figure : CompositeDrawable
            {
                public static readonly string[] Palette = { "ff8a80", "ffd180", "ccff90", "a7ffeb", "80d8ff", "8c9eff", "ea80fc", "4B974B", "0D69AC", "C4281C" };

                public Figure(Random rng)
                    : this(Color4Extensions.FromHex(Palette[rng.Next(Palette.Length)]))
                {
                }

                public Figure(Color4 colour)
                {
                    Origin = Anchor.Centre;
                    Size = new Vector2(10, 15);

                    InternalChildren = new Drawable[]
                    {
                        new Box { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Size = new Vector2(5), Colour = Color4Extensions.FromHex("F5CD30") },
                        new Box { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Y = 5.5f, Size = new Vector2(8, 6), Colour = colour },
                        new Box { Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, X = 1, Size = new Vector2(3, 3.5f), Colour = Color4Extensions.FromHex("1B2A35") },
                        new Box { Anchor = Anchor.BottomRight, Origin = Anchor.BottomRight, X = -1, Size = new Vector2(3, 3.5f), Colour = Color4Extensions.FromHex("1B2A35") },
                    };
                }

                /// <summary>A speech bubble above the head for a moment.</summary>
                public void Say(string text, double duration = 1600)
                {
                    var bubble = new Container
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -3,
                        AutoSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 5,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White },
                            new OsuSpriteText { Text = text, Margin = new MarginPadding { Horizontal = 4, Vertical = 1 }, Font = OsuFont.GetFont(size: 9, weight: FontWeight.Bold), Colour = Color4Extensions.FromHex("222") },
                        },
                    };
                    AddInternal(bubble);
                    bubble.FadeIn(150).Delay(duration).FadeOut(250).Expire();
                }
            }
        }

        // ================================================================ side panel pieces

        /// <summary>The one HUD button: post in the Discord, players join.</summary>
        private partial class PostButton : OsuClickableContainer
        {
            private readonly Box background;
            private readonly OsuSpriteText detailText;
            private readonly Container popups;

            public string Detail
            {
                set => detailText.Text = value;
            }

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public PostButton(Action action)
            {
                Action = action;
                RelativeSizeAxes = Axes.X;
                Height = 52;
                Masking = true;
                CornerRadius = 12;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("5865F2") },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Size = new Vector2(22),
                        Icon = FontAwesome.Brands.Discord,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 50,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = ClickerStrings.Text("Post in the Discord", "Im Discord posten"),
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                            },
                            detailText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 12),
                                Colour = Color4.White.Opacity(0.8f),
                            },
                        },
                    },
                    popups = new Container { RelativeSizeAxes = Axes.Both },
                };
            }

            public void Pulse(string text)
            {
                background.FlashColour(Color4.White, 200);
                this.ScaleTo(0.97f, 40).Then().ScaleTo(1, 180, Easing.OutQuint);

                var popup = new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -14,
                    Text = text,
                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                    Colour = colours.Yellow,
                    Shadow = true,
                };
                popups.Add(popup);
                popup.MoveToOffset(new Vector2(0, -24), 600, Easing.OutQuint).FadeOut(600, Easing.In).Expire();
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(Color4Extensions.FromHex("6974f5"), 150);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(Color4Extensions.FromHex("5865F2"), 150);
                base.OnHoverLost(e);
            }
        }

        /// <summary>The current event: icon, title, text and, when the player has to decide, the choices.</summary>
        private partial class EventCard : CompositeDrawable
        {
            private readonly Box accent;
            private readonly SpriteIcon icon;
            private readonly OsuSpriteText title;
            private readonly OsuTextFlowContainer text;
            private readonly FillFlowContainer choices;

            public EventCard()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 10;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.06f) },
                    accent = new Box { RelativeSizeAxes = Axes.Y, Width = 4 },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 14, Right = 10, Vertical = 10 },
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    icon = new SpriteIcon { Size = new Vector2(16), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                    title = new OsuSpriteText { Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                },
                            },
                            text = new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 12))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                            choices = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                            },
                        },
                    },
                };
            }

            public void Show(TycoonEvent ev, Action<TycoonEventChoice>? onChoice)
            {
                accent.Colour = ev.Colour;
                icon.Icon = ev.Icon;
                icon.Colour = ev.Colour;
                title.Text = ev.Title;
                text.Text = ev.Text;
                choices.Clear();

                if (onChoice != null)
                {
                    foreach (var choice in ev.Choices)
                    {
                        var captured = choice;
                        choices.Add(new ClickerPillButton(choice.Label, ev.Colour, () => onChoice(captured)));
                    }
                }

                ClearTransforms();
                this.FadeIn(200);

                if (onChoice == null)
                    this.Delay(9000).FadeOut(600);
            }

            public void Dismiss()
            {
                ClearTransforms();
                this.FadeOut(300);
            }
        }

        private partial class StatRow : CompositeDrawable
        {
            public StatRow(LocalisableString label, string value)
            {
                RelativeSizeAxes = Axes.X;
                Height = 26;
                Masking = true;
                CornerRadius = 6;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.05f) },
                    new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 10, Text = label, Font = OsuFont.GetFont(size: 12) },
                    new OsuSpriteText { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, X = -10, Text = value, Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold) },
                };
            }
        }

        private partial class MilestoneRow : CompositeDrawable
        {
            public MilestoneRow(LocalisableString label, bool reached, OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;
                Alpha = reached ? 1 : 0.55f;

                InternalChildren = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 6,
                        Size = new Vector2(12),
                        Icon = reached ? FontAwesome.Solid.CheckCircle : FontAwesome.Regular.Circle,
                        Colour = reached ? colours.Green : colours.Gray9,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 26,
                        Text = label,
                        Font = OsuFont.GetFont(size: 12, weight: reached ? FontWeight.SemiBold : FontWeight.Regular),
                    },
                };
            }
        }
    }
}

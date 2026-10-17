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
    /// Banchosucks: the Private Server Tycoon, an idle game. Run your own osu! server: marketing brings players, players
    /// donate, donations buy hardware, features and more marketing. Too many players for the hardware means lag, a
    /// falling reputation and players leaving. Events (cheater waves, DDoS, broken deploys, a streamer) shake things up;
    /// features make the bad ones harmless. A season restart trades the run for legacy points. The save lives on the lazer
    /// server (plugin <c>banchosucks_tycoon</c>, which checks it for plausibility) and as a local file for offline play.
    /// </summary>
    public partial class TycoonScreen : OsuScreen
    {
        private const double autosave_interval = 30_000;
        private const double local_save_interval = 10_000;
        private const string local_file = "banchosucks-tycoon.json";
        private const float side_width = 470;
        private const int log_lines = 7;

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
        private GetTycoonSaveRequest? loadRequest;
        private SubmitTycoonSaveRequest? saveRequest;

        private ServerRack rack = null!;
        private OsuSpriteText serverNameText = null!;
        private OsuSpriteText seasonText = null!;
        private OsuSpriteText playersText = null!;
        private OsuSpriteText capacityText = null!;
        private OsuSpriteText inflowText = null!;
        private OsuSpriteText donationsText = null!;
        private OsuSpriteText rateText = null!;
        private OsuSpriteText rufText = null!;
        private Box rufBar = null!;
        private OsuSpriteText boostText = null!;
        private PostButton postButton = null!;
        private EventCard eventCard = null!;
        private FillFlowContainer logFlow = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText prestigeText = null!;
        private ClickerPillButton prestigeButton = null!;

        private readonly Dictionary<Tab, ClickerTabButton> tabButtons = new Dictionary<Tab, ClickerTabButton>();
        private readonly Dictionary<Tab, Drawable> tabContents = new Dictionary<Tab, Drawable>();
        private FillFlowContainer hardwareRows = null!;
        private FillFlowContainer marketingRows = null!;
        private FillFlowContainer featureRows = null!;
        private TycoonLeaderboardPanel board = null!;
        private Tab tab = Tab.Hardware;

        private enum Tab
        {
            Hardware,
            Marketing,
            Features,
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
                    Padding = new MarginPadding { Top = 56, Bottom = 24, Left = 30, Right = side_width + 50 },
                    Child = createLeftColumn(),
                },
                new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = side_width,
                    RelativeSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Top = 56, Bottom = 24, Right = 30 },
                    Child = createRightColumn(),
                },
            };

            rebuildShop();
            selectTab(Tab.Hardware);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            serverNameText.Text = serverName();
            beginLoading();
        }

        // ---------------------------------------------------------------- layout

        private Drawable createLeftColumn()
        {
            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 14),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = t("Private Server Tycoon", "Privatserver-Tycoon"),
                                Font = OsuFont.GetFont(size: 40, weight: FontWeight.Bold),
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(12, 0),
                                Children = new Drawable[]
                                {
                                    serverNameText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                                        Colour = colours.Pink,
                                    },
                                    seasonText = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 16),
                                        Colour = colours.Gray9,
                                    },
                                },
                            },
                        },
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 220,
                        ColumnDimensions = new[] { new Dimension(GridSizeMode.Relative, 0.52f), new Dimension(GridSizeMode.Absolute, 14), new Dimension() },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                rack = new ServerRack(),
                                Empty(),
                                createStatsPanel(),
                            },
                        },
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(14, 0),
                        Children = new Drawable[]
                        {
                            postButton = new PostButton(post),
                            boostText = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                Colour = colours.Yellow,
                            },
                        },
                    },
                    eventCard = new EventCard(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Masking = true,
                        CornerRadius = 12,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.04f) },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(12),
                                Spacing = new Vector2(0, 3),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = t("Server log", "Server-Log"),
                                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                                        Colour = colours.Gray9,
                                    },
                                    logFlow = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private Drawable createStatsPanel()
        {
            OsuSpriteText label(LocalisableString text) => new OsuSpriteText { Text = text, Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold), Colour = colours.Gray9 };
            OsuSpriteText big(Color4 colour) => new OsuSpriteText { Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold), Colour = colour };
            OsuSpriteText small() => new OsuSpriteText { Font = OsuFont.GetFont(size: 13), Colour = colours.Gray9 };

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.05f) },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(14),
                        RowDimensions = new[] { new Dimension(), new Dimension(), new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        label(t("Players online", "Spieler online")),
                                        playersText = big(colours.Blue),
                                        capacityText = small(),
                                        inflowText = small(),
                                    },
                                },
                            },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        label(t("Donations", "Spenden")),
                                        donationsText = big(colours.Yellow),
                                        rateText = small(),
                                    },
                                },
                            },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 4),
                                    Children = new Drawable[]
                                    {
                                        rufText = new OsuSpriteText { Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold), Colour = colours.Gray9 },
                                        new CircularContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 8,
                                            Masking = true,
                                            Children = new Drawable[]
                                            {
                                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f) },
                                                rufBar = new Box { RelativeSizeAxes = Axes.Both, Width = 1, Colour = colours.Green },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private Drawable createRightColumn()
        {
            var tabRow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
            };

            foreach (var (value, text, icon) in new[]
                     {
                         (Tab.Hardware, t("Hardware", "Hardware"), FontAwesome.Solid.Server),
                         (Tab.Marketing, t("Marketing", "Marketing"), FontAwesome.Solid.Bullhorn),
                         (Tab.Features, t("Features", "Features"), FontAwesome.Solid.Cogs),
                         (Tab.Board, t("Ranking", "Rangliste"), FontAwesome.Solid.Trophy),
                     })
            {
                var captured = value;
                tabRow.Add(tabButtons[value] = new ClickerTabButton(text, icon, colours.Pink, () => selectTab(captured), 15, 13));
            }

            FillFlowContainer rows() => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Padding = new MarginPadding { Right = 6 },
            };

            Drawable scroll(FillFlowContainer flow) => new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = true,
                Child = flow,
            };

            tabContents[Tab.Hardware] = scroll(hardwareRows = rows());
            tabContents[Tab.Marketing] = scroll(marketingRows = rows());
            tabContents[Tab.Features] = scroll(featureRows = rows());
            tabContents[Tab.Board] = board = new TycoonLeaderboardPanel();

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 16,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("0d0e12").Opacity(0.75f) },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(14),
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Absolute, 10), new Dimension(), new Dimension(GridSizeMode.Absolute, 10), new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[] { tabRow },
                            new Drawable[] { Empty() },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = tabContents.Values.ToArray(),
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
                                    Spacing = new Vector2(0, 6),
                                    Children = new Drawable[]
                                    {
                                        prestigeText = new OsuSpriteText
                                        {
                                            Font = OsuFont.GetFont(size: 13),
                                            Colour = colours.Gray9,
                                        },
                                        prestigeButton = new ClickerPillButton(t("Start a new season", "Neue Saison starten"), colours.Purple, askPrestige, FontAwesome.Solid.Redo),
                                        statusText = new OsuSpriteText
                                        {
                                            Font = OsuFont.GetFont(size: 12),
                                            Colour = colours.Gray9,
                                        },
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

            if (value == Tab.Board && IsLoaded)
                board.Refresh();
        }

        private void rebuildShop()
        {
            hardwareRows.Clear();
            marketingRows.Clear();
            featureRows.Clear();

            for (int i = 0; i < TycoonRules.Hardware.Length; i++)
            {
                int level = i;
                var tier = TycoonRules.Hardware[i];
                hardwareRows.Add(new ClickerShopRow(
                    ClickerShopRow.CreateIcon(hardwareIcon(level), colours.Blue), colours.Blue,
                    t(tier.NameEn, tier.NameDe),
                    t(tier.DescriptionEn, tier.DescriptionDe),
                    () => ClickerStrings.Pick($"{ClickerFormat.Count(tier.Capacity)} players", $"{ClickerFormat.Count(tier.Capacity)} Spieler"),
                    () => level <= state.Hardware ? "✓" : level == state.Hardware + 1 ? n(tier.Cost) : ClickerStrings.Pick("later", "später"),
                    () => ready && level == state.Hardware + 1 && TycoonRules.CanAfford(state, tier.Cost),
                    () => buy(() => TycoonRules.BuyHardware(state), t("Upgraded to {0}: room for {1} players", "Aufgerüstet auf {0}: Platz für {1} Spieler", ClickerStrings.Pick(tier.NameEn, tier.NameDe), ClickerFormat.Count(tier.Capacity)))));
            }

            foreach (var kind in TycoonRules.Marketing)
            {
                var captured = kind;
                marketingRows.Add(new ClickerShopRow(
                    ClickerShopRow.CreateIcon(marketingIcon(kind.Key), colours.Green), colours.Green,
                    t(kind.NameEn, kind.NameDe),
                    t(kind.DescriptionEn, kind.DescriptionDe),
                    () => ClickerStrings.Pick(
                        $"+{n(captured.Rate)} players/s each · now +{n(captured.Rate * state.MarketingCount(captured.Key))}/s",
                        $"+{n(captured.Rate)} Spieler/s je Stück · jetzt +{n(captured.Rate * state.MarketingCount(captured.Key))}/s"),
                    () => n(TycoonRules.MarketingCost(captured.Key, state.MarketingCount(captured.Key))),
                    () => ready && TycoonRules.CanAfford(state, TycoonRules.MarketingCost(captured.Key, state.MarketingCount(captured.Key))),
                    () => buy(() => TycoonRules.BuyMarketing(state, captured.Key), null),
                    () => ClickerFormat.Count(state.MarketingCount(captured.Key))));
            }

            foreach (var feature in TycoonRules.Features)
            {
                var captured = feature;
                featureRows.Add(new ClickerShopRow(
                    ClickerShopRow.CreateIcon(featureIcon(feature.Key), colours.Purple), colours.Purple,
                    t(feature.NameEn, feature.NameDe),
                    t(feature.DescriptionEn, feature.DescriptionDe),
                    () => state.Has(captured.Key) ? ClickerStrings.Pick("active", "aktiv") : ClickerStrings.Pick("one-time purchase", "einmaliger Kauf"),
                    () => state.Has(captured.Key) ? "✓" : n(captured.Cost),
                    () => ready && !state.Has(captured.Key) && TycoonRules.CanAfford(state, captured.Cost),
                    () => buy(() => TycoonRules.BuyFeature(state, captured.Key), t("{0} is live", "{0} ist live", ClickerStrings.Pick(captured.NameEn, captured.NameDe)))));
            }
        }

        private static IconUsage hardwareIcon(int level) => level switch
        {
            0 => FontAwesome.Solid.Microchip,
            1 => FontAwesome.Solid.Laptop,
            2 => FontAwesome.Solid.Hdd,
            3 => FontAwesome.Solid.Server,
            4 => FontAwesome.Solid.Server,
            5 => FontAwesome.Solid.NetworkWired,
            6 => FontAwesome.Solid.Sitemap,
            7 => FontAwesome.Solid.Building,
            _ => FontAwesome.Solid.Cloud,
        };

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

        // ---------------------------------------------------------------- game loop

        protected override void Update()
        {
            base.Update();

            if (!ready) return;

            double dt = Math.Min(0.25, Clock.ElapsedFrameTime / 1000.0);
            if (dt <= 0) return;

            boosts.Tick(dt);
            TycoonRules.Step(state, dt, boosts.Inflow, boosts.Donation);
            dirty = true;

            if (pending == null)
            {
                nextEventIn -= dt;
                if (nextEventIn <= 0) fireEvent();
            }

            if (Time.Current - lastLocalSave > local_save_interval)
                saveLocal();

            if (Time.Current - lastCloudSave > autosave_interval)
                saveCloud();

            refreshTexts();
        }

        private void refreshTexts()
        {
            int cap = TycoonRules.Capacity(state.Hardware);
            double load = state.Players / cap;

            playersText.Text = n(state.Players);
            playersText.Colour = load > TycoonRules.OVERLOAD_TOLERANCE ? colours.Red : colours.Blue;
            capacityText.Text = ClickerStrings.Pick($"capacity {ClickerFormat.Count(cap)} · {ClickerStrings.Pick(TycoonRules.HardwareAt(state.Hardware).NameEn, TycoonRules.HardwareAt(state.Hardware).NameDe)}",
                $"Kapazität {ClickerFormat.Count(cap)} · {ClickerStrings.Pick(TycoonRules.HardwareAt(state.Hardware).NameEn, TycoonRules.HardwareAt(state.Hardware).NameDe)}");
            inflowText.Text = ClickerStrings.Pick($"+{n(TycoonRules.Inflow(state, boosts.Inflow))}/s joining · {n(state.Players * TycoonRules.CHURN)}/s leaving",
                $"+{n(TycoonRules.Inflow(state, boosts.Inflow))}/s Zulauf · {n(state.Players * TycoonRules.CHURN)}/s gehen");
            donationsText.Text = n(state.Donations);
            rateText.Text = ClickerStrings.Pick($"+{n(TycoonRules.DonationRate(state, boosts.Donation))}/s · {n(state.RunTotal)} this season", $"+{n(TycoonRules.DonationRate(state, boosts.Donation))}/s · {n(state.RunTotal)} diese Saison");
            rufText.Text = ClickerStrings.Pick($"Reputation {state.Ruf:0}/100{(load > TycoonRules.OVERLOAD_TOLERANCE ? " · the server lags!" : string.Empty)}",
                $"Ruf {state.Ruf:0}/100{(load > TycoonRules.OVERLOAD_TOLERANCE ? " · der Server laggt!" : string.Empty)}");
            rufBar.Width = (float)(state.Ruf / 100);
            rufBar.Colour = state.Ruf > 60 ? colours.Green : state.Ruf > 30 ? colours.Yellow : colours.Red;
            seasonText.Text = ClickerStrings.Pick($"season {state.Prestige + 1} · {state.Legacy} legacy (x{TycoonRules.LegacyMult(state.Legacy):0.00})", $"Saison {state.Prestige + 1} · {state.Legacy} Legacy (x{TycoonRules.LegacyMult(state.Legacy):0.00})");
            postButton.Detail = ClickerStrings.Pick($"+{n(TycoonRules.ClickValue(state))} players", $"+{n(TycoonRules.ClickValue(state))} Spieler");

            var boostParts = new List<string>();
            if (boosts.InflowRemaining > 0) boostParts.Add(ClickerStrings.Pick($"inflow x{boosts.Inflow:0.#} ({boosts.InflowRemaining:0} s)", $"Zulauf x{boosts.Inflow:0.#} ({boosts.InflowRemaining:0} s)"));
            if (boosts.DonationRemaining > 0) boostParts.Add(ClickerStrings.Pick($"donations x{boosts.Donation:0.#} ({boosts.DonationRemaining:0} s)", $"Spenden x{boosts.Donation:0.#} ({boosts.DonationRemaining:0} s)"));
            boostText.Text = string.Join(" · ", boostParts);

            int gain = TycoonRules.LegacyGain(state.RunTotal);
            prestigeButton.SetEnabled(gain > 0);
            prestigeText.Text = gain > 0
                ? ClickerStrings.Pick($"A new season gives +{gain} legacy (everything else starts over).", $"Eine neue Saison bringt +{gain} Legacy (alles andere beginnt von vorn).")
                : ClickerStrings.Pick($"New season from {n(TycoonRules.PRESTIGE_MIN_TOTAL)} donations in a season ({n(state.RunTotal)} so far).", $"Neue Saison ab {n(TycoonRules.PRESTIGE_MIN_TOTAL)} Spenden in einer Saison (bisher {n(state.RunTotal)}).");

            rack.SetLoad(state.Hardware, load);
        }

        private void post()
        {
            if (!ready) return;

            double added = TycoonRules.Click(state);
            postButton.Pulse(ClickerStrings.Pick($"+{n(added)}", $"+{n(added)}"));
        }

        private void buy(Func<bool> purchase, LocalisableString? logLine)
        {
            if (!ready || !purchase()) return;

            if (logLine != null) log(logLine.Value, colours.Green);
            dirty = true;
        }

        private void fireEvent()
        {
            var ev = TycoonEvents.Roll(state, rng, colours);
            state.Events++;
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
                Font = OsuFont.GetFont(size: 13),
                Colour = colour,
                RelativeSizeAxes = Axes.X,
            });

            while (logFlow.Children.Count > log_lines)
                logFlow.Remove(logFlow.Children.First(), true);
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
                log(t("Season {0} started with {1} legacy", "Saison {0} gestartet mit {1} Legacy", state.Prestige + 1, state.Legacy), colours.Purple);
                dirty = true;
                saveCloud(true);
            }

            if (dialogOverlay != null)
            {
                dialogOverlay.Push(new ClickerConfirmDialog(
                    t("Start a new season?", "Neue Saison starten?"),
                    t("Hardware, marketing, features and donations start over. You keep +{0} legacy: +{1} % on donations and players for ever.",
                        "Hardware, Marketing, Features und Spenden beginnen von vorn. Du behältst +{0} Legacy: dauerhaft +{1} % auf Spenden und Zulauf.", gain, gain * 5),
                    t("Start the season", "Saison starten"), FontAwesome.Solid.Redo, doPrestige));
            }
            else
                doPrestige();
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

            if (loaded == null)
            {
                log(t("Welcome. A Raspberry Pi, no players, big plans. Post in the Discord and buy a server before it lags.",
                    "Willkommen. Ein Raspberry Pi, keine Spieler, große Pläne. Poste im Discord und kauf einen Server, bevor es laggt."), colours.Pink);
            }
            else if (savedAt != null)
            {
                double away = Math.Min(TycoonRules.OFFLINE_CAP, Math.Max(0, nowUnix() - savedAt.Value));

                if (away >= 10)
                {
                    double gained = TycoonRules.Simulate(state, away);
                    log(t("While you were away ({0}): +{1} donations, {2} players online now",
                        "Während du weg warst ({0}): +{1} Spenden, jetzt {2} Spieler online", ClickerFormat.Duration(away), n(gained), n(state.Players)), colours.Yellow);
                }
            }

            ready = true;
            dirty = true;
            lastCloudSave = Time.Current;
            lastLocalSave = Time.Current;
            nextEventIn = rng.Next(40, 90);
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

        /// <summary>
        /// The server as a rack: one unit per hardware level, LEDs that blink faster the busier it gets and turn red under load.
        /// </summary>
        private partial class ServerRack : CompositeDrawable
        {
            private readonly FillFlowContainer units;
            private readonly Box loadBar;
            private readonly OsuSpriteText loadText;
            private readonly SpriteIcon fan;
            private readonly List<Box> leds = new List<Box>();
            private int level = -1;
            private int fanBucket = -1;
            private double load;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public ServerRack()
            {
                RelativeSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 12;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("1a1d2a") },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(14),
                        Children = new Drawable[]
                        {
                            units = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 5),
                            },
                            fan = new SpriteIcon
                            {
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
                                Y = -16,
                                Size = new Vector2(16),
                                Icon = FontAwesome.Solid.Cog,
                                Colour = Color4.White.Opacity(0.6f),
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Children = new Drawable[]
                                {
                                    loadText = new OsuSpriteText { Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold), Colour = Color4.White.Opacity(0.7f) },
                                    new CircularContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 8,
                                        Masking = true,
                                        Children = new Drawable[]
                                        {
                                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f) },
                                            loadBar = new Box { RelativeSizeAxes = Axes.Both, Width = 0 },
                                        },
                                    },
                                },
                            },
                        },
                    },
                };
            }

            public void SetLoad(int hardwareLevel, double newLoad)
            {
                load = newLoad;

                if (hardwareLevel != level)
                {
                    level = hardwareLevel;
                    rebuild();
                }

                loadBar.Width = (float)Math.Clamp(load, 0, 1);
                loadBar.Colour = load > TycoonRules.OVERLOAD_TOLERANCE ? colours.Red : load > 0.8 ? colours.Yellow : colours.Green;
                loadText.Text = ClickerStrings.Pick($"load {load * 100:0} %", $"Auslastung {load * 100:0} %");

                // the fan spins faster the busier the server is
                int bucket = load > TycoonRules.OVERLOAD_TOLERANCE ? 2 : load > 0.8 ? 1 : 0;

                if (bucket != fanBucket)
                {
                    fanBucket = bucket;
                    fan.ClearTransforms();
                    fan.Spin(bucket == 2 ? 450 : bucket == 1 ? 1200 : 3200, RotationDirection.Clockwise);
                    fan.FadeColour(bucket == 2 ? colours.Red : Color4.White.Opacity(0.6f), 300);
                }
            }

            private void rebuild()
            {
                units.Clear();
                leds.Clear();

                int count = Math.Min(6, level + 1);

                for (int i = 0; i < count; i++)
                {
                    var row = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 18,
                        Masking = true,
                        CornerRadius = 3,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("2a2f42") },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 4, Colour = Color4.White.Opacity(0.08f), X = 6 },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 4, Colour = Color4.White.Opacity(0.08f), X = 14 },
                        },
                    };

                    for (int j = 0; j < 4; j++)
                    {
                        var led = new Box
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Size = new Vector2(6),
                            X = -8 - j * 10,
                            Colour = colours.Green,
                        };
                        leds.Add(led);
                        row.Add(led);
                    }

                    units.Add(row);
                }
            }

            protected override void Update()
            {
                base.Update();

                // LEDs blink faster with load; above capacity they go red
                double speed = 1.5 + Math.Min(3, load) * 2.5;
                Color4 colour = load > TycoonRules.OVERLOAD_TOLERANCE ? colours.Red : load > 0.8 ? colours.Yellow : colours.Green;

                for (int i = 0; i < leds.Count; i++)
                {
                    double phase = Math.Sin(Time.Current / 1000 * speed * (1 + i % 4 * 0.37) + i * 1.7);
                    leds[i].Alpha = phase > 0 ? 1 : 0.25f;
                    leds[i].Colour = colour;
                }
            }
        }

        /// <summary>The one active thing to do: post in the Discord, players join.</summary>
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
                Size = new Vector2(260, 64);
                Masking = true;
                CornerRadius = 14;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("5865F2") },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 18,
                        Size = new Vector2(26),
                        Icon = FontAwesome.Brands.Discord,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 58,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = ClickerStrings.Text("Post in the Discord", "Im Discord posten"),
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            },
                            detailText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 13),
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
                this.ScaleTo(0.96f, 40).Then().ScaleTo(1, 180, Easing.OutQuint);

                var popup = new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    X = -14,
                    Y = 10,
                    Text = text,
                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                    Colour = colours.Yellow,
                    Shadow = true,
                };
                popups.Add(popup);
                popup.MoveToOffset(new Vector2(0, -30), 600, Easing.OutQuint).FadeOut(600, Easing.In).Expire();
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
                CornerRadius = 12;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.06f) },
                    accent = new Box { RelativeSizeAxes = Axes.Y, Width = 5 },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 18, Right = 14, Vertical = 12 },
                        Spacing = new Vector2(0, 6),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    icon = new SpriteIcon { Size = new Vector2(20), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                    title = new OsuSpriteText { Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                },
                            },
                            text = new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 14))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                            choices = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
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

                // events without a decision linger for a while and then make way for the log
                if (onChoice == null)
                    this.Delay(9000).FadeOut(600);
            }

            public void Dismiss()
            {
                ClearTransforms();
                this.FadeOut(300);
            }
        }
    }
}

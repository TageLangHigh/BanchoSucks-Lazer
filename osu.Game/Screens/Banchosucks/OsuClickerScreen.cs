// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Volume;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: osu! Clicker, a small idle clicker game in osu! style.
    /// </summary>
    /// <remarks>
    /// The game itself lives in <see cref="ClickerEngine"/> (production, purchases, saving, server); this screen
    /// shows it and turns input into taps. Click the circle or tap the osu!standard keys (the player's own
    /// bindings) for PP, buy buildings and mod upgrades on the right. Bonus spinners and sliders show up now and then.
    /// </remarks>
    public partial class OsuClickerScreen : OsuScreen
    {
        private const float side_panel_width = 490;

        /// <summary>
        /// "x max" buys at most this many buildings at once (keeps the per-frame price check cheap).
        /// </summary>
        private const int max_bulk = 100;

        // osu!standard key bindings are stored under the ruleset short name; OsuAction.LeftButton = 0, OsuAction.RightButton = 1
        private const string osu_ruleset = "osu";
        private const int osu_left_button = 0;
        private const int osu_right_button = 1;

        private static readonly InputKey[] default_tap_keys = { InputKey.Z, InputKey.X };

        // every InputKey a keyboard can produce; mouse buttons in the osu! bindings are covered by clicking the circle
        private static readonly HashSet<InputKey> keyboard_keys = Enum.GetValues<Key>().Select(KeyCombination.FromKey).Where(k => k != InputKey.None).ToHashSet();

        private enum ClickerTab
        {
            Shop,
            Prestige,
            Medals,
            Stats,
            Leaderboard,
        }

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ReadableKeyCombinationProvider keyCombinationProvider { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private readonly Random random = new Random();
        private Sample? clickSample;
        private Sample? comboBreakSample;
        private Sample? bonusSample;

        private OsuSpriteText pointsText = null!;
        private OsuSpriteText rateText = null!;
        private OsuSpriteText statsText = null!;
        private OsuSpriteText messageText = null!;
        private OsuSpriteText bpmText = null!;
        private OsuSpriteText bpmDetailText = null!;
        private OsuSpriteText modifierText = null!;
        private Container floatingLayer = null!;
        private Box flash = null!;
        private ClickerCircle circle = null!;
        private ClickerComboCounter comboCounter = null!;
        private ClickerBuildingStrip buildingStrip = null!;
        private ClickerLeaderboardPanel leaderboard = null!;
        private ClickerPrestigePanel prestigePanel = null!;
        private ClickerMedalsPanel medalsPanel = null!;
        private ClickerStatsPanel statsPanel = null!;
        private Container shopContent = null!;
        private OsuScrollContainer shopScroll = null!;
        private FillFlowContainer shop = null!;
        private ClickerVolumePopover volumePopover = null!;
        private readonly Dictionary<ClickerTab, ClickerTabButton> tabButtons = new Dictionary<ClickerTab, ClickerTabButton>();
        private readonly Dictionary<ClickerTab, Drawable> tabContents = new Dictionary<ClickerTab, Drawable>();
        private readonly List<ClickerAbilityButton> abilityButtons = new List<ClickerAbilityButton>();
        private readonly List<(int amount, ClickerPillButton pill)> amountPills = new List<(int, ClickerPillButton)>();
        private readonly List<ClickerProducer> visibleProducers = new List<ClickerProducer>();
        private readonly Dictionary<string, (int amount, double price)> buyPlan = new Dictionary<string, (int, double)>();
        private ClickerStanceSelector? stanceSelector;
        private ClickerExpeditionSection? expeditionSection;

        private InputKey[] tapKeys = default_tap_keys;
        private string tapKeysText = "Z / X";
        private IDisposable? keyBindingSubscription;

        /// <summary>
        /// A notice stays at least this long before the next queued one replaces it.
        /// </summary>
        private const double message_min_duration = 2500;

        private readonly Queue<LocalisableString> pendingMessages = new Queue<LocalisableString>();
        private double messageShownAt = double.MinValue;

        private double nextBonusAt;
        private double nextSliderAt;
        private int comboColourIndex;
        private string shopSignature = string.Empty;
        private bool numbersHidden;

        /// <summary>
        /// 1, 10 or 0 for "as many as affordable".
        /// </summary>
        private int buyAmount = 1;

        private ClickerBalance balance => engine.Balance;
        private ClickerState state => engine.State;
        private ClickerEconomy economy => engine.Economy;

        private static string format(double value) => ClickerFormat.Number(value);

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            clickSample = audio.Samples.Get(@"Gameplay/normal-hitnormal");
            comboBreakSample = audio.Samples.Get(@"Gameplay/combobreak");
            bonusSample = audio.Samples.Get(@"Gameplay/spinnerbonus");

            Color4 pink = colours.Pink;
            Color4 purple = Color4Extensions.FromHex("6b3fa0");

            foreach (var ability in balance.Abilities)
            {
                var a = ability;
                abilityButtons.Add(new ClickerAbilityButton(a, a.ClickMult > 1 ? colours.Pink : colours.Green, () => activateAbility(a))
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = new Vector2(abilityButtons.Count == 0 ? -(ClickerCircle.SIZE / 2 + 90) : ClickerCircle.SIZE / 2 + 90, 20 + ClickerCircle.SIZE / 2 - 20),
                });
            }

            InternalChildren = new Drawable[]
            {
                // the mouse wheel (with or without Alt) adjusts the volume like at the main menu; the shop keeps scrolling
                new GlobalScrollAdjustsVolume(),
                // background: dark gradient over the menu background, drifting triangles and a glow behind the circle
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("1d1026").Opacity(0.88f), Color4Extensions.FromHex("0b0910").Opacity(0.94f)),
                },
                new Triangles
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = pink,
                    ColourDark = purple,
                    TriangleScale = 3,
                    Velocity = 0.35f,
                    // Triangles ignores the alpha of its colours, so it is dimmed as a whole
                    Alpha = 0.12f,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 40, Top = 80, Bottom = 30 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, side_panel_width),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Right = 30 },
                                Children = new Drawable[]
                                {
                                    new CircularContainer
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Y = 20,
                                        Size = new Vector2(ClickerCircle.SIZE * 1.9f),
                                        Masking = true,
                                        EdgeEffect = new EdgeEffectParameters
                                        {
                                            Type = EdgeEffectType.Glow,
                                            Colour = pink.Opacity(0.12f),
                                            Radius = 140,
                                        },
                                        Child = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = pink.Opacity(0.05f),
                                        },
                                    },
                                    flash = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.White,
                                        Alpha = 0,
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 4),
                                        Children = new Drawable[]
                                        {
                                            modifierText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                                Colour = colours.Yellow,
                                                Alpha = 0,
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Text = "osu! Clicker",
                                                Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                                                Colour = pink,
                                            },
                                            pointsText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 58, weight: FontWeight.Black),
                                                Colour = ColourInfo.GradientVertical(Color4.White, pink.Lighten(0.6f)),
                                                Shadow = true,
                                            },
                                            rateText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                                                Colour = colours.Gray9,
                                            },
                                            buildingStrip = new ClickerBuildingStrip
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Margin = new MarginPadding { Top = 8 },
                                            },
                                        },
                                    },
                                    circle = new ClickerCircle(pink)
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Y = 20,
                                        Clicked = position => tap(position, ClickerEngine.TapSource.Mouse),
                                    },
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ChildrenEnumerable = abilityButtons,
                                    },
                                    comboCounter = new ClickerComboCounter
                                    {
                                        Anchor = Anchor.BottomLeft,
                                        Origin = Anchor.BottomLeft,
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.BottomCentre,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 4),
                                        Children = new Drawable[]
                                        {
                                            bpmText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 34, weight: FontWeight.Bold),
                                                Text = "0 BPM",
                                                Shadow = true,
                                            },
                                            bpmDetailText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                                Colour = colours.Gray9,
                                                Margin = new MarginPadding { Bottom = 8 },
                                            },
                                            messageText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                                Colour = colours.Yellow,
                                            },
                                            statsText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 14),
                                                Colour = colours.Gray9,
                                            },
                                        },
                                    },
                                    floatingLayer = new Container { RelativeSizeAxes = Axes.Both },
                                    new IconButton
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Size = new Vector2(36),
                                        Icon = FontAwesome.Solid.VolumeUp,
                                        IconColour = colours.Gray9,
                                        TooltipText = ClickerStrings.Text("Volume", "Lautstärke"),
                                        Action = () => volumePopover.ToggleVisibility(),
                                    },
                                    volumePopover = new ClickerVolumePopover
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Y = 40,
                                    },
                                },
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = 16,
                                EdgeEffect = new EdgeEffectParameters
                                {
                                    Type = EdgeEffectType.Shadow,
                                    Colour = Color4.Black.Opacity(0.35f),
                                    Radius = 24,
                                },
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("241a2c").Opacity(0.92f), Color4Extensions.FromHex("15111a").Opacity(0.96f)),
                                    },
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        RowDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.Absolute, 54),
                                            new Dimension(),
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
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Colour = Color4.Black.Opacity(0.25f),
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Direction = FillDirection.Horizontal,
                                                            Spacing = new Vector2(6, 0),
                                                            Padding = new MarginPadding { Horizontal = 10 },
                                                            Children = new Drawable[]
                                                            {
                                                                tabButtons[ClickerTab.Shop] = new ClickerTabButton(ClickerStrings.Text("Shop", "Shop"), FontAwesome.Solid.ShoppingCart, pink, () => showTab(ClickerTab.Shop), 14, 13),
                                                                tabButtons[ClickerTab.Prestige] = new ClickerTabButton(ClickerStrings.Text("Prestige", "Prestige"), FontAwesome.Solid.Star, colours.Purple, () => showTab(ClickerTab.Prestige), 14, 13),
                                                                tabButtons[ClickerTab.Medals] = new ClickerTabButton(ClickerStrings.Text("Medals", "Medaillen"), FontAwesome.Solid.Medal, colours.Green, () => showTab(ClickerTab.Medals), 14, 13),
                                                                tabButtons[ClickerTab.Stats] = new ClickerTabButton(ClickerStrings.Text("Stats", "Stats"), FontAwesome.Solid.ChartBar, colours.Blue, () => showTab(ClickerTab.Stats), 14, 13),
                                                                tabButtons[ClickerTab.Leaderboard] = new ClickerTabButton(ClickerStrings.Text("Leaderboard", "Rangliste"), FontAwesome.Solid.Trophy, colours.Yellow, () => showTab(ClickerTab.Leaderboard), 14, 13),
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                            new Drawable[]
                                            {
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Children = new Drawable[]
                                                    {
                                                        shopContent = new Container
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Child = shopScroll = new OsuScrollContainer
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Child = shop = new FillFlowContainer
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    AutoSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Vertical,
                                                                    Spacing = new Vector2(0, 6),
                                                                    Padding = new MarginPadding(14),
                                                                },
                                                            },
                                                        },
                                                        prestigePanel = new ClickerPrestigePanel { Alpha = 0 },
                                                        medalsPanel = new ClickerMedalsPanel { Alpha = 0 },
                                                        statsPanel = new ClickerStatsPanel { Alpha = 0 },
                                                        leaderboard = new ClickerLeaderboardPanel { Alpha = 0 },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            tabContents[ClickerTab.Shop] = shopContent;
            tabContents[ClickerTab.Prestige] = prestigePanel;
            tabContents[ClickerTab.Medals] = medalsPanel;
            tabContents[ClickerTab.Stats] = statsPanel;
            tabContents[ClickerTab.Leaderboard] = leaderboard;

            shopSignature = computeShopSignature();
            rebuildShop();
        }

        // ------------------------------------------------------------------ shop

        /// <summary>
        /// Everything that changes the set of shop rows; the shop is rebuilt when this differs after an engine change.
        /// </summary>
        private string computeShopSignature()
        {
            var signature = new StringBuilder();

            foreach (var producer in balance.Producers)
            {
                if (!economy.ProducerUnlocked(producer))
                    continue;

                signature.Append(producer.Id);
                signature.Append(economy.StarAvailable(producer) ? economy.NextStarIndex(producer.Id) : -1);
                signature.Append(';');
            }

            foreach (var synergy in balance.Synergies)
                signature.Append(state.Synergies.Contains(synergy.Id) ? 'o' : economy.SynergyAvailable(synergy) ? 'a' : '-');

            signature.Append(economy.ExpeditionSlots);
            return signature.ToString();
        }

        private void rebuildShop()
        {
            shop.Clear();
            amountPills.Clear();
            visibleProducers.Clear();
            visibleProducers.AddRange(balance.Producers.Where(economy.ProducerUnlocked));
            updateBuyPlan();

            Color4 gold = Color4Extensions.FromHex("ffd966");

            // buy amount
            var amountRow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Padding = new MarginPadding { Left = 4 },
                Child = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = ClickerStrings.Text("Buy", "Kaufen"),
                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                    Colour = colours.Gray9,
                    Margin = new MarginPadding { Right = 4 },
                },
            };

            foreach (var (amount, label) in new[] { (1, ClickerStrings.Text("x1", "x1")), (10, ClickerStrings.Text("x10", "x10")), (0, ClickerStrings.Text("x max", "x max")) })
            {
                int a = amount;
                var pill = new ClickerPillButton(label, colours.Pink, () => setBuyAmount(a)) { Active = buyAmount == a };
                amountPills.Add((a, pill));
                amountRow.Add(pill);
            }

            shop.Add(amountRow);

            shop.Add(sectionHeader(ClickerStrings.Text("Buildings", "Gebäude")));
            shop.Add(sectionNote(ClickerStrings.Text("Keys 1 to 0 buy the rows below with the chosen amount.", "Die Tasten 1 bis 0 kaufen die Zeilen unten mit der gewählten Menge.")));

            foreach (var producer in visibleProducers)
            {
                var p = producer;
                shop.Add(new ClickerShopRow(ClickerShopRow.CreateIcon(p.Icon, p.Colour), p.Colour, ClickerStrings.Text(p.Name), ClickerStrings.Text(p.Description),
                    () => numbersHidden
                        ? ClickerStrings.Pick($"{economy.Count(p.Id)}× · ??? PP/s each", $"{economy.Count(p.Id)}× · ??? PP/s pro Stück")
                        : ClickerStrings.Pick($"{economy.Count(p.Id)}× · {format(economy.DisplayRate(p.Id))} PP/s each", $"{economy.Count(p.Id)}× · {format(economy.DisplayRate(p.Id))} PP/s pro Stück"),
                    () => planFor(p).amount > 1 ? $"x{planFor(p).amount} · {format(planFor(p).price)} PP" : $"{format(planFor(p).price)} PP",
                    () => state.Points >= planFor(p).price,
                    () => buyProducer(p),
                    () => economy.Count(p.Id) > 0 ? ClickerFormat.Count(economy.Count(p.Id)) : string.Empty,
                    () => readyIn(planFor(p).price)));

                if (!economy.StarAvailable(p))
                    continue;

                int starIndex = economy.NextStarIndex(p.Id);
                int starCount = economy.StarThresholds.Length;
                string name = ClickerStrings.Pick(p.Name);

                shop.Add(new ClickerShopRow(ClickerShopRow.CreateIcon(FontAwesome.Solid.Star, gold.Darken(0.2f)), gold,
                    ClickerStrings.Text("Star {0}: x2 for {1}", "Stern {0}: x2 für {1}", starIndex + 1, name),
                    ClickerStrings.Text("Every {0} produces twice as much for the rest of this run.", "Jeder {0} produziert für den Rest des Runs doppelt.", name),
                    () => ClickerStrings.Pick($"star {starIndex + 1} of {starCount} · {economy.Count(p.Id)} owned", $"Stern {starIndex + 1} von {starCount} · {economy.Count(p.Id)} im Besitz"),
                    () => $"{format(economy.StarCost(p, starIndex))} PP",
                    () => state.Points >= economy.StarCost(p, starIndex),
                    () =>
                    {
                        if (engine.BuyStar(p))
                            showMessage(ClickerStrings.Text("{0}: star {1} bought, production doubled!", "{0}: Stern {1} gekauft, Produktion verdoppelt!", name, starIndex + 1));
                    },
                    null,
                    () => readyIn(economy.StarCost(p, starIndex))));
            }

            shop.Add(sectionHeader(ClickerStrings.Text("Upgrades", "Upgrades")));

            // mod upgrades show the real osu! mod icons
            Ruleset? osu = rulesets.GetRuleset(osu_ruleset)?.CreateInstance();

            foreach (var upgrade in balance.Upgrades)
            {
                var u = upgrade;
                Drawable icon = ClickerShopRow.CreateIcon(u.Icon, colours.Yellow.Darken(0.3f));

                if (u.ModAcronym != null && osu?.CreateModFromAcronym(u.ModAcronym) is { } mod)
                {
                    icon = new ModIcon(mod, showTooltip: false, showExtendedInformation: false)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(0.62f),
                    };
                }

                shop.Add(new ClickerShopRow(icon, colours.Yellow, ClickerStrings.Text(u.Name), ClickerStrings.Text(u.Description),
                    () => state.Upgrades.Contains(u.Id) ? ClickerStrings.Pick("active", "aktiv") : ClickerStrings.Pick("one-off", "einmalig"),
                    () => state.Upgrades.Contains(u.Id) ? ClickerStrings.Pick("owned", "gekauft") : $"{format(u.Cost)} PP",
                    () => !state.Upgrades.Contains(u.Id) && state.Points >= u.Cost,
                    () =>
                    {
                        if (engine.BuyUpgrade(u))
                            showMessage(ClickerStrings.Text("{0} activated!", "{0} aktiviert!", ClickerStrings.Pick(u.Name)));
                    },
                    null,
                    () => state.Upgrades.Contains(u.Id) ? string.Empty : readyIn(u.Cost)));
            }

            // synergies show up once both building types are numerous enough
            var synergies = balance.Synergies.Where(s => state.Synergies.Contains(s.Id) || economy.SynergyAvailable(s)).ToList();

            if (synergies.Count > 0)
            {
                shop.Add(sectionHeader(ClickerStrings.Text("Synergies", "Synergien")));
                shop.Add(sectionNote(ClickerStrings.Text("Two building types boost each other once you own {0} of both.", "Zwei Gebäudetypen stärken sich, sobald du {0} von beiden hast.", balance.SynergyMinCount)));

                foreach (var synergy in synergies)
                {
                    var s = synergy;
                    double cost = economy.SynergyCost(s);
                    string source = ClickerStrings.Pick(balance.Producer(s.Source)?.Name ?? new LocalisedName());
                    string target = ClickerStrings.Pick(balance.Producer(s.Target)?.Name ?? new LocalisedName());

                    shop.Add(new ClickerShopRow(ClickerShopRow.CreateIcon(FontAwesome.Solid.Link, colours.Blue.Darken(0.2f)), colours.Blue, ClickerStrings.Text(s.Name), ClickerStrings.Text(s.Description),
                        () => state.Synergies.Contains(s.Id)
                            ? ClickerStrings.Pick($"active · +{ClickerFormat.Percent(Math.Min(economy.Count(s.Source) * balance.SynergyPerSource, balance.SynergyCap))} for {target}", $"aktiv · +{ClickerFormat.Percent(Math.Min(economy.Count(s.Source) * balance.SynergyPerSource, balance.SynergyCap))} für {target}")
                            : ClickerStrings.Pick($"{economy.Count(s.Source)} × {source} → {target}", $"{economy.Count(s.Source)} × {source} → {target}"),
                        () => state.Synergies.Contains(s.Id) ? ClickerStrings.Pick("active", "aktiv") : $"{format(cost)} PP",
                        () => !state.Synergies.Contains(s.Id) && state.Points >= cost,
                        () =>
                        {
                            if (engine.BuySynergy(s))
                                showMessage(ClickerStrings.Text("{0} active!", "{0} aktiv!", ClickerStrings.Pick(s.Name)));
                        },
                        null,
                        () => state.Synergies.Contains(s.Id) ? string.Empty : readyIn(cost)));
                }
            }

            shop.Add(sectionHeader(ClickerStrings.Text("Abilities", "Fähigkeiten")));

            foreach (var ability in balance.Abilities)
            {
                var a = ability;
                Color4 accent = a.ClickMult > 1 ? colours.Pink : colours.Green;

                shop.Add(new ClickerShopRow(ClickerShopRow.CreateIcon(a.Icon, accent.Darken(0.2f)), accent, ClickerStrings.Text(a.Name), ClickerStrings.Text(a.Description),
                    () => abilityDetail(a),
                    () => state.Abilities.Contains(a.Id) ? ClickerStrings.Pick("Activate", "Aktivieren") : $"{format(a.UnlockCost)} PP",
                    () => state.Abilities.Contains(a.Id) ? economy.AbilityReady(a.Id, engine.Now) : state.Points >= a.UnlockCost,
                    () =>
                    {
                        if (state.Abilities.Contains(a.Id))
                            activateAbility(a);
                        else if (engine.UnlockAbility(a))
                            showMessage(ClickerStrings.Text("{0} unlocked!", "{0} freigeschaltet!", ClickerStrings.Pick(a.Name)));
                    },
                    null,
                    () => state.Abilities.Contains(a.Id) ? string.Empty : readyIn(a.UnlockCost)));
            }

            shop.Add(sectionHeader(ClickerStrings.Text("Mod stance", "Mod-Haltung")));
            shop.Add(stanceSelector = new ClickerStanceSelector());

            expeditionSection = null;

            if (economy.ExpeditionSlots > 0)
            {
                shop.Add(sectionHeader(ClickerStrings.Text("Expeditions", "Expeditionen")));
                shop.Add(expeditionSection = new ClickerExpeditionSection());
            }
        }

        private void setBuyAmount(int amount)
        {
            buyAmount = amount;

            foreach (var (a, pill) in amountPills)
                pill.Active = a == amount;

            updateBuyPlan();
        }

        /// <summary>
        /// Amount and total price of the next purchase per visible building, refreshed once per frame.
        /// </summary>
        private void updateBuyPlan()
        {
            foreach (var producer in visibleProducers)
            {
                int amount = buyAmount == 0 ? Math.Max(economy.MaxAffordable(producer, state.Points, max_bulk), 1) : buyAmount;
                buyPlan[producer.Id] = (amount, economy.BulkCost(producer, amount));
            }
        }

        private (int amount, double price) planFor(ClickerProducer producer) => buyPlan.TryGetValue(producer.Id, out var plan) ? plan : (1, economy.ProducerCost(producer));

        private void buyProducer(ClickerProducer producer)
        {
            var plan = planFor(producer);
            engine.BuyProducer(producer, plan.amount);
        }

        /// <summary>
        /// "ready in 0:42" while a price is out of reach, from the production rate.
        /// </summary>
        private string readyIn(double price)
        {
            if (numbersHidden || state.Points >= price)
                return string.Empty;

            double perSecond = economy.PerSecond(engine.Now);
            if (perSecond <= 0)
                return string.Empty;

            string wait = ClickerFormat.Duration((price - state.Points) / perSecond);
            return ClickerStrings.Pick($"ready in {wait}", $"bereit in {wait}");
        }

        private string abilityDetail(ClickerAbility ability)
        {
            if (!state.Abilities.Contains(ability.Id))
                return ClickerStrings.Pick("locked", "gesperrt");

            long now = engine.Now;

            if (economy.AbilityActive(ability.Id, now))
            {
                string left = ClickerFormat.Duration(state.AbilityActiveUntil.GetValueOrDefault(ability.Id) - now);
                return ClickerStrings.Pick($"active · {left} left", $"aktiv · noch {left}");
            }

            long readyAt = state.AbilityReadyAt.GetValueOrDefault(ability.Id);
            if (readyAt > now)
            {
                string wait = ClickerFormat.Duration(readyAt - now);
                return ClickerStrings.Pick($"ready in {wait}", $"bereit in {wait}");
            }

            return ClickerStrings.Pick("ready!", "bereit!");
        }

        private void activateAbility(ClickerAbility ability)
        {
            if (!engine.ActivateAbility(ability))
                return;

            showMessage(ClickerStrings.Text("{0} active!", "{0} aktiv!", ClickerStrings.Pick(ability.Name)));
            circle.Kiai();
            flash.FadeTo(0.15f, 40).Then().FadeOut(600, Easing.OutQuint);
        }

        // ------------------------------------------------------------------ tabs

        private void showTab(ClickerTab tab)
        {
            foreach (var (t, button) in tabButtons)
                button.Active = t == tab;

            foreach (var (t, content) in tabContents)
                content.FadeTo(t == tab ? 1 : 0, 200, Easing.OutQuint);

            switch (tab)
            {
                case ClickerTab.Leaderboard:
                    engine.SubmitNow();
                    leaderboard.Refresh();
                    break;

                case ClickerTab.Prestige:
                    prestigePanel.Refresh();
                    break;

                case ClickerTab.Medals:
                    medalsPanel.Refresh();
                    break;

                case ClickerTab.Stats:
                    statsPanel.Rebuild();
                    break;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tabButtons[ClickerTab.Shop].Active = true;
            updateVisuals();
            updateModifierBanner();

            engine.ScreenOpen = true;
            engine.Changed += onEngineChanged;
            engine.Notice += showMessage;
            engine.Tapped += onTapped;
            engine.ComboBroken += onComboBroken;
            engine.Submitted += onSubmitted;

            showWelcome();

            // follows changes made in the settings while the game is open
            keyBindingSubscription = realm.RegisterForNotifications(
                r => r.All<RealmKeyBinding>().Where(b => b.RulesetName == osu_ruleset && b.Variant == 0),
                (bindings, _) => updateTapKeys(bindings));

            scheduleBonus();
            scheduleSlider();
        }

        private void showWelcome()
        {
            if (engine.SeasonResetNoticePending)
            {
                engine.SeasonResetNoticePending = false;
                showMessage(ClickerStrings.Text("Season {0}: everyone starts over. Old saves and records are gone, the prestige tree is new. Have fun!",
                    "Saison {0}: alle fangen neu an. Alte Spielstände und Rekorde sind weg, der Prestige-Baum ist neu. Viel Spaß!", balance.Season));
                return;
            }

            var receipt = engine.TakeReceipt();
            statsPanel.SetReceipt(receipt);

            if (receipt.Total >= 1)
            {
                var parts = new List<string>();
                if (receipt.Maps >= 1) parts.Add(ClickerStrings.Pick($"during maps +{format(receipt.Maps)}", $"während Maps +{format(receipt.Maps)}"));
                if (receipt.Client >= 1) parts.Add(ClickerStrings.Pick($"in the client +{format(receipt.Client)}", $"im Client +{format(receipt.Client)}"));
                if (receipt.Offline >= 1) parts.Add(ClickerStrings.Pick($"offline +{format(receipt.Offline)}", $"offline +{format(receipt.Offline)}"));
                if (receipt.Encore >= 1) parts.Add(ClickerStrings.Pick($"Encore +{format(receipt.Encore)}", $"Encore +{format(receipt.Encore)}"));
                if (receipt.Expeditions >= 1) parts.Add(ClickerStrings.Pick($"expeditions +{format(receipt.Expeditions)}", $"Expeditionen +{format(receipt.Expeditions)}"));
                if (receipt.Daily >= 1) parts.Add(ClickerStrings.Pick($"daily bonus +{format(receipt.Daily)}", $"Tagesbonus +{format(receipt.Daily)}"));
                showMessage(ClickerStrings.Text("Welcome back! {0} PP: {1}", "Willkommen zurück! {0} PP: {1}", format(receipt.Total), string.Join(" · ", parts)));
            }
            else if (state.Clicks == 0 && state.TotalEarned < 1)
                showMessage(ClickerStrings.Text("Click the circle or tap with your osu! keys and buy buildings on the right.", "Klick den Kreis oder tippe mit deinen osu!-Tasten und kauf dir rechts Gebäude."));

            foreach (string id in engine.UnseenMedals)
            {
                var medal = balance.Medal(id);
                if (medal != null)
                    showMessage(ClickerStrings.Text("Medal earned: {0}", "Medaille verdient: {0}", ClickerStrings.Pick(medal.Name)));
            }

            engine.UnseenMedals.Clear();
        }

        private void onEngineChanged()
        {
            // the set of shop rows changes with tree unlocks, star thresholds and synergies; rebuilding is deferred so a
            // row whose button triggered the change is not disposed inside its own click handler
            string signature = computeShopSignature();

            if (signature != shopSignature)
            {
                shopSignature = signature;
                Scheduler.AddOnce(rebuildShop);
            }

            if (stanceSelector?.IsLoaded == true)
                stanceSelector.Refresh();
            if (expeditionSection?.IsLoaded == true)
                expeditionSection.Refresh();

            prestigePanel.Refresh();
            medalsPanel.Refresh();
            statsPanel.Rebuild();
            updateModifierBanner();
            updateVisuals();
        }

        private void onSubmitted(ClickerSubmitResponse response) => leaderboard.SetOwnRanks(response);

        private void updateModifierBanner()
        {
            var modifier = engine.Modifier;

            if (modifier == null)
            {
                modifierText.Text = string.Empty;
                modifierText.Alpha = 0;
                return;
            }

            modifierText.Text = ClickerStrings.Text("This week: {0}", "Diese Woche: {0}", ClickerStrings.Pick(modifier.Name));
            modifierText.Alpha = 1;
        }

        protected override void Update()
        {
            base.Update();

            long now = engine.Now;
            double perSecond = economy.PerSecond(now);
            double clickValue = economy.ClickValue(now);
            numbersHidden = balance.Stance(state.Stance)?.HideNumbers == true;

            pointsText.Text = numbersHidden ? "??? PP" : $"{format(state.Points)} PP";
            rateText.Text = numbersHidden
                ? ClickerStrings.Pick("Hidden: your numbers stay a secret", "Hidden: deine Zahlen bleiben geheim")
                : ClickerStrings.Pick($"{format(perSecond)} PP per second · {format(clickValue)} PP per click", $"{format(perSecond)} PP pro Sekunde · {format(clickValue)} PP pro Klick");
            statsText.Text = ClickerStrings.Pick(
                $"{ClickerFormat.Count(state.Clicks)} clicks · max combo {ClickerFormat.Count(state.BestCombo)} · {format(state.TotalEarned)} PP earned in total",
                $"{ClickerFormat.Count(state.Clicks)} Klicks · Max-Combo {ClickerFormat.Count(state.BestCombo)} · insgesamt {format(state.TotalEarned)} PP verdient");

            double tapsPerSecond = engine.BpmMeter.TapsPerSecond(Time.Current);
            bpmText.Text = $"{tapsPerSecond * 15:0} BPM";
            bpmText.Colour = tapsPerSecond > 0 && tapsPerSecond * 15 >= state.BestBpm ? colours.Yellow : Color4.White;
            bpmDetailText.Text = ClickerStrings.Pick(
                $"{tapsPerSecond:0.0} taps/s · record {state.BestBpm:0} BPM · keys {tapKeysText}",
                $"{tapsPerSecond:0.0} Taps/s · Rekord {state.BestBpm:0} BPM · Tasten {tapKeysText}");

            updateBuyPlan();
            updateAbilities(now);

            if (pendingMessages.Count > 0 && Time.Current - messageShownAt >= message_min_duration)
                displayMessage(pendingMessages.Dequeue());

            if (Time.Current >= nextBonusAt)
                spawnBonus();
            if (Time.Current >= nextSliderAt)
                SpawnSlider();
        }

        private void updateAbilities(long now)
        {
            // fractional seconds for a smooth ring; the engine's whole seconds decide the state
            double nowExact = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            bool boosted = false;

            foreach (var button in abilityButtons)
            {
                var ability = button.Ability;

                if (!state.Abilities.Contains(ability.Id))
                {
                    button.SetState(ClickerAbilityButton.AbilityState.Locked, 0, string.Empty);
                    continue;
                }

                if (economy.AbilityActive(ability.Id, now))
                {
                    boosted = true;
                    double until = state.AbilityActiveUntil.GetValueOrDefault(ability.Id);
                    button.SetState(ClickerAbilityButton.AbilityState.Active, (until - nowExact) / ability.DurationSeconds, ClickerFormat.Duration(until - nowExact));
                    continue;
                }

                double readyAt = state.AbilityReadyAt.GetValueOrDefault(ability.Id);

                if (readyAt > now)
                    button.SetState(ClickerAbilityButton.AbilityState.Cooldown, (readyAt - nowExact) / ability.CooldownSeconds, ClickerFormat.Duration(readyAt - nowExact));
                else
                    button.SetState(ClickerAbilityButton.AbilityState.Ready, 0, ClickerStrings.Pick("ready", "bereit"));
            }

            circle.SetBoosted(boosted);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // the keys the player taps circles with in osu!standard
            if (tapKeys.Contains(KeyCombination.FromKey(e.Key)))
            {
                if (!e.Repeat && tap(null, ClickerEngine.TapSource.Keyboard))
                    circle.Press();

                return true;
            }

            // number keys buy the n-th visible building (0 is the tenth)
            if (e.Key >= Key.Number0 && e.Key <= Key.Number9 && !e.ControlPressed && !e.AltPressed)
            {
                int index = e.Key == Key.Number0 ? 9 : e.Key - Key.Number1;

                if (!e.Repeat && index < visibleProducers.Count)
                    buyProducer(visibleProducers[index]);

                return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            if (tapKeys.Contains(KeyCombination.FromKey(e.Key)))
                circle.Release();

            base.OnKeyUp(e);
        }

        private void updateTapKeys(IEnumerable<RealmKeyBinding> bindings)
        {
            var combinations = bindings.Where(b => b.ActionInt == osu_left_button || b.ActionInt == osu_right_button)
                                       .Select(b => b.KeyCombination)
                                       .Where(c => c.Keys.Length == 1 && keyboard_keys.Contains(c.Keys[0]))
                                       .ToArray();

            tapKeys = combinations.Length > 0 ? combinations.Select(c => c.Keys[0]).Distinct().ToArray() : default_tap_keys;
            tapKeysText = combinations.Length > 0
                ? string.Join(" / ", combinations.Select(c => keyCombinationProvider.GetReadableString(c)).Distinct())
                : "Z / X";
        }

        private Vector2? pendingTapPosition;

        /// <summary>
        /// Hands a tap to the engine. Returns false when it was ignored (input lock or BPM cap).
        /// </summary>
        private bool tap(Vector2? screenPosition, ClickerEngine.TapSource source)
        {
            pendingTapPosition = screenPosition;
            return engine.Tap(source);
        }

        private void onTapped(double value)
        {
            clickSample?.Play();
            comboCounter.Set(engine.Combo);

            Color4 comboColour = ClickerBalance.COMBO_COLOURS[comboColourIndex++ % ClickerBalance.COMBO_COLOURS.Length];
            circle.Hit(comboColour);

            if (engine.Combo % 100 == 0)
            {
                circle.Kiai();
                flash.FadeTo(0.12f, 40).Then().FadeOut(500, Easing.OutQuint);
                showMessage(ClickerStrings.Text("{0}x combo!", "{0}x Combo!", engine.Combo));
            }

            Vector2 position = pendingTapPosition ?? circle.ToScreenSpace(circle.DrawSize / 2 + new Vector2((float)random.NextDouble() * 220 - 110, -20 - (float)random.NextDouble() * 60));
            pendingTapPosition = null;
            spawnFloating(position, numbersHidden ? "+???" : $"+{format(value)}", comboColour, 26);
        }

        private void onComboBroken()
        {
            if (comboCounter.Current >= 20)
                comboBreakSample?.Play();
            comboCounter.Break();
        }

        private void updateVisuals()
        {
            circle.SetCursorCount(economy.Count(balance.Producers[0].Id));
            buildingStrip.SetCounts(balance.Producers.Select(p => (p, economy.Count(p.Id))));
        }

        // ------------------------------------------------------------------ events in the play area

        private void scheduleBonus()
        {
            var rules = balance.Spinner;
            double interval = rules.MinIntervalSeconds + random.NextDouble() * (rules.MaxIntervalSeconds - rules.MinIntervalSeconds);
            nextBonusAt = Time.Current + interval * 1000 / economy.EventBoost;
        }

        private void spawnBonus()
        {
            scheduleBonus();

            bool kiai = random.NextDouble() < balance.Spinner.KiaiChance;
            var bonus = new BonusSpinner(kiai ? colours.Pink : colours.Yellow, kiai)
            {
                RelativePositionAxes = Axes.Both,
                Position = new Vector2(0.12f + (float)random.NextDouble() * 0.76f, 0.25f + (float)random.NextDouble() * 0.5f),
            };

            bonus.Clicked = () =>
            {
                double reward = economy.SpinnerReward(engine.Now, kiai);
                engine.GrantSpinner(kiai);
                bonusSample?.Play();
                spawnFloating(bonus.ScreenSpaceDrawQuad.Centre, ClickerStrings.Pick(kiai ? "Kiai spinner! +{0}" : "Spinner bonus! +{0}", kiai ? "Kiai-Spinner! +{0}" : "Spinner-Bonus! +{0}").Replace("{0}", format(reward)), kiai ? colours.Pink : colours.Yellow, 32);
                bonus.ScaleTo(1.5f, 200, Easing.OutQuint).FadeOut(200).Expire();
            };

            floatingLayer.Add(bonus);
            bonus.Delay(balance.Spinner.LifetimeSeconds * 1000).FadeOut(500).Expire();
        }

        private void scheduleSlider()
        {
            var rules = balance.Slider;
            double interval = rules.MinIntervalSeconds + random.NextDouble() * (rules.MaxIntervalSeconds - rules.MinIntervalSeconds);
            nextSliderAt = Time.Current + interval * 1000 / economy.EventBoost;
        }

        /// <summary>
        /// Puts a slider somewhere in the play area (also used by tests).
        /// </summary>
        internal ClickerSliderEvent SpawnSlider()
        {
            scheduleSlider();

            // a curved path: quadratic bezier with a random control point, laid out in a box the ball radius away from the edges
            const int samples = 40;
            const float width = 340;
            const float height = 150;
            float r = ClickerSliderEvent.RADIUS;

            var start = new Vector2(r, r + height * (0.2f + (float)random.NextDouble() * 0.6f));
            var end = new Vector2(r + width, r + height * (0.2f + (float)random.NextDouble() * 0.6f));
            var control = new Vector2(r + width * (0.3f + (float)random.NextDouble() * 0.4f), r + height * (float)random.NextDouble());

            var vertices = new Vector2[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                vertices[i] = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
            }

            var slider = new ClickerSliderEvent(vertices, balance.Slider.DurationSeconds * 1000, colours.Blue)
            {
                RelativePositionAxes = Axes.Both,
                Origin = Anchor.Centre,
                Position = new Vector2(0.2f + (float)random.NextDouble() * 0.6f, 0.2f + (float)random.NextDouble() * 0.6f),
            };

            slider.Completed = accuracy =>
            {
                double reward = engine.GrantSlider(accuracy);
                bonusSample?.Play();
                spawnFloating(slider.ScreenSpaceDrawQuad.Centre, numbersHidden ? $"+??? PP ({ClickerFormat.Percent(accuracy)})" : $"+{format(reward)} PP ({ClickerFormat.Percent(accuracy)})", colours.Blue.Lighten(0.4f), 30);
            };

            floatingLayer.Add(slider);
            return slider;
        }

        private void spawnFloating(Vector2 screenPosition, string text, Color4 colour, float size)
        {
            var sprite = new OsuSpriteText
            {
                Text = text,
                Origin = Anchor.Centre,
                Position = floatingLayer.ToLocalSpace(screenPosition),
                Font = OsuFont.GetFont(size: size, weight: FontWeight.Black),
                Colour = colour,
                Shadow = true,
            };

            floatingLayer.Add(sprite);
            sprite.ScaleTo(0.6f).ScaleTo(1f, 200, Easing.OutBack)
                  .MoveToOffset(new Vector2((float)random.NextDouble() * 40 - 20, -100), 900, Easing.OutQuint)
                  .FadeOut(900, Easing.InQuint)
                  .Expire();
        }

        /// <summary>
        /// Shows a notice; notices that arrive together (expedition back, then the medal for it) take turns.
        /// </summary>
        private void showMessage(LocalisableString text)
        {
            if (Time.Current - messageShownAt < message_min_duration)
            {
                pendingMessages.Enqueue(text);
                return;
            }

            displayMessage(text);
        }

        private void displayMessage(LocalisableString text)
        {
            messageShownAt = Time.Current;
            messageText.ClearTransforms();
            messageText.Text = text;
            messageText.FadeIn(100).Then().Delay(6000).FadeOut(800);
        }

        private static Drawable sectionHeader(LocalisableString text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
            Margin = new MarginPadding { Top = 8, Bottom = 2, Left = 4 },
        };

        private Drawable sectionNote(LocalisableString text) => new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = text,
            Colour = colours.Gray9,
            Margin = new MarginPadding { Bottom = 2, Left = 4 },
        };

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(250, Easing.OutQuint);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            engine.ScreenOpen = false;
            engine.Save();
            engine.SubmitNow();
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            engine.ScreenOpen = false;
            engine.Save();
            base.OnSuspending(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            engine.ScreenOpen = true;
        }

        protected override void Dispose(bool isDisposing)
        {
            keyBindingSubscription?.Dispose();

            if (engine.IsNotNull())
            {
                engine.Changed -= onEngineChanged;
                engine.Notice -= showMessage;
                engine.Tapped -= onTapped;
                engine.ComboBroken -= onComboBroken;
                engine.Submitted -= onSubmitted;
            }

            base.Dispose(isDisposing);
        }
    }
}

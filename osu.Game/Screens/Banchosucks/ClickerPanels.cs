// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// The "Prestige" tab: lifetime PP, rebirth, the prestige tree and respec.
    /// </summary>
    internal partial class ClickerPrestigePanel : CompositeDrawable
    {
        private const double live_interval = 1000;

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private OsuSpriteText lifetimeText = null!;
        private OsuSpriteText pointsText = null!;
        private OsuSpriteText rebirthsText = null!;
        private OsuSpriteText nextText = null!;
        private OsuSpriteText treeInfoText = null!;
        private RoundedButton rebirthButton = null!;
        private RoundedButton respecButton = null!;
        private OsuSpriteText prestigeBuildingsText = null!;
        private FillFlowContainer prestigeBuildingRows = null!;
        private readonly List<ClickerTreeNodeBox> nodes = new List<ClickerTreeNodeBox>();
        private int lastPending = -1;
        private double lastLive = double.MinValue;

        public ClickerPrestigePanel()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var balance = engine.Balance;
            string[] branches = balance.Branches.ToArray();
            int maxTier = balance.Tree.Max(n => n.Tier);

            // branches as columns, tiers as rows; empty cells stay null
            var content = new Drawable[maxTier + 1][];
            content[0] = branches.Select(b => (Drawable)new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = BranchTitle(b),
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                Colour = colours.Gray9,
                Margin = new MarginPadding { Bottom = 4 },
            }).ToArray();

            for (int tier = 1; tier <= maxTier; tier++)
            {
                content[tier] = new Drawable[branches.Length];

                foreach (var node in balance.Tree.Where(n => n.Tier == tier))
                {
                    int column = Array.IndexOf(branches, node.Branch);
                    if (column < 0)
                        continue;

                    var box = new ClickerTreeNodeBox(node);
                    nodes.Add(box);
                    content[tier][column] = box;
                }
            }

            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Padding = new MarginPadding(14),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Masking = true,
                            CornerRadius = 10,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black,
                                    Alpha = 0.35f,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding(14),
                                    Spacing = new Vector2(0, 3),
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Text = ClickerStrings.Text("Prestige", "Prestige"),
                                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                                            Colour = colours.Pink,
                                        },
                                        lifetimeText = new OsuSpriteText { Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold) },
                                        pointsText = new OsuSpriteText { Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold) },
                                        rebirthsText = new OsuSpriteText { Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold) },
                                        nextText = new OsuSpriteText { Font = OsuFont.GetFont(size: 14), Colour = colours.Gray9 },
                                        rebirthButton = new RoundedButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 44,
                                            Margin = new MarginPadding { Top = 8 },
                                            BackgroundColour = colours.Pink.Darken(0.3f),
                                            Action = confirmRebirth,
                                        },
                                    },
                                },
                            },
                        },
                        treeInfoText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                            Margin = new MarginPadding { Left = 4 },
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = branches.Select(_ => new Dimension()).ToArray(),
                            RowDimensions = Enumerable.Range(0, maxTier + 1).Select(_ => new Dimension(GridSizeMode.AutoSize)).ToArray(),
                            Content = content,
                        },
                        respecButton = new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 36,
                            BackgroundColour = colours.Purple.Darken(0.4f),
                            Action = confirmRespec,
                        },
                        prestigeBuildingsText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                            Margin = new MarginPadding { Left = 4, Top = 6 },
                        },
                        prestigeBuildingRows = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                        },
                    },
                },
            };

            // prestige buildings are paid with prestige points and stay through every rebirth
            foreach (var producer in engine.Balance.PrestigeProducers)
            {
                var p = producer;
                prestigeBuildingRows.Add(new ClickerShopRow(ClickerShopRow.CreateIcon(p.Icon, p.Colour), p.Colour, ClickerStrings.Text(p.Name), ClickerStrings.Text(p.Description),
                    () => ClickerStrings.Pick($"{engine.Economy.PrestigeProducerCount(p.Id)}× · {ClickerFormat.Number(p.PerSecond * engine.Economy.ProductionMultiplier)} PP/s each",
                        $"{engine.Economy.PrestigeProducerCount(p.Id)}× · {ClickerFormat.Number(p.PerSecond * engine.Economy.ProductionMultiplier)} PP/s pro Stück"),
                    () => ClickerStrings.Pick($"{engine.Economy.PrestigeProducerCost(p)} prestige", $"{engine.Economy.PrestigeProducerCost(p)} Prestige"),
                    () => engine.Economy.PrestigeProducersUnlocked && engine.Economy.PrestigeAvailable >= engine.Economy.PrestigeProducerCost(p),
                    () => engine.BuyPrestigeProducer(p),
                    () => engine.Economy.PrestigeProducerCount(p.Id) > 0 ? ClickerFormat.Count(engine.Economy.PrestigeProducerCount(p.Id)) : string.Empty));
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        public static LocalisableString BranchTitle(string branch)
        {
            switch (branch)
            {
                case "aim": return ClickerStrings.Text("Aim", "Aim");
                case "speed": return ClickerStrings.Text("Speed", "Speed");
                case "acc": return ClickerStrings.Text("Accuracy", "Accuracy");
                case "read": return ClickerStrings.Text("Reading", "Reading");
                case "mods": return ClickerStrings.Text("Mods", "Mods");
                case "rank": return ClickerStrings.Text("Rank", "Rang");
                case "srv": return ClickerStrings.Text("Server", "Server");
                case "wysi": return ClickerStrings.Text("WYSI", "WYSI");
                default: return branch;
            }
        }

        /// <summary>
        /// Rebuilds everything that changes with purchases, rebirths and tree changes.
        /// </summary>
        public void Refresh()
        {
            var economy = engine.Economy;
            var state = engine.State;

            pointsText.Text = ClickerStrings.Pick(
                $"Prestige points: {economy.PrestigeEffective} · {economy.PrestigeAvailable} available · production {ClickerFormat.Multiplier(economy.PrestigeBonus)} (+{ClickerFormat.Percent(economy.PrestigeRate)} each up to {economy.Balance.PrestigeSoftcap:0}, slower after that)",
                $"Prestige-Punkte: {economy.PrestigeEffective} · {economy.PrestigeAvailable} frei · Produktion {ClickerFormat.Multiplier(economy.PrestigeBonus)} (je +{ClickerFormat.Percent(economy.PrestigeRate)} bis {economy.Balance.PrestigeSoftcap:0}, danach langsamer)");
            rebirthsText.Text = ClickerStrings.Pick($"Rebirths: {state.Rebirths}", $"Rebirths: {state.Rebirths}");
            treeInfoText.Text = ClickerStrings.Pick(
                $"Prestige tree · {state.Tree.Count} / {engine.Balance.Tree.Length} nodes · {economy.PrestigeAvailable} points to spend",
                $"Prestige-Baum · {state.Tree.Count} / {engine.Balance.Tree.Length} Knoten · {economy.PrestigeAvailable} Punkte übrig");

            foreach (var node in nodes)
                node.Refresh();

            prestigeBuildingsText.Text = economy.PrestigeProducersUnlocked
                ? ClickerStrings.Pick(
                    $"Prestige buildings · paid with prestige points, kept through rebirths · {engine.Balance.PrestigeProducers.Sum(p => economy.PrestigeProducerCount(p.Id))} owned",
                    $"Prestige-Gebäude · kosten Prestige-Punkte, bleiben bei jedem Rebirth · {engine.Balance.PrestigeProducers.Sum(p => economy.PrestigeProducerCount(p.Id))} im Besitz")
                : ClickerStrings.Pick(
                    $"Prestige buildings · unlocked after {engine.Balance.PrestigeProducersUnlockRebirths} rebirth, paid with prestige points",
                    $"Prestige-Gebäude · ab {engine.Balance.PrestigeProducersUnlockRebirths} Rebirth, kosten Prestige-Punkte");

            respecButton.Text = ClickerStrings.Text("Respec: costs {0} prestige points, refunds the rest", "Respec: kostet {0} Prestige-Punkte, der Rest kommt zurück", economy.RespecCost);
            respecButton.Enabled.Value = state.Tree.Count > 0 && economy.PrestigeEffective >= 1;

            refreshLive();
        }

        private void refreshLive()
        {
            lastLive = Time.Current;

            var economy = engine.Economy;
            lifetimeText.Text = ClickerStrings.Pick($"Lifetime PP: {ClickerFormat.Number(engine.State.TotalEarned)}", $"PP insgesamt: {ClickerFormat.Number(engine.State.TotalEarned)}");
            nextText.Text = ClickerStrings.Pick($"Next prestige point at {ClickerFormat.Number(economy.NextPrestigeAt)} PP", $"Nächster Prestige-Punkt bei {ClickerFormat.Number(economy.NextPrestigeAt)} PP");

            int pending = economy.PrestigePending;
            if (pending == lastPending)
                return;

            lastPending = pending;
            rebirthButton.Enabled.Value = pending >= 1;
            rebirthButton.Text = pending >= 1
                ? ClickerStrings.Text("Rebirth: +{0} prestige points", "Rebirth: +{0} Prestige-Punkte", pending)
                : ClickerStrings.Text("Rebirth: not enough PP yet", "Rebirth: noch nicht genug PP");
        }

        protected override void Update()
        {
            base.Update();

            // lifetime PP grows every frame; the texts follow once a second
            if (Time.Current - lastLive > live_interval)
                refreshLive();
        }

        private void confirmRebirth()
        {
            int pending = engine.Economy.PrestigePending;
            if (pending < 1)
                return;

            var dialog = new ClickerConfirmDialog(
                ClickerStrings.Text("New account?", "Neuer Account?"),
                ClickerStrings.Text("Your old pp stays on the leaderboard. Buildings, upgrades and this run's PP start over; you get {0} prestige points.",
                    "Deine alten pp bleiben in der Rangliste. Gebäude, Upgrades und die PP dieses Runs fangen von vorn an; du bekommst {0} Prestige-Punkte.", pending),
                ClickerStrings.Text("Rebirth", "Rebirth"),
                FontAwesome.Solid.Redo,
                () => engine.Rebirth());

            if (dialogOverlay != null)
                dialogOverlay.Push(dialog);
            else
                engine.Rebirth();
        }

        private void confirmRespec()
        {
            var dialog = new ClickerConfirmDialog(
                ClickerStrings.Text("Reset the prestige tree?", "Prestige-Baum zurücksetzen?"),
                ClickerStrings.Text("All nodes are removed. {0} prestige points burn, the rest comes back to spend again.",
                    "Alle Knoten werden entfernt. {0} Prestige-Punkte verbrennen, der Rest kommt zurück.", engine.Economy.RespecCost),
                ClickerStrings.Text("Respec", "Respec"),
                FontAwesome.Solid.Undo,
                () => engine.Respec());

            if (dialogOverlay != null)
                dialogOverlay.Push(dialog);
            else
                engine.Respec();
        }
    }

    /// <summary>
    /// One node of the prestige tree: owned (accent), buyable (bright, pulsing border) or locked (dim).
    /// </summary>
    internal partial class ClickerTreeNodeBox : OsuClickableContainer
    {
        public readonly ClickerTreeNode Node;

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly Box background;
        private readonly Container border;

        public ClickerTreeNodeBox(ClickerTreeNode node)
        {
            Node = node;

            RelativeSizeAxes = Axes.X;
            Height = 56;
            Margin = new MarginPadding(2);
            Masking = true;
            CornerRadius = 6;
            Action = buy;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.35f,
                },
                border = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 2,
                    BorderColour = Color4.White,
                    Alpha = 0,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 4, Vertical = 6 },
                    Spacing = new Vector2(0, 3),
                    Children = new Drawable[]
                    {
                        new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = ClickerStrings.Text(node.Name),
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(3, 0),
                            Children = new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(9),
                                    Icon = FontAwesome.Solid.Star,
                                    Colour = Color4Extensions.FromHex("ffd966"),
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = node.Cost.ToString(),
                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                },
                            },
                        },
                    },
                },
            };
        }

        private void buy()
        {
            if (!engine.BuyNode(Node))
                background.FlashColour(colours.Red, 300, Easing.OutQuint);
        }

        public void Refresh()
        {
            var economy = engine.Economy;
            bool owned = economy.NodeOwned(Node.Id);
            bool buyable = economy.CanBuyNode(Node);
            bool requirementsMet = economy.NodeRequirementsMet(Node);
            Color4 accent = colours.Pink;

            background.FadeColour(owned ? accent.Darken(0.25f) : buyable ? accent.Darken(0.75f) : Color4.Black, 200, Easing.OutQuint);
            background.FadeTo(owned ? 0.9f : buyable ? 0.7f : 0.35f, 200, Easing.OutQuint);
            this.FadeTo(owned || buyable ? 1f : requirementsMet ? 0.7f : 0.4f, 200, Easing.OutQuint);

            border.ClearTransforms();

            if (buyable)
            {
                border.BorderColour = accent.Lighten(0.6f);
                border.Loop(b => b.FadeTo(1f, 600, Easing.InOutSine).Then().FadeTo(0.2f, 600, Easing.InOutSine));
            }
            else if (owned)
            {
                border.BorderColour = Color4.White;
                border.FadeTo(0.8f, 200);
            }
            else
                border.FadeOut(200);

            string name = ClickerStrings.Pick(Node.Name);
            string description = ClickerStrings.Pick(Node.Description);

            if (owned)
                TooltipText = ClickerStrings.Text("{0}: {1} (owned)", "{0}: {1} (gekauft)", name, description);
            else if (requirementsMet)
                TooltipText = ClickerStrings.Text("{0}: {1} Costs {2} prestige points.", "{0}: {1} Kostet {2} Prestige-Punkte.", name, description, Node.Cost);
            else
                TooltipText = ClickerStrings.Text("{0}: {1} Requires {2}.", "{0}: {1} Braucht {2}.", name, description, requirementNames());
        }

        private string requirementNames()
        {
            var balance = engine.Balance;

            IEnumerable<ClickerTreeNode?> required = Node.Requires != null
                ? Node.Requires.Select(balance.Node)
                : balance.Tree.Where(n => n.Branch == Node.Branch && n.Tier == Node.Tier - 1);

            return string.Join(" + ", required.Where(n => n != null).Select(n => ClickerStrings.Pick(n!.Name)));
        }
    }

    /// <summary>
    /// The "Medals" tab: all medals as tiles, earned ones coloured with a check, locked ones dimmed with their progress.
    /// </summary>
    internal partial class ClickerMedalsPanel : CompositeDrawable
    {
        private const double refresh_interval = 1000;

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private OsuSpriteText headerText = null!;
        private readonly List<ClickerMedalTile> tiles = new List<ClickerMedalTile>();
        private double lastRefresh = double.MinValue;

        public ClickerMedalsPanel()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            foreach (var medal in engine.Balance.Medals)
                tiles.Add(new ClickerMedalTile(medal));

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        headerText = new OsuSpriteText
                        {
                            Margin = new MarginPadding { Horizontal = 16, Vertical = 10 },
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                            Colour = colours.Yellow,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(6),
                                Padding = new MarginPadding { Horizontal = 14, Bottom = 14 },
                                ChildrenEnumerable = tiles,
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        protected override void Update()
        {
            base.Update();

            // progress (clicks, buildings) moves without a structural change
            if (Time.Current - lastRefresh > refresh_interval)
                Refresh();
        }

        public void Refresh()
        {
            lastRefresh = Time.Current;

            int earned = engine.State.Medals.Count;
            int total = engine.Balance.Medals.Length;
            string bonus = ClickerFormat.Percent(earned * engine.Balance.MedalRate);
            headerText.Text = ClickerStrings.Pick($"{earned} / {total} medals · +{bonus} production", $"{earned} / {total} Medaillen · +{bonus} Produktion");

            foreach (var tile in tiles)
                tile.Refresh();
        }
    }

    /// <summary>
    /// One medal tile.
    /// </summary>
    internal partial class ClickerMedalTile : Container, IHasTooltip
    {
        public readonly ClickerMedal Medal;

        public bool Earned { get; private set; }

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        private readonly Box background;
        private readonly SpriteIcon icon;
        private readonly OsuSpriteText progressText;
        private readonly SpriteIcon check;
        private Color4 accent;
        private string lastProgress = string.Empty;
        private bool? lastEarned;

        public LocalisableString TooltipText => ClickerStrings.Text(Medal.Description);

        public ClickerMedalTile(ClickerMedal medal)
        {
            Medal = medal;

            Size = new Vector2(106, 96);
            Masking = true;
            CornerRadius = 8;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.35f,
                },
                icon = new SpriteIcon
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 10,
                    Size = new Vector2(26),
                    Icon = IconFor(medal.Kind),
                    Shadow = true,
                },
                new TruncatingSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 44,
                    MaxWidth = 98,
                    Text = ClickerStrings.Text(medal.Name),
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                },
                progressText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -6,
                    Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                    Colour = Color4.White.Opacity(0.7f),
                },
                check = new SpriteIcon
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-5, 5),
                    Size = new Vector2(12),
                    Icon = FontAwesome.Solid.Check,
                    Alpha = 0,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            accent = colourFor(Medal.Kind, colours);
            icon.Colour = accent.Lighten(0.3f);
            check.Colour = colours.Green;
        }

        public static IconUsage IconFor(string kind)
        {
            switch (kind)
            {
                case "clicks": return FontAwesome.Solid.MousePointer;
                case "buildings": return FontAwesome.Solid.City;
                case "lifetime": return FontAwesome.Solid.Coins;
                case "bpm": return FontAwesome.Solid.TachometerAlt;
                case "combo": return FontAwesome.Solid.Fire;
                case "rebirths": return FontAwesome.Solid.Redo;
                case "upgrades": return FontAwesome.Solid.Magic;
                case "stars": return FontAwesome.Solid.Star;
                case "tree": return FontAwesome.Solid.Sitemap;
                case "tree_node": return FontAwesome.Solid.Gem;
                case "encore": return FontAwesome.Solid.Music;
                case "daily": return FontAwesome.Solid.CalendarCheck;
                case "expeditions": return FontAwesome.Solid.Plane;
                case "sliders": return FontAwesome.Solid.HandPointer;
                default: return FontAwesome.Solid.Medal;
            }
        }

        private static Color4 colourFor(string kind, OsuColour colours)
        {
            switch (kind)
            {
                case "clicks":
                case "combo":
                    return colours.Pink;

                case "buildings":
                case "stars":
                    return colours.Blue;

                case "lifetime":
                case "bpm":
                    return colours.Yellow;

                case "rebirths":
                case "tree":
                case "tree_node":
                    return colours.Purple;

                case "encore":
                case "daily":
                case "expeditions":
                case "sliders":
                    return colours.Green;

                default:
                    return colours.Orange1;
            }
        }

        public void Refresh()
        {
            var economy = engine.Economy;
            Earned = engine.State.Medals.Contains(Medal.Id);

            string progress = Earned ? string.Empty : progressFor(economy);
            if (progress != lastProgress)
                progressText.Text = lastProgress = progress;

            if (Earned == lastEarned)
                return;

            lastEarned = Earned;
            this.FadeTo(Earned ? 1f : 0.5f, 300, Easing.OutQuint);
            check.FadeTo(Earned ? 1 : 0, 300);
            background.FadeColour(Earned ? accent.Darken(0.6f) : Color4.Black, 300, Easing.OutQuint);
            background.FadeTo(Earned ? 0.9f : 0.35f, 300, Easing.OutQuint);
        }

        private string progressFor(ClickerEconomy economy)
        {
            switch (Medal.Kind)
            {
                case "lifetime":
                    return $"{ClickerFormat.Number(engine.State.TotalEarned)} / {ClickerFormat.Number(Medal.Value)}";

                case "tree_node":
                case "sliders":
                    return string.Empty;

                default:
                    return $"{ClickerFormat.Count(economy.MedalProgressValue(Medal))} / {ClickerFormat.Count((long)Medal.Value)}";
            }
        }
    }

    /// <summary>
    /// The "Stats" tab: lifetime numbers, per-building production, events, boosts and the receipt of the last absence.
    /// </summary>
    internal partial class ClickerStatsPanel : CompositeDrawable
    {
        private const double refresh_interval = 1000;

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private FillFlowContainer flow = null!;
        private readonly List<LiveRow> liveRows = new List<LiveRow>();
        private ClickerLedger? receipt;
        private double lastRefresh = double.MinValue;

        public ClickerStatsPanel()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(14),
                    Spacing = new Vector2(0, 2),
                },
            };

            Rebuild();
        }

        /// <summary>
        /// What was earned while the screen was closed (taken by the screen on entering).
        /// </summary>
        public void SetReceipt(ClickerLedger ledger)
        {
            receipt = ledger;
            Rebuild();
        }

        /// <summary>
        /// Rebuilds the row set (the owned buildings change with purchases).
        /// </summary>
        public void Rebuild()
        {
            var balance = engine.Balance;
            var state = engine.State;
            var economy = engine.Economy;

            flow.Clear();
            liveRows.Clear();

            header(ClickerStrings.Text("Progress", "Fortschritt"));
            row(ClickerStrings.Text("Lifetime PP", "PP insgesamt"), () => ClickerFormat.Number(state.TotalEarned));
            row(ClickerStrings.Text("PP this run", "PP in diesem Run"), () => ClickerFormat.Number(state.RunEarned));
            row(ClickerStrings.Text("Clicks", "Klicks"), () => ClickerFormat.Count(state.Clicks));
            row(ClickerStrings.Text("Best BPM", "Bester BPM-Wert"), () => $"{state.BestBpm:0} BPM");
            row(ClickerStrings.Text("Best combo", "Beste Combo"), () => ClickerFormat.Count(state.BestCombo));
            row(ClickerStrings.Text("Rebirths", "Rebirths"), () => ClickerFormat.Count(state.Rebirths));
            row(ClickerStrings.Text("Prestige points", "Prestige-Punkte"), () => ClickerStrings.Pick($"{economy.PrestigeEffective} ({economy.PrestigeAvailable} available)", $"{economy.PrestigeEffective} ({economy.PrestigeAvailable} frei)"));
            row(ClickerStrings.Text("Buildings", "Gebäude"), () => ClickerFormat.Count(economy.Buildings));
            row(ClickerStrings.Text("Stars", "Sterne"), () => ClickerFormat.Count(economy.TotalStars));

            header(ClickerStrings.Text("Production per building", "Produktion je Gebäude"));
            bool any = false;

            foreach (var producer in balance.Producers)
            {
                var p = producer;
                if (economy.Count(p.Id) == 0)
                    continue;

                any = true;
                row(ClickerStrings.Text(p.Name), () => numbersHidden
                    ? "???"
                    : $"{ClickerFormat.Count(economy.Count(p.Id))} × · {ClickerFormat.Number(economy.ProducerPerSecond(p.Id))} PP/s");
            }

            if (!any)
                note(ClickerStrings.Text("No buildings yet.", "Noch keine Gebäude."));

            header(ClickerStrings.Text("Events", "Events"));
            row(ClickerStrings.Text("Encore maps", "Encore-Maps"), () => ClickerFormat.Count(state.EncoreMaps));
            row(ClickerStrings.Text("Daily bonuses", "Tagesboni"), () => ClickerStrings.Pick($"{state.DailyDays} · streak {state.DailyStreak}", $"{state.DailyDays} · Streak {state.DailyStreak}"), FontAwesome.Solid.Fire, () => state.DailyStreak > 0);
            row(ClickerStrings.Text("Expeditions", "Expeditionen"), () => ClickerFormat.Count(state.ExpeditionsDone));
            row(ClickerStrings.Text("Sliders", "Slider"), () => ClickerFormat.Count(state.SlidersDone));
            row(ClickerStrings.Text("Spinners", "Spinner"), () => ClickerFormat.Count(state.SpinnersDone));
            row(ClickerStrings.Text("Season", "Saison"), () => balance.Season.ToString());
            row(ClickerStrings.Text("This week", "Diese Woche"), () => engine.Modifier is { } modifier
                ? $"{ClickerStrings.Pick(modifier.Name)} · {ClickerFormat.Duration(modifier.EndsAt - engine.Now)}"
                : ClickerStrings.Pick("no special rule", "keine Sonderregel"));
            row(ClickerStrings.Text("Boosts", "Boosts"), boostsText);

            header(ClickerStrings.Text("Since your last visit", "Seit deinem letzten Besuch"));

            var ledger = receipt ?? state.Ledger;

            if (ledger.Total < 1)
                note(ClickerStrings.Text("Nothing earned while you were away.", "Nichts verdient, während du weg warst."));
            else
            {
                receiptRow(ClickerStrings.Text("During maps", "Während Maps"), ledger.Maps);
                receiptRow(ClickerStrings.Text("In the client", "Im Client"), ledger.Client);
                receiptRow(ClickerStrings.Text("Offline", "Offline"), ledger.Offline);
                receiptRow(ClickerStrings.Text("Encore", "Encore"), ledger.Encore);
                receiptRow(ClickerStrings.Text("Expeditions", "Expeditionen"), ledger.Expeditions);
                receiptRow(ClickerStrings.Text("Daily bonus", "Tagesbonus"), ledger.Daily);
                receiptRow(ClickerStrings.Text("Total", "Summe"), ledger.Total);
            }

            refreshLive();
        }

        private bool numbersHidden => engine.Balance.Stance(engine.State.Stance)?.HideNumbers == true;

        private string boostsText()
        {
            long now = engine.Now;
            var parts = new List<string>();

            foreach (var ability in engine.Balance.Abilities)
            {
                if (engine.Economy.AbilityActive(ability.Id, now))
                    parts.Add($"{ClickerStrings.Pick(ability.Name)} {ClickerFormat.Duration(engine.State.AbilityActiveUntil.GetValueOrDefault(ability.Id) - now)}");
            }

            if (engine.Economy.WarmedUp(now))
                parts.Add(ClickerStrings.Pick($"warmed up {ClickerFormat.Duration(engine.State.WarmedUpUntil - now)}", $"aufgewärmt {ClickerFormat.Duration(engine.State.WarmedUpUntil - now)}"));

            return parts.Count > 0 ? string.Join(" · ", parts) : ClickerStrings.Pick("none active", "keiner aktiv");
        }

        private void header(LocalisableString text) => flow.Add(new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
            Colour = colours.Pink,
            Margin = new MarginPadding { Top = 10, Bottom = 2 },
        });

        private void note(LocalisableString text) => flow.Add(new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 14),
            Colour = colours.Gray9,
        });

        private void row(LocalisableString label, Func<string> value, IconUsage? icon = null, Func<bool>? iconVisible = null)
        {
            var valueText = new OsuSpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
            };

            var container = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 22,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = icon != null ? 20 : 0,
                        Text = label,
                        Font = OsuFont.GetFont(size: 14),
                        Colour = colours.Gray9,
                    },
                    valueText,
                },
            };

            SpriteIcon? iconSprite = null;

            if (icon != null)
            {
                container.Add(iconSprite = new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(14),
                    Icon = icon.Value,
                    Colour = colours.Orange1,
                    Alpha = 0,
                });
            }

            flow.Add(container);
            liveRows.Add(new LiveRow(valueText, value, iconSprite, iconVisible));
        }

        private void receiptRow(LocalisableString label, double value)
        {
            if (value < 1)
                return;

            string text = $"+{ClickerFormat.Number(value)} PP";
            row(label, () => text);
        }

        private void refreshLive()
        {
            lastRefresh = Time.Current;

            foreach (var live in liveRows)
                live.Refresh();
        }

        protected override void Update()
        {
            base.Update();

            if (Time.Current - lastRefresh > refresh_interval)
                refreshLive();
        }

        private class LiveRow
        {
            private readonly OsuSpriteText text;
            private readonly Func<string> value;
            private readonly SpriteIcon? icon;
            private readonly Func<bool>? iconVisible;
            private string last = string.Empty;

            public LiveRow(OsuSpriteText text, Func<string> value, SpriteIcon? icon, Func<bool>? iconVisible)
            {
                this.text = text;
                this.value = value;
                this.icon = icon;
                this.iconVisible = iconVisible;
            }

            public void Refresh()
            {
                string current = value();
                if (current != last)
                    text.Text = last = current;

                if (icon != null)
                    icon.Alpha = iconVisible?.Invoke() == true ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// The four mod stances as pills; locked until enough buildings are owned.
    /// </summary>
    internal partial class ClickerStanceSelector : CompositeDrawable
    {
        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly Dictionary<string, ClickerPillButton> pills = new Dictionary<string, ClickerPillButton>();
        private OsuSpriteText hintText = null!;

        public ClickerStanceSelector()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var pillFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(6),
            };

            foreach (var stance in engine.Balance.Stances)
            {
                var s = stance;
                pills[s.Id] = new ClickerPillButton(ClickerStrings.Text(s.Name), colours.Purple, () => engine.SetStance(s), s.Icon);
                pillFlow.Add(pills[s.Id]);
            }

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Padding = new MarginPadding { Horizontal = 4 },
                Children = new Drawable[]
                {
                    pillFlow,
                    hintText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 13),
                        Colour = colours.Gray9,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        public void Refresh()
        {
            var economy = engine.Economy;
            var balance = engine.Balance;
            bool unlocked = economy.StanceUnlocked;

            foreach (var (id, pill) in pills)
            {
                pill.Active = id == engine.State.Stance;
                pill.SetEnabled(unlocked || id == "none");
            }

            if (unlocked)
            {
                var active = balance.Stance(engine.State.Stance) ?? balance.Stances.First();
                hintText.Text = ClickerStrings.Text(active.Description);
            }
            else
            {
                hintText.Text = ClickerStrings.Text("Unlocks at {0} buildings ({1} / {0}).", "Ab {0} Gebäuden frei ({1} / {0}).", balance.StanceUnlockBuildings, economy.Buildings);
            }
        }
    }

    /// <summary>
    /// Tournament expeditions: send half of a building type away for a while and hope for the prize money.
    /// </summary>
    internal partial class ClickerExpeditionSection : CompositeDrawable
    {
        private const double countdown_interval = 1000;

        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private OsuSpriteText infoText = null!;
        private FillFlowContainer runningRows = null!;
        private FillFlowContainer chooser = null!;
        private FillFlowContainer producerPills = null!;
        private FillFlowContainer durationPills = null!;
        private RoundedButton sendButton = null!;
        private readonly List<(ClickerExpedition expedition, OsuSpriteText text)> countdowns = new List<(ClickerExpedition, OsuSpriteText)>();
        private string? selectedProducer;
        private int selectedDuration;
        private double lastCountdown = double.MinValue;

        public ClickerExpeditionSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Padding = new MarginPadding { Horizontal = 4 },
                Children = new Drawable[]
                {
                    infoText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 13),
                        Colour = colours.Gray9,
                    },
                    runningRows = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                    },
                    chooser = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = ClickerStrings.Text("Send half of a building type:", "Schick die Hälfte eines Gebäudetyps los:"),
                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                            },
                            producerPills = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(4),
                            },
                            durationPills = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4),
                            },
                            sendButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 36,
                                BackgroundColour = colours.Green.Darken(0.4f),
                                Action = send,
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        public void Refresh()
        {
            var balance = engine.Balance;
            var economy = engine.Economy;
            var state = engine.State;

            infoText.Text = ClickerStrings.Pick(
                $"{state.Expeditions.Count} / {economy.ExpeditionSlots} slots · {ClickerFormat.Percent(economy.ExpeditionChance)} success chance · prize {ClickerFormat.Multiplier(balance.Expedition.RewardMult)} the production while away",
                $"{state.Expeditions.Count} / {economy.ExpeditionSlots} Plätze · {ClickerFormat.Percent(economy.ExpeditionChance)} Erfolgschance · Preisgeld {ClickerFormat.Multiplier(balance.Expedition.RewardMult)} der Produktion unterwegs");

            runningRows.Clear();
            countdowns.Clear();

            foreach (var expedition in state.Expeditions)
            {
                var text = new OsuSpriteText { Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold) };
                runningRows.Add(text);
                countdowns.Add((expedition, text));
            }

            bool free = state.Expeditions.Count < economy.ExpeditionSlots;
            chooser.Alpha = free ? 1 : 0;

            var eligible = balance.Producers
                                  .Where(p => economy.ProducerUnlocked(p) && economy.Count(p.Id) >= 2 && state.Expeditions.All(e => e.Producer != p.Id))
                                  .ToList();

            if (selectedProducer != null && eligible.All(p => p.Id != selectedProducer))
                selectedProducer = null;

            producerPills.Clear();

            foreach (var producer in eligible)
            {
                var p = producer;
                producerPills.Add(new ClickerPillButton(ClickerStrings.Text("{0} ({1})", "{0} ({1})", ClickerStrings.Pick(p.Name), economy.Count(p.Id)), p.Colour.Darken(0.2f), () =>
                {
                    selectedProducer = p.Id;
                    Schedule(Refresh);
                }, p.Icon) { Active = selectedProducer == p.Id });
            }

            durationPills.Clear();

            for (int i = 0; i < balance.Expedition.DurationsSeconds.Length; i++)
            {
                int index = i;
                int minutes = (int)(balance.Expedition.DurationsSeconds[i] / 60);
                durationPills.Add(new ClickerPillButton(ClickerStrings.Text("{0} min", "{0} min", minutes), colours.Green, () =>
                {
                    selectedDuration = index;
                    Schedule(Refresh);
                }) { Active = selectedDuration == index });
            }

            var selected = selectedProducer != null ? balance.Producer(selectedProducer) : null;
            sendButton.Enabled.Value = selected != null;
            sendButton.Text = selected != null
                ? ClickerStrings.Text("Send {0} × {1} for {2} min", "Schick {0} × {1} für {2} min", economy.Count(selected.Id) / 2, ClickerStrings.Pick(selected.Name), (int)(balance.Expedition.DurationsSeconds[selectedDuration] / 60))
                : eligible.Count > 0
                    ? ClickerStrings.Text("Pick a building type", "Wähl einen Gebäudetyp")
                    : ClickerStrings.Text("You need at least 2 of a building type", "Du brauchst mindestens 2 von einem Gebäudetyp");

            updateCountdowns();
        }

        private void send()
        {
            var producer = selectedProducer != null ? engine.Balance.Producer(selectedProducer) : null;
            if (producer == null)
                return;

            if (engine.StartExpedition(producer, selectedDuration))
                selectedProducer = null;
        }

        private void updateCountdowns()
        {
            lastCountdown = Time.Current;
            long now = engine.Now;

            foreach (var (expedition, text) in countdowns)
            {
                string name = ClickerStrings.Pick(engine.Balance.Producer(expedition.Producer)?.Name ?? new LocalisedName());
                string remaining = ClickerFormat.Duration(expedition.EndsAt - now);
                text.Text = ClickerStrings.Pick($"{expedition.Count} × {name} · back in {remaining}", $"{expedition.Count} × {name} · zurück in {remaining}");
            }
        }

        protected override void Update()
        {
            base.Update();

            if (countdowns.Count > 0 && Time.Current - lastCountdown > countdown_interval)
                updateCountdowns();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    /// <summary>
    /// The second half of the tycoon screen: the team office, the quest line, the supply drops and the life on the
    /// plot (day and night, lamps, chatter, a trophy shelf, event effects, smoke and a LAG sign when the server is overloaded).
    /// </summary>
    public partial class TycoonScreen
    {
        private const double supply_first = 120;
        private const int supply_min = 150;
        private const int supply_max = 240;

        private double supplyIn = supply_first;
        private int supplies;

        private void hireStaff(string key)
        {
            var role = TycoonRules.StaffOf(key);
            if (role == null) return;

            string name = ClickerStrings.Pick(role.Value.NameEn, role.Value.NameDe);
            int before = TycoonRules.StaffTotal(state);

            if (buy(() => TycoonRules.Hire(state, key), t("{0} hired ({1} in the team now)", "{0} eingestellt (jetzt {1} im Team)", name, before + 1)))
                plot.Announce(FontAwesome.Solid.UserPlus, t("New in the team: {0}", "Neu im Team: {0}", name), colours.Pink);
        }

        private void tickSupply(double dt)
        {
            supplyIn -= dt;
            if (supplyIn > 0) return;

            supplyIn = rng.Next(supply_min, supply_max);
            plot.DropSupply(claimSupply);
        }

        /// <summary>A crate on the plot was clicked in time: one of five rewards.</summary>
        private void claimSupply(Vector2 screenPosition)
        {
            if (!ready) return;

            supplies++;
            LocalisableString line;

            switch (rng.Next(5))
            {
                case 0:
                    line = TycoonQuests.GrantCash(state, 20);
                    flyCoins(screenPosition, cashText.ScreenSpaceDrawQuad.Centre, 8);
                    break;

                case 1:
                    boosts.SetInflow(2.0, 30);
                    line = t("Player inflow x2 for 30 seconds", "Zulauf x2 für 30 Sekunden");
                    break;

                case 2:
                    boosts.SetDonation(1.5, 45);
                    line = t("Donations x1.5 for 45 seconds", "Spenden x1,5 für 45 Sekunden");
                    break;

                case 3:
                    state.Ruf = Math.Min(100, state.Ruf + 10);
                    line = t("Reputation +10", "Ruf +10");
                    break;

                default:
                    double added = TycoonRules.ClickValue(state) * 10;
                    state.Players += added;
                    state.MaxPlayers = Math.Max(state.MaxPlayers, state.Players);
                    plot.Burst("discord", 5);
                    line = t("+{0} players at once", "+{0} Spieler auf einmal", n(added));
                    break;
            }

            dirty = true;
            log(t("Supply drop: {0}", "Lieferung: {0}", line), colours.Yellow);
            plot.Announce(FontAwesome.Solid.Gift, line, colours.Yellow);
        }

        /// <summary>The current quest is done: reward, log, banner, next quest.</summary>
        private void checkQuest()
        {
            var quest = TycoonQuests.Current(state);
            if (quest == null || !quest.IsComplete(state)) return;

            var line = TycoonQuests.Apply(quest, state, boosts);
            state.Quest++;
            dirty = true;
            log(t("Quest done: {0}", "Auftrag erledigt: {0}", quest.Title), colours.Green);
            log(line, colours.Gray9);
            plot.Announce(FontAwesome.Solid.ClipboardCheck, t("Quest done: {0}", "Auftrag erledigt: {0}", quest.Title), colours.Green);

            if (quest.Reward == TycoonRewardKind.Cash)
                cashText.ScaleTo(1.12f, 60).Then().ScaleTo(1, 250, Easing.OutQuint);
        }

        /// <summary>The current quest in the side panel: title, progress bar, reward.</summary>
        private partial class QuestCard : CompositeDrawable
        {
            private readonly OsuSpriteText header;
            private readonly TruncatingSpriteText title;
            private readonly Box bar;
            private readonly OsuSpriteText progressText;
            private readonly TruncatingSpriteText rewardText;

            public QuestCard()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 10;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.06f) },
                    new Box { RelativeSizeAxes = Axes.Y, Width = 4, Colour = Color4Extensions.FromHex("66bb6a") },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 14, Right = 10, Vertical = 8 },
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            header = new OsuSpriteText { Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold), Colour = Color4Extensions.FromHex("66bb6a") },
                            title = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold) },
                            new CircularContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 6,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.1f) },
                                    bar = new Box { RelativeSizeAxes = Axes.Both, Width = 0, Colour = Color4Extensions.FromHex("66bb6a") },
                                },
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 14,
                                Children = new Drawable[]
                                {
                                    progressText = new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Font = OsuFont.GetFont(size: 11), Colour = Color4.White.Opacity(0.7f) },
                                    rewardText = new TruncatingSpriteText { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, MaxWidth = 250, Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold), Colour = Color4Extensions.FromHex("ffd54f") },
                                },
                            },
                        },
                    },
                };
            }

            public void Set(TycoonQuest? quest, int index, int total, TycoonState state)
            {
                if (quest == null)
                {
                    header.Text = ClickerStrings.Text("QUESTS", "AUFTRÄGE");
                    title.Text = ClickerStrings.Text("All quests done. The server runs itself now.", "Alle Aufträge erledigt. Der Server läuft von selbst.");
                    bar.Width = 1;
                    progressText.Text = $"{total}/{total}";
                    rewardText.Text = string.Empty;
                    return;
                }

                double progress = quest.Progress(state);
                header.Text = ClickerStrings.Text("QUEST {0}/{1}", "AUFTRAG {0}/{1}", index + 1, total);
                title.Text = quest.Title;
                bar.Width = (float)Math.Clamp(progress / quest.Target, 0, 1);
                progressText.Text = $"{ClickerFormat.Number(Math.Min(progress, quest.Target))} / {ClickerFormat.Number(quest.Target)}";
                rewardText.Text = ClickerStrings.Text("Reward: {0}", "Belohnung: {0}", quest.RewardText);
            }
        }

        private partial class TycoonPlot
        {
            private const float office_x = 8;
            private const float office_y = 8;

            private static readonly string[] chatter_lines = { "gg", "pp!", "fc!!", "ranked?", "nice map", "one more", "hi", "DT?", "choke...", "ez", "wr?", "nomod" };
            private static readonly string[] lag_lines = { "lag...", "500?", "server?", "rip", "timeout", "disconnected" };
            private static readonly float[] supply_columns = { 1.5f, 2.5f, 4.5f, 5.5f, 6.5f, 8.5f, 9.5f, 10.5f, 12.5f, 13.5f };

            // floor palettes in brick colours: garage, office, server room, data centre (two tile shades each)
            private static readonly (string a, string b)[] themes = { ("635F62", "5a5658"), ("7d7c80", "737275"), ("5a6f82", "52667a"), ("2d3a5a", "27324f") };

            private readonly Dictionary<string, BuyPad> staffPads = new Dictionary<string, BuyPad>();
            private readonly Dictionary<string, BuyPad> boostPads = new Dictionary<string, BuyPad>();
            private readonly Dictionary<string, int> tooltipCounts = new Dictionary<string, int>();
            private readonly List<Lamp> lamps = new List<Lamp>();
            private Container desks = null!;
            private Container staffLayer = null!;
            private Container effects = null!;
            private TrophyShelf trophies = null!;
            private Box nightOverlay = null!;
            private LagSign lagSign = null!;
            private PlotBanner banner = null!;
            private SupplyCrate? supply;
            private OwnerSign ownerSign = null!;
            private double smokeAccumulator;
            private int lastStaffTotal = -1;
            private int lastTheme = -1;
            private bool lagging;
            private readonly List<CloudShadow> clouds = new List<CloudShadow>();

            /// <summary>Shift is held: marketing pads offer ten at once.</summary>
            public bool Bulk;

            private double lastPurchase;
            private SpriteIcon hintArrow = null!;
            private VipFigure? vip;
            private CrisisOverlay? crisis;

            private void buildExtras()
            {
                // the season restart scales the buildings down around the middle of the plot
                structures.Anchor = Anchor.Centre;
                structures.Origin = Anchor.Centre;

                // cloud shadows drift over the floor by day
                for (int i = 0; i < 3; i++)
                {
                    var cloud = new CloudShadow(rng);
                    clouds.Add(cloud);
                    floor.Add(cloud);
                }

                // the team office right of the lower droppers: a lighter floor patch, desks per hire, three pads
                structures.Add(new Container
                {
                    Position = cell(office_x, office_y),
                    Size = new Vector2(CELL * 8, CELL * 2),
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("2a3048") },
                        new OsuSpriteText { Position = new Vector2(6, 3), Text = "TEAM", Font = OsuFont.GetFont(size: 9, weight: FontWeight.Bold), Colour = Color4.White.Opacity(0.3f) },
                    },
                });
                structures.Add(desks = new Container { RelativeSizeAxes = Axes.Both });
                structures.Add(staffLayer = new Container { RelativeSizeAxes = Axes.Both });

                int slot = 0;

                foreach (var role in TycoonRules.Staff)
                {
                    var captured = role.Key;
                    var pad = new BuyPad(staffIcon(role.Key), colours.Pink, () => screen.hireStaff(captured)) { Position = cell(office_x + slot * 2, office_y + 2), Size = new Vector2(CELL * 2, CELL) };
                    staffPads[role.Key] = pad;
                    pads.Add(pad);
                    slot++;
                }

                // the owner's sign above the servers
                structures.Add(ownerSign = new OwnerSign { Position = cell(16, 1), Size = new Vector2(CELL * 4, CELL) });

                // the boost machine: three consumables, bought with cash, each on a cooldown
                structures.Add(new Container
                {
                    Position = cell(0, 7.55f),
                    Size = new Vector2(CELL * 2, CELL * 3.45f),
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("5d1f1f") },
                        new Box { RelativeSizeAxes = Axes.X, Height = 8, Colour = Color4Extensions.FromHex("F5CD30") },
                        new OsuSpriteText { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Y = 8, Text = "BOOSTS", Font = OsuFont.GetFont(size: 8, weight: FontWeight.Bold), Colour = Color4.White.Opacity(0.6f) },
                    },
                });

                int boostSlot = 0;

                foreach (var item in boost_items)
                {
                    var captured = item.key;
                    var pad = new BuyPad(boostIcon(item.key), colours.Yellow, () => screen.buyBoost(captured)) { Position = cell(0, 8 + boostSlot), Size = new Vector2(CELL * 2, CELL) };
                    boostPads[item.key] = pad;
                    pads.Add(pad);
                    boostSlot++;
                }

                // a trophy per milestone along row 1
                structures.Add(trophies = new TrophyShelf(milestones.Length) { Position = cell(0, 1), Size = new Vector2(CELL * 16, CELL) });

                // lamps on both sides of the walkway
                for (int x = 3; x <= 15; x += 4)
                {
                    var above = new Lamp { Position = cell(x + 0.5f, 5.55f) };
                    var below = new Lamp { Position = cell(x + 0.5f, 7.6f) };
                    lamps.Add(above);
                    lamps.Add(below);
                    structures.Add(above);
                    structures.Add(below);
                }

                // night falls over floor and buildings, but not over the pads and the people
                AddInternal(nightOverlay = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("070b2a"), Alpha = 0 });
                ChangeInternalChildDepth(pads, -1);
                ChangeInternalChildDepth(figures, -2);
                ChangeInternalChildDepth(coins, -3);
                AddInternal(lagSign = new LagSign { Position = cell(18, 2.7f), Depth = -3 });
                AddInternal(effects = new Container { RelativeSizeAxes = Axes.Both, Depth = -4 });
                AddInternal(hintArrow = new SpriteIcon
                {
                    Origin = Anchor.BottomCentre,
                    Size = new Vector2(18),
                    Icon = FontAwesome.Solid.ArrowDown,
                    Colour = Color4Extensions.FromHex("F5CD30"),
                    Alpha = 0,
                    Depth = -5,
                });
                AddInternal(banner = new PlotBanner { Depth = -5 });
            }

            private void resetExtras()
            {
                trophies.Reset();
                lastStaffTotal = -1;
                lastTheme = -1;
                supply?.Expire();
                supply = null;
                lastPurchase = Time.Current;
            }

            private void refreshExtras(TycoonState state, double load, bool ready)
            {
                lagging = load > TycoonRules.OVERLOAD_TOLERANCE;

                // the team: pads from the Mini PC on, one desk and one figure per head
                foreach (var role in TycoonRules.Staff)
                {
                    int level = state.StaffCount(role.Key);
                    long cost = TycoonRules.StaffCost(role.Key, level);
                    string name = staffShort(role.Key);
                    var pad = staffPads[role.Key];
                    pad.Set(level == 0 ? name : $"{name} {level}/{TycoonRules.STAFF_MAX_LEVEL}", staffBonus(role), cost,
                        ready && TycoonRules.CanAfford(state, cost), state.Hardware >= 2 && level < TycoonRules.STAFF_MAX_LEVEL);
                    pad.TooltipText = $"{ClickerStrings.Pick(role.NameEn, role.NameDe)}: {ClickerStrings.Pick(role.DescriptionEn, role.DescriptionDe)}";
                }

                foreach (var item in boost_items)
                {
                    var (cost, locked) = screen.boostOffer(item.key);
                    boostPads[item.key].Set(ClickerStrings.Pick(item.en, item.de), ClickerStrings.Pick(item.descEn, item.descDe), cost,
                        ready && locked == null && TycoonRules.CanAfford(state, cost), state.Hardware >= 1, locked);
                }

                int total = TycoonRules.StaffTotal(state);

                if (total != lastStaffTotal)
                {
                    lastStaffTotal = total;
                    rebuildTeam(state);
                }

                // the floor grows up with the hardware: garage, office, server room, data centre
                int theme = state.Hardware <= 1 ? 0 : state.Hardware <= 3 ? 1 : state.Hardware <= 5 ? 2 : 3;

                if (theme != lastTheme)
                {
                    bool instant = lastTheme < 0;
                    lastTheme = theme;
                    int i = 0;

                    foreach (var tile in floor.Children.OfType<Box>())
                    {
                        int x = i % TycoonRules.OFFICE_WIDTH, y = i / TycoonRules.OFFICE_WIDTH;
                        tile.FadeColour(Color4Extensions.FromHex((x + y) % 2 == 0 ? themes[theme].a : themes[theme].b), instant ? 0 : 800);
                        i++;
                    }
                }

                foreach (var (key, _, _, _) in dropper_slots)
                {
                    int count = state.MarketingCount(key);
                    if (tooltipCounts.TryGetValue(key, out int shown) && shown == count) continue;

                    tooltipCounts[key] = count;
                    var kind = TycoonRules.MarketingOf(key);
                    droppers[key].TooltipText = count == 0
                        ? string.Empty
                        : ClickerStrings.Pick($"{kind.NameEn} Lv {count}: +{ClickerFormat.Number(kind.Rate * count)} players/s", $"{kind.NameDe} Lv {count}: +{ClickerFormat.Number(kind.Rate * count)} Spieler/s");
                }

                lagSign.SetVisible(lagging);

                if (lagging)
                {
                    smokeAccumulator += Clock.ElapsedFrameTime;

                    if (smokeAccumulator >= 350)
                    {
                        smokeAccumulator = 0;
                        puff();
                    }
                }

                // beginners only (before the first hardware upgrade), after a while without a purchase: the laptop first, else the cheapest pad
                BuyPad? candidate = null;

                if (ready && state.Hardware == 0 && Time.Current - lastPurchase > 40_000)
                    candidate = hardwarePad.Affordable ? hardwarePad : allPads().Where(p => p.Affordable).OrderBy(p => p.LastCost).FirstOrDefault();

                if (candidate != null)
                {
                    hintArrow.Alpha = 1;
                    hintArrow.Position = candidate.Position + new Vector2(candidate.Width / 2, -4 + (float)Math.Sin(Time.Current / 200) * 4);
                }
                else
                    hintArrow.Alpha = 0;
            }

            private IEnumerable<BuyPad> allPads() => marketingPads.Values.Concat(featurePads.Values).Concat(decoPads.Values).Concat(staffPads.Values).Append(hardwarePad);

            private void rebuildTeam(TycoonState state)
            {
                staffLayer.Clear();
                desks.Clear();
                int slot = 0;

                foreach (var role in TycoonRules.Staff)
                {
                    for (int i = 0; i < state.StaffCount(role.Key); i++)
                    {
                        var deskPosition = cell(office_x + 0.15f + slot % 8, office_y + 0.25f + slot / 8);
                        desks.Add(new Desk { Position = deskPosition });
                        staffLayer.Add(new StaffFigure(role.Key, rng, cell(office_x + 0.3f, office_y + 0.4f), cell(office_x + 7.7f, office_y + 1.7f))
                        {
                            Position = deskPosition + new Vector2(CELL * 0.35f, CELL * 0.75f),
                        });
                        slot++;
                    }
                }
            }

            protected override void Update()
            {
                base.Update();

                // a day on the plot lasts four minutes; the night tints everything and lights the lamps
                double phase = Time.Current / 240_000 % 1;
                float night = (float)Math.Clamp((Math.Sin(phase * Math.PI * 2) - 0.2) / 0.8, 0, 1);
                nightOverlay.Alpha = night * 0.3f;

                foreach (var lamp in lamps)
                    lamp.SetNight(night);

                foreach (var cloud in clouds)
                    cloud.Alpha = 0.03f + 0.12f * (1 - night);
            }

            // ------------------------------------------------------------ things the screen asks for

            public void Announce(IconUsage icon, LocalisableString text, Color4 colour) => banner.Show(icon, text, colour);

            public void SetOwner(string name, int prestige, APIUser? user) => ownerSign.Set(name, prestige, user);

            public void PopupAtCollector(string text, Color4 colour) => popup(cell(18, 10.2f), text, colour);

            public void NotePurchase() => lastPurchase = Time.Current;

            /// <summary>A crisis: the server block wants clicks; <paramref name="done"/> gets whether the player made it in time.</summary>
            public void StartCrisis(int clicks, double ms, Action<bool> done)
            {
                if (crisis?.IsAlive == true)
                {
                    done(false);
                    return;
                }

                effects.Add(crisis = new CrisisOverlay(clicks, ms, done) { Position = cell(16, 2), Size = new Vector2(CELL * 4, CELL * 7) });
            }

            /// <summary>A well-known player walks the conveyor slowly; a click gets an autograph.</summary>
            public void SpawnVip(Action<Vector2> autograph)
            {
                if (vip?.IsAlive == true) return;

                vip = new VipFigure(autograph) { Position = cell(2, 6.5f) };
                figures.Add(vip);
                vip.MoveToX(walk_end_x * CELL, 14_000).Then().MoveTo(cell(17.5f, 6.5f), 600, Easing.InQuad).FadeOut(600).Expire();
                Announce(FontAwesome.Solid.Crown, ClickerStrings.Text("A well-known player visits your server", "Ein bekannter Spieler schaut auf deinem Server vorbei"), Color4Extensions.FromHex("F5CD30"));
            }

            /// <summary>The old season goes down, the new one rises: buildings shrink away and come back, confetti, a banner.</summary>
            public void PlaySeasonReset(int season)
            {
                structures.ClearTransforms();
                structures.ScaleTo(0.92f, 350, Easing.In).FadeOut(350).Then().ScaleTo(1, 600, Easing.OutBack).FadeIn(600);
                flash(Color4Extensions.FromHex("9c7cf5"));
                confetti(40);
                Scheduler.AddDelayed(() => Announce(FontAwesome.Solid.Redo, ClickerStrings.Text("Season {0}", "Saison {0}", season), Color4Extensions.FromHex("9c7cf5")), 400);
            }

            public void LightTrophy(int index, bool animate) => trophies.Light(index, animate);

            /// <summary>A crate lands on a free spot next to the walkway; one at a time.</summary>
            public void DropSupply(Action<Vector2> claim)
            {
                if (supply?.IsAlive == true) return;

                // a drone flies in from the left, lets the crate go above the spot and leaves to the right
                var target = cell(supply_columns[rng.Next(supply_columns.Length)], rng.Next(2) == 0 ? 5.45f : 7.6f);
                var drone = new Drone { Position = new Vector2(-60, target.Y - 140) };
                effects.Add(drone);
                double flight = (target.X + 60) * 6;
                drone.MoveToX(target.X, flight, Easing.InOutSine).Then().MoveToX(WIDTH + 80, (WIDTH + 80 - target.X) * 5, Easing.InSine).Expire();
                Scheduler.AddDelayed(() => effects.Add(supply = new SupplyCrate(target, claim)), flight + 100);
            }

            /// <summary>What an event looks like on the plot.</summary>
            public void PlayEvent(string key, bool bad)
            {
                switch (key)
                {
                    case "cheaters":
                        for (int i = 0; i < 6; i++)
                        {
                            bool banned = !bad;
                            Scheduler.AddDelayed(() => spawnWalker(Color4Extensions.FromHex("ff5252"), banned), i * 140);
                        }

                        break;

                    case "ddos":
                        for (int i = 0; i < 3; i++)
                            Scheduler.AddDelayed(() => ring(cell(1, 6.5f), bad ? colours.Red : colours.Green, 260), i * 220);
                        if (bad) flash(colours.Red);
                        break;

                    case "dbcrash":
                    case "deploy":
                    case "medal":
                        if (bad)
                        {
                            shakeServers();
                            sparks(14);
                        }
                        else
                            ring(cell(18, 5.5f), colours.Green, 180);

                        break;

                    case "streamer":
                    case "lazer":
                    case "weekend":
                        confetti(24);
                        for (int i = 0; i < 10; i++)
                            Scheduler.AddDelayed(() => spawnWalker(Color4Extensions.FromHex(key == "streamer" ? "ffd54f" : "80d8ff"), false), i * 90);
                        break;

                    case "donor":
                        coinShower(14);
                        break;
                }
            }

            // ------------------------------------------------------------ effects

            private string chatter() => lagging ? lag_lines[rng.Next(lag_lines.Length)] : chatter_lines[rng.Next(chatter_lines.Length)];

            /// <summary>A figure from the gate: bad ones walk into the servers, banned ones vanish three cells in.</summary>
            private void spawnWalker(Color4 colour, bool banned)
            {
                if (figures.Children.Count > 70) return;

                var start = cell(2, 6.5f) + new Vector2(0, (float)(rng.NextDouble() - 0.5) * 16);
                var figure = new Figure(colour) { Position = start };
                figures.Add(figure);

                if (banned)
                {
                    figure.MoveToX(CELL * 5, 700).Then().FadeColour(Color4.White, 80).ScaleTo(1.8f, 150).FadeOut(150).Expire();
                    Scheduler.AddDelayed(() => figure.Say("banned", 500), 600);
                    return;
                }

                figure.MoveToX(walk_end_x * CELL, 1800).Then().MoveTo(cell(17.5f, 6.5f), 500, Easing.InQuad).FadeOut(500).Expire();
            }

            private void ring(Vector2 centre, Color4 colour, float size)
            {
                var r = new CircularContainer
                {
                    Origin = Anchor.Centre,
                    Position = centre,
                    Size = new Vector2(10),
                    Masking = true,
                    BorderThickness = 3,
                    BorderColour = colour,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                };
                effects.Add(r);
                r.ResizeTo(size, 700, Easing.OutQuint).FadeOut(700).Expire();
            }

            private void flash(Color4 colour)
            {
                var f = new Box { RelativeSizeAxes = Axes.Both, Colour = colour, Alpha = 0 };
                effects.Add(f);
                f.FadeTo(0.22f, 80).Then().FadeOut(600).Expire();
            }

            private void shakeServers()
            {
                float x = CELL * 16;
                servers.MoveToX(x + 5, 40).Then().MoveToX(x - 5, 70).Then().MoveToX(x + 3, 70).Then().MoveToX(x, 60);
            }

            private void sparks(int count)
            {
                var origin = cell(18, 4.5f);

                for (int i = 0; i < count; i++)
                {
                    var spark = new Box
                    {
                        Origin = Anchor.Centre,
                        Position = origin,
                        Size = new Vector2(4),
                        Colour = Color4Extensions.FromHex(rng.Next(2) == 0 ? "ffb74d" : "fff176"),
                        Rotation = rng.Next(360),
                    };
                    effects.Add(spark);
                    var target = origin + new Vector2((float)(rng.NextDouble() - 0.5) * 140, (float)(rng.NextDouble() * 120 - 50));
                    spark.MoveTo(target, 450 + rng.Next(250), Easing.OutQuad).FadeOut(600).Expire();
                }
            }

            private void confetti(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var piece = new Box
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2((float)rng.NextDouble() * WIDTH, -10),
                        Size = new Vector2(7, 3),
                        Colour = Color4Extensions.FromHex(Figure.Palette[rng.Next(Figure.Palette.Length)]),
                    };
                    effects.Add(piece);
                    double fall = 1500 + rng.Next(1200);
                    piece.Delay(rng.Next(400)).MoveToY(HEIGHT + 10, fall, Easing.In).RotateTo(rng.Next(2) == 0 ? 540 : -540, fall).FadeOut(fall).Expire();
                }
            }

            private void coinShower(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var coin = new SpriteIcon
                    {
                        Origin = Anchor.Centre,
                        Position = cell(16 + (float)rng.NextDouble() * 4, -0.5f),
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.Coins,
                        Colour = colours.Yellow,
                    };
                    coins.Add(coin);
                    coin.Delay(i * 70).MoveTo(cell(18, 11), 800, Easing.InQuad).FadeOut(800, Easing.In).Expire();
                }

                Scheduler.AddDelayed(collector.Pulse, count * 70 + 600);
            }

            /// <summary>A line of text floating up from a spot on the plot.</summary>
            private void popup(Vector2 position, string text, Color4 colour)
            {
                var label = new OsuSpriteText
                {
                    Origin = Anchor.BottomCentre,
                    Position = position,
                    Text = text,
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                    Colour = colour,
                    Shadow = true,
                };
                effects.Add(label);
                label.MoveToOffset(new Vector2(0, -30), 800, Easing.OutQuint).FadeOut(800, Easing.In).Expire();
            }

            /// <summary>Dust flying when something gets built.</summary>
            private void dust(Vector2 centre, int count = 6)
            {
                for (int i = 0; i < count; i++)
                {
                    var grain = new Circle
                    {
                        Origin = Anchor.Centre,
                        Position = centre,
                        Size = new Vector2(8),
                        Colour = Color4.LightGray.Opacity(0.5f),
                    };
                    effects.Add(grain);
                    double angle = rng.NextDouble() * Math.PI * 2;
                    var target = centre + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (30 + (float)rng.NextDouble() * 20);
                    grain.MoveTo(target, 500, Easing.OutQuad).ScaleTo(2.5f, 500).FadeOut(500).Expire();
                }
            }

            /// <summary>A puff of smoke from the servers while they lag.</summary>
            private void puff()
            {
                var smoke = new Circle
                {
                    Origin = Anchor.Centre,
                    Position = cell(17 + (float)rng.NextDouble() * 2, 2.2f),
                    Size = new Vector2(10),
                    Colour = Color4.Gray.Opacity(0.6f),
                };
                effects.Add(smoke);
                smoke.MoveToOffset(new Vector2((float)(rng.NextDouble() - 0.5) * 20, -55), 1300, Easing.Out).ScaleTo(2.2f, 1300).FadeOut(1300).Expire();
            }

            /// <summary>Short names that fit a pad; the full name goes into the tooltip.</summary>
            private static string padName(string key) => key switch
            {
                "discord" => "Discord",
                "reddit" => "Reddit",
                "youtube" => "YouTube",
                "streamer" => "Streamer",
                "tournament" => ClickerStrings.Pick("Tourney", "Turnier"),
                "campaign" => ClickerStrings.Pick("Ads", "Werbung"),
                _ => "Influencer",
            };

            private static string featureShort(TycoonFeature feature) => feature.Key switch
            {
                "tests" => "Tests",
                "supporter" => "Supporter",
                "mirror" => "Mirror",
                "lazer" => "Lazer",
                _ => ClickerStrings.Pick(feature.NameEn, feature.NameDe),
            };

            private static string decoShort(string key) => key switch
            {
                "carpet" => ClickerStrings.Pick("Carpet", "Teppich"),
                "plant" => ClickerStrings.Pick("Plant", "Pflanze"),
                "desk" => ClickerStrings.Pick("Desk", "Tisch"),
                "poster" => "Poster",
                "coffee" => ClickerStrings.Pick("Coffee", "Kaffee"),
                "sofa" => "Sofa",
                "fridge" => ClickerStrings.Pick("Fridge", "Kühlschrank"),
                "aircon" => ClickerStrings.Pick("Aircon", "Klima"),
                "beanbag" => ClickerStrings.Pick("Bean bag", "Sitzsack"),
                "aquarium" => "Aquarium",
                _ => "Arcade",
            };

            private static string staffShort(string key) => key switch
            {
                "moderator" => "Moderator",
                "developer" => ClickerStrings.Pick("Developer", "Entwickler"),
                "designer" => "Designer",
                _ => "Community",
            };

            private static IconUsage staffIcon(string key) => key switch
            {
                "moderator" => FontAwesome.Solid.Gavel,
                "developer" => FontAwesome.Solid.Code,
                "designer" => FontAwesome.Solid.PaintBrush,
                _ => FontAwesome.Solid.Comments,
            };

            public static IconUsage BoostIcon(string key) => key switch
            {
                "pizza" => FontAwesome.Solid.PizzaSlice,
                "giveaway" => FontAwesome.Solid.Gift,
                _ => FontAwesome.Solid.Tools,
            };

            private static IconUsage boostIcon(string key) => BoostIcon(key);

            private static string staffBonus(TycoonStaff role)
            {
                if (role.DonationPerHead > 0) return ClickerStrings.Pick($"donations +{role.DonationPerHead * 100:0} % each", $"Spenden +{role.DonationPerHead * 100:0} % je Kopf");
                if (role.InflowPerHead > 0) return ClickerStrings.Pick($"inflow +{role.InflowPerHead * 100:0} % each", $"Zulauf +{role.InflowPerHead * 100:0} % je Kopf");
                if (role.ComfortPerHead > 0) return ClickerStrings.Pick($"comfort +{role.ComfortPerHead} each", $"Gemütlichkeit +{role.ComfortPerHead} je Kopf");

                return ClickerStrings.Pick($"reputation regen +{role.RegenPerHead * 100:0} % each", $"Ruf-Erholung +{role.RegenPerHead * 100:0} % je Kopf");
            }

            // ------------------------------------------------------------ pieces

            /// <summary>A team member: a bigger figure in the role's colour with a badge, wandering around the office.</summary>
            private partial class StaffFigure : CompositeDrawable
            {
                private readonly Random rng;
                private readonly Vector2 min;
                private readonly Vector2 max;
                private double nextMove;

                public StaffFigure(string role, Random rng, Vector2 min, Vector2 max)
                {
                    this.rng = rng;
                    this.min = min;
                    this.max = max;
                    Origin = Anchor.Centre;
                    Size = new Vector2(12, 18);

                    var colour = Color4Extensions.FromHex(role switch
                    {
                        "moderator" => "66bb6a",
                        "developer" => "42a5f5",
                        "designer" => "ffa726",
                        _ => "ec407a",
                    });

                    InternalChildren = new Drawable[]
                    {
                        new Box { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Size = new Vector2(6), Colour = Color4Extensions.FromHex("F5CD30") },
                        new Box { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Y = 6.5f, Size = new Vector2(10, 7), Colour = colour },
                        new Box { Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, X = 1, Size = new Vector2(3.5f, 4), Colour = Color4Extensions.FromHex("1B2A35") },
                        new Box { Anchor = Anchor.BottomRight, Origin = Anchor.BottomRight, X = -1, Size = new Vector2(3.5f, 4), Colour = Color4Extensions.FromHex("1B2A35") },
                        new SpriteIcon { Anchor = Anchor.TopRight, Origin = Anchor.Centre, Y = 8, Size = new Vector2(7), Icon = staffIcon(role), Colour = Color4.White },
                    };
                }

                protected override void Update()
                {
                    base.Update();
                    if (Time.Current < nextMove) return;

                    nextMove = Time.Current + 2500 + rng.NextDouble() * 4000;
                    var target = new Vector2(min.X + (float)rng.NextDouble() * (max.X - min.X), min.Y + (float)rng.NextDouble() * (max.Y - min.Y));
                    this.MoveTo(target, 1200 + rng.NextDouble() * 800, Easing.InOutSine);
                }
            }

            /// <summary>The plot's name board above the servers, like the owner sign of a tycoon.</summary>
            private partial class OwnerSign : CompositeDrawable
            {
                private readonly OsuSpriteText nameText;
                private readonly FillFlowContainer stars;
                private readonly UpdateableAvatar avatar;
                private string shownName = string.Empty;
                private int shownStars = -1;
                private int shownUser = -1;

                public OwnerSign()
                {
                    Padding = new MarginPadding { Horizontal = 3, Top = 2, Bottom = 1 };

                    InternalChild = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 5,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.4f), Radius = 4, Offset = new Vector2(0, 2) },
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("1B2A35") },
                            new Box { RelativeSizeAxes = Axes.X, Height = 6, Colour = Color4Extensions.FromHex("34475c") },
                            avatar = new UpdateableAvatar(isInteractive: false) { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 6, Y = 2, Size = new Vector2(24), Masking = true, CornerRadius = 5 },
                            nameText = new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 36, Y = 2, Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold), Shadow = true },
                            stars = new FillFlowContainer { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, X = -46, Y = 2, AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(1, 0) },
                            new OsuSpriteText { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, X = -8, Y = 2, Text = "TYCOON", Font = OsuFont.GetFont(size: 8, weight: FontWeight.Bold), Colour = Color4Extensions.FromHex("F5CD30") },
                        },
                    };
                }

                /// <summary>The name, the player's avatar and one star per season played, up to five.</summary>
                public void Set(string name, int prestige, APIUser? user)
                {
                    int userId = user?.Id ?? 0;

                    if (userId != shownUser)
                    {
                        shownUser = userId;
                        avatar.User = user;
                    }

                    if (name != shownName)
                    {
                        shownName = name;
                        nameText.Text = name;
                    }

                    int count = Math.Min(5, prestige);
                    if (count == shownStars) return;

                    shownStars = count;
                    stars.Clear();

                    for (int i = 0; i < count; i++)
                        stars.Add(new SpriteIcon { Size = new Vector2(7), Icon = FontAwesome.Solid.Star, Colour = Color4Extensions.FromHex("F5CD30") });
                }

                protected override void Update()
                {
                    base.Update();
                    nameText.Scale = new Vector2(Math.Min(1, (DrawWidth - 96 - Math.Max(0, shownStars) * 8) / Math.Max(1, nameText.DrawWidth)));
                }
            }

            /// <summary>A soft dark blob drifting over the floor: the shadow of a cloud.</summary>
            private partial class CloudShadow : CompositeDrawable
            {
                private readonly float speed;

                public CloudShadow(Random rng)
                {
                    speed = 6 + (float)rng.NextDouble() * 6;
                    Position = new Vector2((float)rng.NextDouble() * WIDTH, (float)rng.NextDouble() * HEIGHT);
                    Origin = Anchor.Centre;
                    Size = new Vector2(150, 70);
                    Alpha = 0.1f;

                    InternalChildren = new Drawable[]
                    {
                        new Circle { Anchor = Anchor.CentreLeft, Origin = Anchor.Centre, X = 45, Size = new Vector2(80, 60), Colour = Color4.Black },
                        new Circle { Anchor = Anchor.Centre, Origin = Anchor.Centre, Y = -6, Size = new Vector2(90, 70), Colour = Color4.Black },
                        new Circle { Anchor = Anchor.CentreRight, Origin = Anchor.Centre, X = -40, Size = new Vector2(70, 50), Colour = Color4.Black },
                    };
                }

                protected override void Update()
                {
                    base.Update();
                    X += (float)(speed * Clock.ElapsedFrameTime / 1000);

                    if (X > WIDTH + 100)
                    {
                        X = -100;
                        Y = (float)(RNG.NextDouble() * HEIGHT);
                    }
                }
            }

            /// <summary>The delivery drone: a dark body with two spinning rotors.</summary>
            private partial class Drone : CompositeDrawable
            {
                private readonly Box left;
                private readonly Box right;

                public Drone()
                {
                    Origin = Anchor.Centre;
                    Size = new Vector2(30, 12);

                    InternalChildren = new Drawable[]
                    {
                        new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(14, 7), Colour = Color4Extensions.FromHex("37474f") },
                        new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Y = 1, Size = new Vector2(4, 3), Colour = Color4Extensions.FromHex("ff5252") },
                        left = new Box { Anchor = Anchor.CentreLeft, Origin = Anchor.Centre, X = 5, Size = new Vector2(12, 2), Colour = Color4Extensions.FromHex("b0bec5") },
                        right = new Box { Anchor = Anchor.CentreRight, Origin = Anchor.Centre, X = -5, Size = new Vector2(12, 2), Colour = Color4Extensions.FromHex("b0bec5") },
                    };
                }

                protected override void Update()
                {
                    base.Update();
                    float spin = (float)(Clock.ElapsedFrameTime * 1.5);
                    left.Rotation += spin;
                    right.Rotation -= spin;
                }
            }

            /// <summary>A crisis over the server block: click it often enough before the bar runs out.</summary>
            private partial class CrisisOverlay : OsuClickableContainer
            {
                private readonly int needed;
                private readonly double duration;
                private readonly Action<bool> done;
                private readonly Container frame;
                private readonly OsuSpriteText counter;
                private readonly Box timer;
                private int clicks;
                private double started;
                private bool finished;

                public CrisisOverlay(int needed, double duration, Action<bool> done)
                {
                    this.needed = needed;
                    this.duration = duration;
                    this.done = done;
                    TooltipText = ClickerStrings.Text("Emergency reboot: click!", "Notfall-Reboot: klicken!");

                    Action = () =>
                    {
                        if (finished) return;

                        clicks++;
                        counter.Text = $"REBOOT {clicks}/{needed}";
                        frame.FlashColour(Color4.White, 150);
                        counter.ScaleTo(1.25f, 60).Then().ScaleTo(1, 200, Easing.OutQuint);
                        if (clicks >= needed) finish(true);
                    };

                    Children = new Drawable[]
                    {
                        frame = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 10,
                            BorderThickness = 4,
                            BorderColour = Color4Extensions.FromHex("ff5252"),
                            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("ff5252").Opacity(0.15f) },
                        },
                        counter = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = $"REBOOT 0/{needed}",
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                            Shadow = true,
                        },
                        new Container
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Y = -10,
                            RelativeSizeAxes = Axes.X,
                            Height = 6,
                            Padding = new MarginPadding { Horizontal = 12 },
                            Child = new CircularContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.5f) },
                                    timer = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("ff5252") },
                                },
                            },
                        },
                    };
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    started = Time.Current;
                    this.FadeInFromZero(200);
                }

                protected override void Update()
                {
                    base.Update();
                    if (finished) return;

                    double progress = (Time.Current - started) / duration;
                    timer.Width = (float)Math.Clamp(1 - progress, 0, 1);
                    frame.Alpha = 0.75f + 0.25f * (float)Math.Sin(Time.Current / 120);
                    if (progress >= 1) finish(false);
                }

                private void finish(bool success)
                {
                    finished = true;
                    frame.Alpha = 1;
                    frame.BorderColour = success ? Color4Extensions.FromHex("66bb6a") : Color4Extensions.FromHex("ff5252");
                    counter.Text = success ? ClickerStrings.Pick("REBOOTED", "NEU GESTARTET") : ClickerStrings.Pick("TOO LATE", "ZU SPÄT");
                    done(success);
                    this.Delay(700).FadeOut(300).Expire();
                }
            }

            /// <summary>A well-known player: a golden avatar with a crown, bigger than the rest, worth a click.</summary>
            private partial class VipFigure : OsuClickableContainer
            {
                private static readonly Color4 gold = Color4Extensions.FromHex("F5CD30");
                private readonly SpriteIcon crown;
                private bool signed;

                public VipFigure(Action<Vector2> autograph)
                {
                    Origin = Anchor.Centre;
                    Size = new Vector2(22, 28);
                    TooltipText = ClickerStrings.Text("A well-known player: click for an autograph", "Ein bekannter Spieler: anklicken für ein Autogramm");

                    Action = () =>
                    {
                        if (signed) return;

                        signed = true;
                        autograph(ScreenSpaceDrawQuad.Centre);
                        this.ScaleTo(1.4f, 120).Then().ScaleTo(1, 300, Easing.OutBack);
                    };

                    Children = new Drawable[]
                    {
                        new Circle { Anchor = Anchor.BottomCentre, Origin = Anchor.Centre, Size = new Vector2(30), Colour = gold.Opacity(0.25f), Blending = BlendingParameters.Additive },
                        new Figure(gold) { Anchor = Anchor.BottomCentre, Origin = Anchor.BottomCentre, Scale = new Vector2(1.3f) },
                        crown = new SpriteIcon { Anchor = Anchor.TopCentre, Origin = Anchor.BottomCentre, Y = 4, Size = new Vector2(11), Icon = FontAwesome.Solid.Crown, Colour = gold },
                    };
                }

                protected override void Update()
                {
                    base.Update();
                    crown.Y = 4 + (float)Math.Sin(Time.Current / 250) * 1.5f;
                }
            }

            /// <summary>A label that shrinks to <see cref="MaxWidth"/> instead of being cut off.</summary>
            private partial class FitText : OsuSpriteText
            {
                public float MaxWidth = 50;

                protected override void Update()
                {
                    base.Update();
                    Scale = new Vector2(Math.Min(1, MaxWidth / Math.Max(1, DrawWidth)));
                }
            }

            private partial class Desk : CompositeDrawable
            {
                public Desk()
                {
                    Size = new Vector2(CELL * 0.7f, CELL * 0.42f);

                    InternalChildren = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 3,
                            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("6d5a4b") },
                        },
                        new Box { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Y = 2, Size = new Vector2(10, 7), Colour = Color4Extensions.FromHex("8fd3ff") },
                    };
                }
            }

            /// <summary>A street lamp: grey by day, warm and glowing at night.</summary>
            private partial class Lamp : CompositeDrawable
            {
                private static readonly Color4 day = Color4Extensions.FromHex("8a8f9c");
                private static readonly Color4 lit = Color4Extensions.FromHex("fff0b0");

                private readonly Circle glow;
                private readonly Circle bulb;

                public Lamp()
                {
                    Origin = Anchor.BottomCentre;
                    Size = new Vector2(16, 18);

                    InternalChildren = new Drawable[]
                    {
                        new Box { Anchor = Anchor.BottomCentre, Origin = Anchor.BottomCentre, Size = new Vector2(2, 13), Colour = Color4Extensions.FromHex("596273") },
                        glow = new Circle { Anchor = Anchor.TopCentre, Origin = Anchor.Centre, Y = 4, Size = new Vector2(28), Colour = Color4Extensions.FromHex("ffd27a"), Alpha = 0, Blending = BlendingParameters.Additive },
                        bulb = new Circle { Anchor = Anchor.TopCentre, Origin = Anchor.Centre, Y = 4, Size = new Vector2(6), Colour = day },
                    };
                }

                public void SetNight(float night)
                {
                    glow.Alpha = night * 0.35f;
                    bulb.Colour = Interpolation.ValueAt(night, day, lit, 0, 1);
                }
            }

            /// <summary>A shelf with one trophy per milestone; they light up when reached.</summary>
            private partial class TrophyShelf : CompositeDrawable
            {
                private static readonly Color4 unlit = Color4.White.Opacity(0.12f);
                private readonly List<SpriteIcon> icons = new List<SpriteIcon>();

                public TrophyShelf(int count)
                {
                    InternalChild = new Box { RelativeSizeAxes = Axes.X, Height = 5, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Y = -6, Colour = Color4Extensions.FromHex("4b3f36") };

                    for (int i = 0; i < count; i++)
                    {
                        var icon = new SpriteIcon
                        {
                            Origin = Anchor.BottomCentre,
                            Position = new Vector2(CELL * (i + 0.5f), CELL - 7),
                            Size = new Vector2(14),
                            Icon = FontAwesome.Solid.Trophy,
                            Colour = unlit,
                        };
                        icons.Add(icon);
                        AddInternal(icon);
                    }
                }

                public void Light(int index, bool animate)
                {
                    if (index < 0 || index >= icons.Count) return;

                    var icon = icons[index];
                    icon.FadeColour(Color4Extensions.FromHex("ffd54f"), animate ? 400 : 0);
                    if (animate) icon.ScaleTo(1.7f).ScaleTo(1, 500, Easing.OutBack);
                }

                public void Reset()
                {
                    foreach (var icon in icons)
                    {
                        icon.ClearTransforms();
                        icon.Colour = unlit;
                        icon.Scale = Vector2.One;
                    }
                }
            }

            /// <summary>Red "LAG" over the servers while they are overloaded.</summary>
            private partial class LagSign : CompositeDrawable
            {
                private bool shown;

                public LagSign()
                {
                    Origin = Anchor.Centre;
                    AutoSizeAxes = Axes.Both;
                    Alpha = 0;

                    InternalChild = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 6,
                        BorderThickness = 2,
                        BorderColour = Color4.White,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("d32f2f") },
                            new OsuSpriteText { Text = "LAG", Margin = new MarginPadding { Horizontal = 8, Vertical = 3 }, Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold) },
                        },
                    };
                }

                public void SetVisible(bool visible)
                {
                    if (visible == shown) return;

                    shown = visible;
                    this.FadeTo(visible ? 1 : 0, 300);
                }

                protected override void Update()
                {
                    base.Update();
                    if (shown) Scale = new Vector2(1 + 0.08f * (float)Math.Sin(Time.Current / 150));
                }
            }

            /// <summary>A pill sliding in from the top of the plot: milestones, quests, supply drops, hires.</summary>
            private partial class PlotBanner : CompositeDrawable
            {
                private readonly Box background;
                private readonly SpriteIcon icon;
                private readonly OsuSpriteText text;

                public PlotBanner()
                {
                    Anchor = Anchor.TopCentre;
                    Origin = Anchor.TopCentre;
                    AutoSizeAxes = Axes.Both;
                    Y = -40;
                    Alpha = 0;
                    Masking = true;
                    CornerRadius = 14;
                    EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.5f), Radius = 8, Offset = new Vector2(0, 3) };

                    InternalChildren = new Drawable[]
                    {
                        background = new Box { RelativeSizeAxes = Axes.Both },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Padding = new MarginPadding { Horizontal = 14, Vertical = 7 },
                            Children = new Drawable[]
                            {
                                icon = new SpriteIcon { Size = new Vector2(16), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                text = new OsuSpriteText { Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Shadow = true },
                            },
                        },
                    };
                }

                public void Show(IconUsage iconUsage, LocalisableString message, Color4 colour)
                {
                    icon.Icon = iconUsage;
                    text.Text = message;
                    background.Colour = colour.Darken(0.35f);
                    ClearTransforms();
                    this.MoveToY(10, 400, Easing.OutBack).FadeIn(200).Delay(3600).FadeOut(400).MoveToY(-40, 400, Easing.In);
                }
            }

            /// <summary>A crate that drops next to the walkway and waits 25 seconds for a click.</summary>
            private partial class SupplyCrate : OsuClickableContainer
            {
                private const double lifetime = 25_000;

                private readonly Vector2 target;
                private readonly Container body;
                private readonly Box timer;
                private double born;
                private bool done;

                public SupplyCrate(Vector2 target, Action<Vector2> claim)
                {
                    this.target = target;
                    Origin = Anchor.Centre;
                    Size = new Vector2(34);
                    Position = target - new Vector2(0, 140);
                    TooltipText = ClickerStrings.Text("Supply drop: click it before it is gone", "Lieferung: anklicken, bevor sie weg ist");

                    Action = () =>
                    {
                        if (done) return;

                        done = true;
                        claim(ScreenSpaceDrawQuad.Centre);
                        this.ScaleTo(1.5f, 150, Easing.OutQuint).FadeOut(200).Expire();
                    };

                    Children = new Drawable[]
                    {
                        body = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 6,
                            BorderThickness = 2,
                            BorderColour = Color4Extensions.FromHex("ffd54f"),
                            EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow, Colour = Color4Extensions.FromHex("ffd54f").Opacity(0.5f), Radius = 10 },
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("8d6e63") },
                                new SpriteIcon { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(18), Icon = FontAwesome.Solid.Gift, Colour = Color4Extensions.FromHex("ffd54f") },
                            },
                        },
                        new Container
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.TopCentre,
                            Y = 4,
                            Size = new Vector2(34, 4),
                            Masking = true,
                            CornerRadius = 2,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.4f) },
                                timer = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("ffd54f") },
                            },
                        },
                    };
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    born = Time.Current;
                    this.MoveTo(target, 900, Easing.OutBounce);
                    this.ScaleTo(0.6f).ScaleTo(1, 400, Easing.OutBack);
                }

                protected override void Update()
                {
                    base.Update();
                    if (done) return;

                    double age = Time.Current - born;
                    timer.Width = (float)Math.Clamp(1 - age / lifetime, 0, 1);
                    body.Y = (float)Math.Sin(Time.Current / 300) * 2;

                    if (age > lifetime)
                    {
                        done = true;
                        this.FadeOut(400).Expire();
                    }
                }
            }
        }
    }
}

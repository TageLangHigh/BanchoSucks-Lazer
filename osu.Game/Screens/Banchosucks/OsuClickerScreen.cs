// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: osu! Clicker, a small idle clicker game in osu! style.
    /// </summary>
    /// <remarks>
    /// Click the circle or tap the osu!standard keys (the player's own bindings) for PP, buy producers that earn
    /// PP on their own and mod upgrades that multiply clicks or production. A bonus spinner shows up every one to
    /// two and a half minutes. Progress is kept in banchosucks/osu-clicker.json in the game's storage; while the
    /// game is closed producers keep working at half speed for up to eight hours. Logged-in players send their
    /// progress to the lazer server (plugin banchosucks_clicker) for the leaderboard.
    /// </remarks>
    public partial class OsuClickerScreen : OsuScreen
    {
        private const string save_file = "osu-clicker.json";
        private const double submit_interval = 60_000;
        private const double first_submit_delay = 10_000;
        private const float side_panel_width = 490;

        // osu!standard key bindings are stored under the ruleset short name; OsuAction.LeftButton = 0, OsuAction.RightButton = 1
        private const string osu_ruleset = "osu";
        private const int osu_left_button = 0;
        private const int osu_right_button = 1;

        private static readonly InputKey[] default_tap_keys = { InputKey.Z, InputKey.X };

        // every InputKey a keyboard can produce; mouse buttons in the osu! bindings are covered by clicking the circle
        private static readonly HashSet<InputKey> keyboard_keys = Enum.GetValues<Key>().Select(KeyCombination.FromKey).Where(k => k != InputKey.None).ToHashSet();

        private static readonly ClickerProducer[] producers = ClickerBalance.PRODUCERS;
        private static readonly ClickerUpgrade[] upgrades = ClickerBalance.UPGRADES;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ReadableKeyCombinationProvider keyCombinationProvider { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private readonly Random random = new Random();
        private ClickerState state = new ClickerState();
        private Storage saveStorage = null!;
        private Sample? clickSample;
        private Sample? comboBreakSample;
        private Sample? bonusSample;

        private OsuSpriteText pointsText = null!;
        private OsuSpriteText rateText = null!;
        private OsuSpriteText statsText = null!;
        private OsuSpriteText messageText = null!;
        private OsuSpriteText bpmText = null!;
        private OsuSpriteText bpmDetailText = null!;
        private Container floatingLayer = null!;
        private Box flash = null!;
        private ClickerCircle circle = null!;
        private ClickerComboCounter comboCounter = null!;
        private ClickerBuildingStrip buildingStrip = null!;
        private ClickerLeaderboardPanel leaderboard = null!;
        private Container shopContent = null!;
        private ClickerTabButton shopTab = null!;
        private ClickerTabButton leaderboardTab = null!;

        private readonly TapBpmMeter bpmMeter = new TapBpmMeter();
        private InputKey[] tapKeys = default_tap_keys;
        private string tapKeysText = "Z / X";
        private IDisposable? keyBindingSubscription;

        private double nextBonusAt;
        private double lastTapTime = double.MinValue;
        private int comboColourIndex;
        private double lastSubmittedTotal = -1;

        // recalculated after loading and after every purchase instead of every frame
        private double clickMultiplier = 1;
        private double productionMultiplier = 1;
        private double clickShare;
        private double perSecond;

        private double clickValue => clickMultiplier + perSecond * clickShare;

        private void recalculate()
        {
            var active = upgrades.Where(owned).ToArray();
            clickMultiplier = active.Aggregate(1.0, (value, upgrade) => value * upgrade.Click);
            productionMultiplier = active.Aggregate(1.0, (value, upgrade) => value * upgrade.Production);
            clickShare = active.Sum(upgrade => upgrade.ClickShare);
            perSecond = producers.Sum(p => ownedCount(p) * p.PerSecond) * productionMultiplier;
        }

        private bool owned(ClickerUpgrade upgrade) => state.Upgrades.Contains(upgrade.Id);
        private int ownedCount(ClickerProducer producer) => state.Producers.GetValueOrDefault(producer.Id);
        private double cost(ClickerProducer producer) => Math.Ceiling(producer.BaseCost * Math.Pow(ClickerBalance.COST_GROWTH, ownedCount(producer)));

        private static string format(double value) => ClickerFormat.Number(value);

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            saveStorage = storage.GetStorageForDirectory("banchosucks");
            clickSample = audio.Samples.Get(@"Gameplay/normal-hitnormal");
            comboBreakSample = audio.Samples.Get(@"Gameplay/combobreak");
            bonusSample = audio.Samples.Get(@"Gameplay/spinnerbonus");
            string? welcome = loadState();

            Color4 pink = colours.Pink;
            Color4 purple = Color4Extensions.FromHex("6b3fa0");

            InternalChildren = new Drawable[]
            {
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
                                        Clicked = tap,
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
                                                Text = welcome ?? string.Empty,
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
                                        Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("241a2c").Opacity(0.92f), Color4Extensions.FromHex("15111a").Opacity(0.95f)),
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
                                                            Spacing = new Vector2(18, 0),
                                                            Padding = new MarginPadding { Horizontal = 18 },
                                                            Children = new Drawable[]
                                                            {
                                                                shopTab = new ClickerTabButton("Shop", FontAwesome.Solid.ShoppingCart, pink, () => showTab(false)),
                                                                leaderboardTab = new ClickerTabButton("Rangliste", FontAwesome.Solid.Trophy, colours.Yellow, () => showTab(true)),
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
                                                            Child = new OsuScrollContainer
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Child = createShop(),
                                                            },
                                                        },
                                                        leaderboard = new ClickerLeaderboardPanel
                                                        {
                                                            Alpha = 0,
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
                },
            };

            if (welcome != null)
                messageText.Delay(8000).FadeOut(1000);
        }

        private Drawable createShop()
        {
            var shop = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Padding = new MarginPadding(14),
            };

            shop.Add(sectionHeader("Gebäude"));

            foreach (var producer in producers)
            {
                shop.Add(new ClickerShopRow(ClickerShopRow.CreateIcon(producer.Icon, producer.Colour), producer.Colour, producer.Name, producer.Description,
                    () => $"{ownedCount(producer)}× · {format(producer.PerSecond * productionMultiplier)} PP/s pro Stück",
                    () => $"{format(cost(producer))} PP",
                    () => state.Points >= cost(producer),
                    () => buyProducer(producer),
                    () => ownedCount(producer) > 0 ? ownedCount(producer).ToString("N0") : string.Empty));
            }

            shop.Add(sectionHeader("Upgrades"));

            // mod upgrades show the real osu! mod icons
            Ruleset? osu = rulesets.GetRuleset(osu_ruleset)?.CreateInstance();

            foreach (var upgrade in upgrades)
            {
                Drawable icon = ClickerShopRow.CreateIcon(upgrade.Icon, colours.Yellow.Darken(0.3f));

                if (upgrade.ModAcronym != null && osu?.CreateModFromAcronym(upgrade.ModAcronym) is { } mod)
                {
                    icon = new ModIcon(mod, showTooltip: false, showExtendedInformation: false)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(0.62f),
                    };
                }

                shop.Add(new ClickerShopRow(icon, colours.Yellow, upgrade.Name, upgrade.Description,
                    () => owned(upgrade) ? "aktiv" : "einmalig",
                    () => owned(upgrade) ? "gekauft" : $"{format(upgrade.Cost)} PP",
                    () => !owned(upgrade) && state.Points >= upgrade.Cost,
                    () => buyUpgrade(upgrade)));
            }

            return shop;
        }

        private void showTab(bool showLeaderboard)
        {
            shopTab.Active = !showLeaderboard;
            leaderboardTab.Active = showLeaderboard;

            shopContent.FadeTo(showLeaderboard ? 0 : 1, 200, Easing.OutQuint);
            leaderboard.FadeTo(showLeaderboard ? 1 : 0, 200, Easing.OutQuint);

            if (showLeaderboard)
            {
                submitProgress();
                leaderboard.Refresh();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            shopTab.Active = true;
            bpmMeter.Best = state.BestBpm;
            updateVisuals();

            // follows changes made in the settings while the game is open
            keyBindingSubscription = realm.RegisterForNotifications(
                r => r.All<RealmKeyBinding>().Where(b => b.RulesetName == osu_ruleset && b.Variant == 0),
                (bindings, _) => updateTapKeys(bindings));

            scheduleBonus();
            Scheduler.AddDelayed(save, 15_000, true);
            Scheduler.AddDelayed(() =>
            {
                submitProgress();
                Scheduler.AddDelayed(submitProgress, submit_interval, true);
            }, first_submit_delay);
        }

        protected override void Update()
        {
            base.Update();

            double gained = perSecond * Time.Elapsed / 1000;
            state.Points += gained;
            state.TotalEarned += gained;

            pointsText.Text = $"{format(state.Points)} PP";
            rateText.Text = $"{format(perSecond)} PP pro Sekunde · {format(clickValue)} PP pro Klick";
            statsText.Text = $"{state.Clicks:N0} Klicks · Max-Combo {state.BestCombo:N0} · insgesamt {format(state.TotalEarned)} PP verdient";

            double tapsPerSecond = bpmMeter.TapsPerSecond(Time.Current);
            bpmText.Text = $"{tapsPerSecond * 15:0} BPM";
            bpmText.Colour = tapsPerSecond > 0 && tapsPerSecond * 15 >= state.BestBpm ? colours.Yellow : Color4.White;
            bpmDetailText.Text = $"{tapsPerSecond:0.0} Taps/s · Rekord {state.BestBpm:0} BPM · Tasten {tapKeysText}";

            // a record is announced once the stream is over
            if (tapsPerSecond == 0 && Math.Round(bpmMeter.Best) > state.BestBpm)
            {
                state.BestBpm = Math.Round(bpmMeter.Best);
                showMessage($"Neuer BPM-Rekord: {state.BestBpm:0} BPM!");
            }

            // combo breaks after a second without a tap
            if (comboCounter.Current > 0 && Time.Current - lastTapTime > ClickerBalance.COMBO_TIMEOUT_MS)
            {
                if (comboCounter.Current >= 20)
                    comboBreakSample?.Play();
                comboCounter.Break();
            }

            if (Time.Current >= nextBonusAt)
                spawnBonus();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // the keys the player taps circles with in osu!standard
            if (tapKeys.Contains(KeyCombination.FromKey(e.Key)))
            {
                if (!e.Repeat)
                {
                    circle.Press();
                    tap(null);
                }

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

        private void tap(Vector2? screenPosition)
        {
            double value = clickValue;
            state.Points += value;
            state.TotalEarned += value;
            state.Clicks++;
            bpmMeter.Tap(Time.Current);
            lastTapTime = Time.Current;
            clickSample?.Play();

            comboCounter.Increment();
            state.BestCombo = Math.Max(state.BestCombo, comboCounter.Current);

            Color4 comboColour = ClickerBalance.COMBO_COLOURS[comboColourIndex++ % ClickerBalance.COMBO_COLOURS.Length];
            circle.Hit(comboColour);

            if (comboCounter.Current % 100 == 0)
            {
                circle.Kiai();
                flash.FadeTo(0.12f, 40).Then().FadeOut(500, Easing.OutQuint);
                showMessage($"{comboCounter.Current}x Combo!");
            }

            Vector2 position = screenPosition ?? circle.ToScreenSpace(circle.DrawSize / 2 + new Vector2((float)random.NextDouble() * 220 - 110, -20 - (float)random.NextDouble() * 60));
            spawnFloating(position, $"+{format(value)}", comboColour, 26);
        }

        private void buyProducer(ClickerProducer producer)
        {
            double price = cost(producer);
            if (state.Points < price)
                return;

            state.Points -= price;
            state.Producers[producer.Id] = ownedCount(producer) + 1;
            recalculate();
            updateVisuals();
        }

        private void buyUpgrade(ClickerUpgrade upgrade)
        {
            if (owned(upgrade) || state.Points < upgrade.Cost)
                return;

            state.Points -= upgrade.Cost;
            state.Upgrades.Add(upgrade.Id);
            recalculate();
            showMessage($"{upgrade.Name} aktiviert!");
        }

        private void updateVisuals()
        {
            circle.SetCursorCount(ownedCount(producers[0]));
            buildingStrip.SetCounts(producers.Select(p => (p, ownedCount(p))));
        }

        private void scheduleBonus() => nextBonusAt = Time.Current + 60_000 + random.NextDouble() * 90_000;

        private void spawnBonus()
        {
            scheduleBonus();

            var bonus = new BonusSpinner(colours.Yellow)
            {
                RelativePositionAxes = Axes.Both,
                Position = new Vector2(0.12f + (float)random.NextDouble() * 0.76f, 0.25f + (float)random.NextDouble() * 0.5f),
            };

            bonus.Clicked = () =>
            {
                double reward = Math.Max(clickValue * 25, perSecond * 60);
                state.Points += reward;
                state.TotalEarned += reward;
                bonusSample?.Play();
                spawnFloating(bonus.ScreenSpaceDrawQuad.Centre, $"Spinner-Bonus! +{format(reward)}", colours.Yellow, 32);
                bonus.ScaleTo(1.5f, 200, Easing.OutQuint).FadeOut(200).Expire();
            };

            floatingLayer.Add(bonus);
            bonus.Delay(12_000).FadeOut(500).Expire();
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

        private void showMessage(string text)
        {
            messageText.ClearTransforms();
            messageText.Text = text;
            messageText.FadeIn(100).Then().Delay(4000).FadeOut(800);
        }

        private static Drawable sectionHeader(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
            Margin = new MarginPadding { Top = 8, Bottom = 2, Left = 4 },
        };

        // ------------------------------------------------------------------ leaderboard

        private ClickerSubmission createSubmission() => new ClickerSubmission
        {
            TotalEarned = Math.Floor(state.TotalEarned),
            Clicks = state.Clicks,
            BestBpm = Math.Max(state.BestBpm, Math.Round(bpmMeter.Best, 1)),
            BestCombo = state.BestCombo,
            Producers = state.Producers.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value),
            Upgrades = state.Upgrades.ToList(),
        };

        private void submitProgress()
        {
            if (api.State.Value != APIState.Online || state.TotalEarned < 1 || Math.Floor(state.TotalEarned) == lastSubmittedTotal)
                return;

            var request = new SubmitClickerScoreRequest(createSubmission());
            lastSubmittedTotal = request.Submission.TotalEarned;

            request.Success += response =>
            {
                if (IsDisposed)
                    return;

                if (response.Accepted)
                    leaderboard.SetOwnRanks(response.RankPp, response.RankBpm);
                else
                    Logger.Log($"osu! Clicker progress not accepted: {response.Reason}", LoggingTarget.Network);
            };

            api.Queue(request);
        }

        // ------------------------------------------------------------------ saving

        private string? loadState()
        {
            try
            {
                string path = saveStorage.GetFullPath(save_file);
                if (!File.Exists(path))
                    return "Klick den Kreis oder tippe mit deinen osu!-Tasten und kauf dir rechts Gebäude.";

                state = JsonSerializer.Deserialize<ClickerState>(File.ReadAllText(path)) ?? new ClickerState();
                recalculate();

                double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - state.SavedAt;
                if (away < 60 || perSecond <= 0)
                    return null;

                double earned = Math.Min(away, ClickerBalance.OFFLINE_CAP_SECONDS) * perSecond * ClickerBalance.OFFLINE_RATE;
                state.Points += earned;
                state.TotalEarned += earned;
                return $"Willkommen zurück! Deine Gebäude haben in der Zwischenzeit {format(earned)} PP gesammelt.";
            }
            catch (Exception e)
            {
                Logger.Error(e, "osu! Clicker save could not be read, starting fresh");
                state = new ClickerState();
                recalculate();
                return null;
            }
        }

        private void save()
        {
            try
            {
                state.SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                state.BestBpm = Math.Max(state.BestBpm, Math.Round(bpmMeter.Best));
                string path = saveStorage.GetFullPath(save_file, true);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(state));
            }
            catch (Exception e)
            {
                Logger.Error(e, "osu! Clicker could not be saved");
            }
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(250, Easing.OutQuint);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            save();
            submitProgress();
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            save();
            base.OnSuspending(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            keyBindingSubscription?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}

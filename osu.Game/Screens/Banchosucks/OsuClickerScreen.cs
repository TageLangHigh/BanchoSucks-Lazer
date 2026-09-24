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
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: osu! Clicker, a small idle clicker game in osu! style.
    /// </summary>
    /// <remarks>
    /// Click the circle for PP, buy producers that earn PP on their own and mod upgrades that multiply
    /// clicks or production. A bonus spinner shows up every one to two and a half minutes. Progress is
    /// kept in banchosucks/osu-clicker.json in the game's storage; while the game is closed producers
    /// keep working at half speed for up to eight hours.
    /// </remarks>
    public partial class OsuClickerScreen : OsuScreen
    {
        private const string save_file = "osu-clicker.json";
        private const double offline_rate = 0.5;
        private const double offline_cap_seconds = 8 * 3600;
        private const double cost_growth = 1.15;

        private static readonly Producer[] producers =
        {
            new Producer("cursor", "Cursor-Trail", "Ein zweiter Cursor klickt ab und zu mit.", 15, 0.1),
            new Producer("taiko", "Taiko-Trommel", "Don und Kat, ganz von allein.", 100, 1),
            new Producer("catch", "Obstkorb", "Fängt Früchte und damit PP.", 1_100, 8),
            new Producer("mania", "Mania-Tastatur", "Sieben Tasten, null Pause.", 12_000, 47),
            new Producer("mapper", "Mapper", "Mappt rund um die Uhr neue Farm-Maps.", 130_000, 260),
            new Producer("nominator", "Nominator", "Rankt Maps am Fließband.", 1_400_000, 1_400),
            new Producer("tournament", "Turnier", "Ganze Teams spielen für dich.", 20_000_000, 7_800),
            new Producer("server", "Banchosucks-Server", "Ein eigener Server nur für deine PP.", 330_000_000, 44_000),
        };

        private static readonly Upgrade[] upgrades =
        {
            new Upgrade("tablet", "Grafiktablett", "Klicks bringen doppelt so viel.", 100, click: 2),
            new Upgrade("relax", "Relax", "Jeder Klick bringt zusätzlich 1 % deiner PP pro Sekunde.", 10_000, clickShare: 0.01),
            new Upgrade("hardrock", "Hard Rock", "Klicks bringen dreimal so viel.", 50_000, click: 3),
            new Upgrade("doubletime", "Double Time", "Alle Gebäude produzieren doppelt.", 200_000, production: 2),
            new Upgrade("hidden", "Hidden", "Alle Gebäude produzieren noch einmal doppelt.", 5_000_000, production: 2),
            new Upgrade("flashlight", "Flashlight", "Jeder Klick bringt zusätzlich 5 % deiner PP pro Sekunde.", 50_000_000, clickShare: 0.05),
            new Upgrade("perfect", "Perfect", "Alle Gebäude produzieren dreimal so viel.", 500_000_000, production: 3),
        };

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly Random random = new Random();
        private ClickerState state = new ClickerState();
        private Storage saveStorage = null!;
        private Sample? clickSample;

        private OsuSpriteText pointsText = null!;
        private OsuSpriteText rateText = null!;
        private OsuSpriteText statsText = null!;
        private OsuSpriteText messageText = null!;
        private Container floatingLayer = null!;
        private double nextBonusAt;

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

        private bool owned(Upgrade upgrade) => state.Upgrades.Contains(upgrade.Id);
        private int ownedCount(Producer producer) => state.Producers.GetValueOrDefault(producer.Id);
        private double cost(Producer producer) => Math.Ceiling(producer.BaseCost * Math.Pow(cost_growth, ownedCount(producer)));

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            saveStorage = storage.GetStorageForDirectory("banchosucks");
            clickSample = audio.Samples.Get(@"Gameplay/normal-hitnormal");
            string? welcome = loadState();

            var shop = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Padding = new MarginPadding(16),
            };

            shop.Add(sectionHeader("Gebäude"));
            foreach (var producer in producers)
            {
                shop.Add(new ShopRow(producer.Name, producer.Description,
                    () => $"{ownedCount(producer)}× · {format(producer.PerSecond * productionMultiplier)} PP/s pro Stück",
                    () => $"{format(cost(producer))} PP",
                    () => state.Points >= cost(producer),
                    () => buyProducer(producer)));
            }

            shop.Add(sectionHeader("Upgrades"));
            foreach (var upgrade in upgrades)
            {
                shop.Add(new ShopRow(upgrade.Name, upgrade.Description,
                    () => owned(upgrade) ? "aktiv" : "einmalig",
                    () => owned(upgrade) ? "gekauft" : $"{format(upgrade.Cost)} PP",
                    () => !owned(upgrade) && state.Points >= upgrade.Cost,
                    () => buyUpgrade(upgrade)));
            }

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.35f,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 40, Top = 80, Bottom = 30 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 470),
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
                                                Colour = colours.Pink,
                                            },
                                            pointsText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 54, weight: FontWeight.Black),
                                            },
                                            rateText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                                                Colour = colours.Gray9,
                                            },
                                        },
                                    },
                                    new ClickerCircle(colours.Pink)
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Clicked = onCircleClicked,
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
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Black,
                                        Alpha = 0.55f,
                                    },
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = shop,
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

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scheduleBonus();
            Scheduler.AddDelayed(save, 15_000, true);
        }

        protected override void Update()
        {
            base.Update();

            double gained = perSecond * Time.Elapsed / 1000;
            state.Points += gained;
            state.TotalEarned += gained;

            pointsText.Text = $"{format(state.Points)} PP";
            rateText.Text = $"{format(perSecond)} PP pro Sekunde · {format(clickValue)} PP pro Klick";
            statsText.Text = $"{state.Clicks:N0} Klicks · insgesamt {format(state.TotalEarned)} PP verdient";

            if (Time.Current >= nextBonusAt)
                spawnBonus();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Z / X click like in gameplay
            if (!e.Repeat && (e.Key == Key.Z || e.Key == Key.X))
            {
                onCircleClicked(null);
                return true;
            }

            return base.OnKeyDown(e);
        }

        private void onCircleClicked(Vector2? screenPosition)
        {
            double value = clickValue;
            state.Points += value;
            state.TotalEarned += value;
            state.Clicks++;
            clickSample?.Play();

            Vector2 position = screenPosition ?? floatingLayer.ToScreenSpace(floatingLayer.DrawSize / 2);
            spawnFloating(position, $"+{format(value)}", Color4.White);
        }

        private void buyProducer(Producer producer)
        {
            double price = cost(producer);
            if (state.Points < price)
                return;

            state.Points -= price;
            state.Producers[producer.Id] = ownedCount(producer) + 1;
            recalculate();
        }

        private void buyUpgrade(Upgrade upgrade)
        {
            if (owned(upgrade) || state.Points < upgrade.Cost)
                return;

            state.Points -= upgrade.Cost;
            state.Upgrades.Add(upgrade.Id);
            recalculate();
            showMessage($"{upgrade.Name} aktiviert!");
        }

        private void scheduleBonus() => nextBonusAt = Time.Current + 60_000 + random.NextDouble() * 90_000;

        private void spawnBonus()
        {
            scheduleBonus();

            var bonus = new BonusSpinner(colours.Yellow)
            {
                RelativePositionAxes = Axes.Both,
                Position = new Vector2(0.15f + (float)random.NextDouble() * 0.7f, 0.25f + (float)random.NextDouble() * 0.5f),
            };

            bonus.Clicked = () =>
            {
                double reward = Math.Max(clickValue * 25, perSecond * 60);
                state.Points += reward;
                state.TotalEarned += reward;
                spawnFloating(bonus.ScreenSpaceDrawQuad.Centre, $"Spinner-Bonus! +{format(reward)}", colours.Yellow);
                bonus.FadeOut(150).Expire();
            };

            floatingLayer.Add(bonus);
            bonus.Delay(12_000).FadeOut(500).Expire();
        }

        private void spawnFloating(Vector2 screenPosition, string text, Color4 colour)
        {
            var sprite = new OsuSpriteText
            {
                Text = text,
                Origin = Anchor.Centre,
                Position = floatingLayer.ToLocalSpace(screenPosition),
                Font = OsuFont.GetFont(size: 26, weight: FontWeight.Bold),
                Colour = colour,
            };

            floatingLayer.Add(sprite);
            sprite.MoveToOffset(new Vector2((float)random.NextDouble() * 40 - 20, -90), 900, Easing.OutQuint)
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
            Margin = new MarginPadding { Top = 8, Bottom = 2 },
        };

        private static string format(double value)
        {
            if (value < 10)
                return value.ToString("0.#");
            if (value < 1_000_000)
                return Math.Floor(value).ToString("N0");

            string[] units = { "Mio", "Mrd", "Bio", "Brd", "Trio" };
            int unit = -1;
            value /= 1000;

            while (value >= 1000 && unit < units.Length - 1)
            {
                value /= 1000;
                unit++;
            }

            return unit < 0 ? $"{value:0.##} Tsd" : $"{value:0.##} {units[unit]}";
        }

        // ------------------------------------------------------------------ saving

        private string? loadState()
        {
            try
            {
                string path = saveStorage.GetFullPath(save_file);
                if (!File.Exists(path))
                    return "Klick den Kreis (oder drück Z / X) und kauf dir rechts Gebäude.";

                state = JsonSerializer.Deserialize<ClickerState>(File.ReadAllText(path)) ?? new ClickerState();
                recalculate();

                double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - state.SavedAt;
                if (away < 60 || perSecond <= 0)
                    return null;

                double earned = Math.Min(away, offline_cap_seconds) * perSecond * offline_rate;
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
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            save();
            base.OnSuspending(e);
        }

        // ------------------------------------------------------------------ data

        private record Producer(string Id, string Name, string Description, double BaseCost, double PerSecond);

        private record Upgrade(string Id, string Name, string Description, double Cost, double click = 1, double production = 1, double clickShare = 0)
        {
            public double Click => click;
            public double Production => production;
            public double ClickShare => clickShare;
        }

        private class ClickerState
        {
            public double Points { get; set; }
            public double TotalEarned { get; set; }
            public long Clicks { get; set; }
            public Dictionary<string, int> Producers { get; set; } = new Dictionary<string, int>();
            public HashSet<string> Upgrades { get; set; } = new HashSet<string>();
            public long SavedAt { get; set; }
        }

        // ------------------------------------------------------------------ drawables

        private partial class ClickerCircle : CompositeDrawable
        {
            public Action<Vector2?>? Clicked;

            private readonly CircularContainer body;
            private readonly CircularContainer approach;

            public ClickerCircle(Color4 colour)
            {
                Size = new Vector2(300);

                InternalChildren = new Drawable[]
                {
                    approach = new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = 6,
                        BorderColour = colour,
                        Alpha = 0,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
                        },
                    },
                    body = new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = 14,
                        BorderColour = Color4.White,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Colour = colour.Opacity(0.6f),
                            Radius = 30,
                        },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colour,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "osu!",
                                Font = OsuFont.GetFont(size: 96, weight: FontWeight.Black),
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // an approach circle closing in, again and again
                Scheduler.AddDelayed(() =>
                {
                    approach.ScaleTo(1.6f).FadeTo(0)
                            .ScaleTo(1f, 900)
                            .FadeTo(0.8f, 250)
                            .Then().FadeOut(150);
                }, 1100, true);
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => body.ReceivePositionalInputAt(screenSpacePos);

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                body.ScaleTo(0.94f, 60, Easing.OutQuint);
                return base.OnMouseDown(e);
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                body.ScaleTo(1f, 400, Easing.OutElastic);
                base.OnMouseUp(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                Clicked?.Invoke(e.ScreenSpaceMousePosition);
                return true;
            }
        }

        private partial class BonusSpinner : CompositeDrawable
        {
            public Action? Clicked;

            private readonly CircularContainer disc;

            public BonusSpinner(Color4 colour)
            {
                Size = new Vector2(90);
                Origin = Anchor.Centre;

                InternalChild = disc = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = 6,
                    BorderColour = Color4.White,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = colour.Opacity(0.7f),
                        Radius = 20,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colour,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(40),
                            Icon = FontAwesome.Solid.Sync,
                            Colour = Color4.Black.Opacity(0.7f),
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                this.FadeInFromZero(300).ScaleTo(0.5f).ScaleTo(1f, 500, Easing.OutElastic);
                disc.Spin(1200, RotationDirection.Clockwise);
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => disc.ReceivePositionalInputAt(screenSpacePos);

            protected override bool OnClick(ClickEvent e)
            {
                Clicked?.Invoke();
                Clicked = null;
                return true;
            }
        }

        private partial class ShopRow : CompositeDrawable
        {
            private readonly Func<string> detail;
            private readonly Func<string> buttonText;
            private readonly Func<bool> enabled;
            private readonly OsuSpriteText detailText;
            private readonly RoundedButton button;
            private string lastDetail = string.Empty;
            private string lastButtonText = string.Empty;

            public ShopRow(string title, string description, Func<string> detail, Func<string> buttonText, Func<bool> enabled, Action action)
            {
                this.detail = detail;
                this.buttonText = buttonText;
                this.enabled = enabled;

                RelativeSizeAxes = Axes.X;
                Height = 72;

                InternalChildren = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Right = 150 },
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = title,
                                Font = OsuFont.GetFont(size: 19, weight: FontWeight.Bold),
                            },
                            new TruncatingSpriteText
                            {
                                Text = description,
                                Font = OsuFont.GetFont(size: 13),
                                RelativeSizeAxes = Axes.X,
                                Alpha = 0.75f,
                            },
                            detailText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                Colour = new Color4(255, 102, 170, 255),
                            },
                        },
                    },
                    button = new RoundedButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Width = 140,
                        Action = action,
                    },
                };
            }

            protected override void Update()
            {
                base.Update();

                string newDetail = detail();
                if (newDetail != lastDetail)
                    detailText.Text = lastDetail = newDetail;

                string newButtonText = buttonText();
                if (newButtonText != lastButtonText)
                    button.Text = lastButtonText = newButtonText;

                button.Enabled.Value = enabled();
            }
        }
    }
}

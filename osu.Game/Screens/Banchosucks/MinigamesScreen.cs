// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Banchosucks.CircleRush;
using osu.Game.Screens.Banchosucks.Guess;
using osu.Game.Screens.Banchosucks.Tycoon;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: small games to pass the time between maps. Opened from the main menu (Play > Minispiele).
    /// </summary>
    public partial class MinigamesScreen : OsuScreen
    {
        private const double affordable_check_interval = 1000;

        // absent in plain test scenes; the game always provides it
        [Resolved(CanBeNull = true)]
        private ClickerEngine? engine { get; set; }

        private GameTile clickerTile = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 24),
                    Padding = new MarginPadding { Top = 70, Bottom = 40 },
                    Children = new Drawable[]
                    {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = ClickerStrings.Text("Minigames", "Minispiele"),
                        Font = OsuFont.GetFont(size: 48, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = ClickerStrings.Text("Small games for in between, only on Banchosucks.", "Kleine Spiele für zwischendurch, nur auf Banchosucks."),
                        Font = OsuFont.GetFont(size: 18),
                        Colour = colours.Gray9,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        // three tiles per row, more rows below
                        Width = 3 * 280 + 2 * 20,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(20),
                        Children = new Drawable[]
                        {
                            clickerTile = new GameTile("osu! Clicker",
                                ClickerStrings.Text("Click the circle, collect PP, buy taiko drums and mappers. With prestige tree, medals and leaderboards.",
                                    "Klick den Kreis, sammle PP, kauf dir Taiko-Trommeln und Mapper. Mit Prestige-Baum, Medaillen und Ranglisten."),
                                FontAwesome.Solid.MousePointer, colours.Pink, () => this.Push(new OsuClickerScreen())),
                            new GameTile("Circle Rush",
                                ClickerStrings.Text("Click circles before they vanish: singles, bursts and streams, faster and faster. One minute or five lives, four difficulties, EZ/HD/HR/FL, leaderboards.",
                                    "Klick Kreise, bevor sie verschwinden: einzeln, als Burst und als Stream, immer schneller. Eine Minute oder fünf Leben, vier Schwierigkeiten, EZ/HD/HR/FL, Ranglisten."),
                                FontAwesome.Solid.Bullseye, colours.Blue, () => this.Push(new CircleRushScreen())),
                            new GameTile(ClickerStrings.Text("Server Tycoon", "Server-Tycoon"),
                                ClickerStrings.Text("Run your own osu! server: attract players, buy hardware before it lags, survive cheater waves and broken deploys. Idle game with seasons and leaderboards.",
                                    "Betreib deinen eigenen osu!-Server: Lock Spieler an, kauf Hardware, bevor es laggt, überstehe Cheater-Wellen und kaputte Deploys. Idle-Spiel mit Saisons und Ranglisten."),
                                FontAwesome.Solid.Server, colours.Purple, () => this.Push(new TycoonScreen())),
                            new GameTile(ClickerStrings.Text("Song Guesser", "Song-Guesser"),
                                ClickerStrings.Text("A snippet plays, four titles to pick from. The most played maps on banchosucks until you miss, or ten rounds about your own library.",
                                    "Ein Schnipsel läuft, vier Titel zur Wahl. Die meistgespielten Maps auf banchosucks bis zum ersten Fehler, oder zehn Runden über deine Bibliothek."),
                                FontAwesome.Solid.Music, colours.Pink, () => this.Push(new SongGuessScreen())),
                            new GameTile(ClickerStrings.Text("Background Guesser", "Hintergrund-Guesser"),
                                ClickerStrings.Text("Which song has this background? Your library or the most played maps, until you miss.", "Welcher Song hat diesen Hintergrund? Deine Bibliothek oder die meistgespielten Maps, bis zum ersten Fehler."),
                                FontAwesome.Solid.Image, colours.Blue, () => this.Push(new BackgroundGuessScreen())),
                        },
                    },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // the tile pulses while something in the shop is affordable
            Scheduler.AddDelayed(() => clickerTile.Pulsing = engine?.AnythingAffordable() == true, affordable_check_interval, true);
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
            this.FadeOut(200);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(200);
            return base.OnExiting(e);
        }

        private partial class GameTile : OsuClickableContainer
        {
            private readonly Container content;
            private readonly Box glow;
            private readonly Color4 colour;
            private bool pulsing;

            public GameTile(LocalisableString title, LocalisableString description, IconUsage icon, Color4 colour, System.Action open)
            {
                this.colour = colour;
                Size = new Vector2(280, 320);
                Action = open;
                Masking = true;
                CornerRadius = 20;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = colour.Opacity(0),
                    Radius = 30,
                };

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour.Darken(1.2f).Opacity(0.85f),
                    },
                    glow = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                        Alpha = 0,
                    },
                    content = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(20),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Size = new Vector2(150),
                                Margin = new MarginPadding { Top = 10 },
                                Masking = true,
                                CornerRadius = 75,
                                BorderThickness = 8,
                                BorderColour = Color4.White,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(56),
                                        Icon = icon,
                                    },
                                },
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 6),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Text = title,
                                        Font = OsuFont.GetFont(size: 26, weight: FontWeight.Bold),
                                    },
                                    new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 15))
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        TextAnchor = Anchor.TopCentre,
                                        Text = description,
                                    },
                                },
                            },
                        },
                    },
                };
            }

            /// <summary>
            /// A subtle glow loop that says "there is something to buy".
            /// </summary>
            public bool Pulsing
            {
                get => pulsing;
                set
                {
                    if (pulsing == value)
                        return;

                    pulsing = value;
                    glow.ClearTransforms();

                    if (value)
                    {
                        this.FadeEdgeEffectTo(colour.Opacity(0.5f), 500, Easing.OutQuint);
                        glow.Loop(g => g.FadeTo(0.22f, 800, Easing.InOutSine).Then().FadeTo(0.06f, 800, Easing.InOutSine));
                    }
                    else
                    {
                        this.FadeEdgeEffectTo(colour.Opacity(0), 500, Easing.OutQuint);
                        glow.FadeOut(400, Easing.OutQuint);
                    }
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                content.ScaleTo(1.04f, 200, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                content.ScaleTo(1, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}

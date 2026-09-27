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
            InternalChild = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 24),
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
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(20),
                        Children = new Drawable[]
                        {
                            clickerTile = new GameTile("osu! Clicker",
                                ClickerStrings.Text("Click the circle, collect PP, buy taiko drums and mappers. With prestige tree, medals and leaderboards.",
                                    "Klick den Kreis, sammle PP, kauf dir Taiko-Trommeln und Mapper. Mit Prestige-Baum, Medaillen und Ranglisten."),
                                FontAwesome.Solid.MousePointer, colours.Pink, () => this.Push(new OsuClickerScreen())),
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

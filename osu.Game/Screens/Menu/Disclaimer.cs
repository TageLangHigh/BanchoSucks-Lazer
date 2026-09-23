// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    public partial class Disclaimer : StartupScreen
    {
        private SpriteIcon icon;
        private Color4 iconColour;
        private LinkFlowContainer textFlow;

        private const float icon_y = -85;
        private const float icon_size = 30;

        private readonly OsuScreen nextScreen;

        private readonly Bindable<APIUser> currentUser = new Bindable<APIUser>();
        private FillFlowContainer fill;

        public Disclaimer(OsuScreen nextScreen = null)
        {
            this.nextScreen = nextScreen;
            ValidForResume = false;
        }

        [Resolved]
        private IAPIProvider api { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                icon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = OsuIcon.Logo,
                    Size = new Vector2(icon_size),
                    Y = icon_y,
                },
                fill = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Y = icon_y + 20,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.TopCentre,
                    Children = new Drawable[]
                    {
                        textFlow = new LinkFlowContainer
                        {
                            Width = 680,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Spacing = new Vector2(0, 2),
                        },
                    }
                },
            };

            textFlow.AddText(DisclaimerStrings.TitlePart, t => t.Font = t.Font.With(Typeface.MapleMono, 30, FontWeight.Regular));

            textFlow.AddText(@"g0v0!", t =>
            {
                t.Font = t.Font.With(Typeface.MapleMono, 30, FontWeight.Regular);
                t.Colour = colours.G0V0ThemeColour;
            });

            static void formatRegular(SpriteText t) => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Regular);
            static void formatBold(SpriteText t) => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold);

            textFlow.NewParagraph();

            textFlow.AddText(DisclaimerStrings.BasedOnParagraph, formatRegular);
            textFlow.AddText(@"osu!lazer", t =>
            {
                t.Font = t.Font.With(Typeface.MapleMono, 20, FontWeight.Bold);
                t.Colour = colours.Pink;
            });
            textFlow.AddText(DisclaimerStrings.OriginalCodeParagraph, formatRegular);

            textFlow.NewParagraph();
            textFlow.NewParagraph();

            textFlow.NewParagraph();

            textFlow.AddText(DisclaimerStrings.CommunityProjectParagraph, formatBold);

            textFlow.NewParagraph();

            textFlow.AddText(DisclaimerStrings.ReportGitHubParagraph, formatRegular);
            textFlow.NewLine();
            textFlow.AddText(@"https://github.com/GooGuTeam/g0v0", t =>
            {
                t.Font = t.Font.With(Typeface.MapleMono, 20, FontWeight.Bold);
                t.Colour = colours.Blue;
            });
            iconColour = colours.Yellow;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (nextScreen != null)
                LoadComponentAsync(nextScreen);

            ((IBindable<APIUser>)currentUser).BindTo(api.LocalUser);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);

            // Once this screen has finished being displayed, we don't want to unnecessarily handle user change events.
            currentUser.UnbindAll();
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            icon.RotateTo(10);
            icon.FadeOut();
            icon.ScaleTo(0.5f);

            icon.Delay(500).FadeIn(500).ScaleTo(1, 500, Easing.OutQuint);

            using (BeginDelayedSequence(2000))
            {
                icon.FadeColour(iconColour, 200, Easing.OutQuint);
                icon.MoveToY(icon_y * 1.3f, 500, Easing.OutCirc)
                    .RotateTo(-360, 520, Easing.OutQuint)
                    .Then()
                    .MoveToY(icon_y, 160, Easing.InQuart)
                    .FadeColour(Color4.White, 160);

                using (BeginDelayedSequence(520 + 160))
                {
                    fill.MoveToOffset(new Vector2(0, 15), 160, Easing.OutQuart);
                }
            }

            double delay = 500;
            foreach (var c in textFlow.Children)
                c.FadeTo(0.001f).Delay(delay += 15).FadeIn(500);

            this
                .FadeInFromZero(500)
                .Then(4000)
                .FadeOut(250)
                .ScaleTo(0.9f, 250, Easing.InQuint)
                .Finally(_ =>
                {
                    if (nextScreen != null)
                        this.Push(nextScreen);
                });
        }
    }
}

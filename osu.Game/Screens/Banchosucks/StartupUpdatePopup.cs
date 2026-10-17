// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: the box the loader shows while a newer client version is downloaded at startup, like the updater of
    /// osu!stable: title, "updating to version ...", a progress bar with the percentage. English and German by client language
    /// (the clicker's text helper, which is the bilingual helper of this fork).
    /// </summary>
    public partial class StartupUpdatePopup : CompositeDrawable
    {
        private readonly string version;

        private Box fill = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText percentText = null!;

        private float progress;

        /// <summary>
        /// Download progress from 0 to 1; the bar follows it smoothly.
        /// </summary>
        public float Progress
        {
            get => progress;
            set => progress = Math.Clamp(value, 0, 1);
        }

        public StartupUpdatePopup(string? version)
        {
            this.version = version ?? "?";
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.6f),
                },
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 480,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 12,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = OsuColour.Gray(0.12f),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(24),
                            Spacing = new Vector2(0, 10),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10, 0),
                                    Children = new Drawable[]
                                    {
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Icon = FontAwesome.Solid.Download,
                                            Size = new Vector2(22),
                                            Colour = colours.Pink,
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = "BanchoSucks Lazer",
                                            Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                                        },
                                    },
                                },
                                statusText = new OsuSpriteText
                                {
                                    Text = ClickerStrings.Text("Updating to version {0} ...", "Update auf Version {0} wird installiert ...", version),
                                    Font = OsuFont.GetFont(size: 18),
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 10,
                                    Masking = true,
                                    CornerRadius = 5,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = OsuColour.Gray(0.3f),
                                        },
                                        fill = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Width = 0,
                                            Colour = colours.Pink,
                                        },
                                    },
                                },
                                percentText = new OsuSpriteText
                                {
                                    Text = "0 %",
                                    Font = OsuFont.GetFont(size: 14),
                                    Colour = colours.Gray9,
                                },
                                new OsuSpriteText
                                {
                                    Text = ClickerStrings.Text("The game restarts by itself when the download is done.", "Das Spiel startet nach dem Download von selbst neu."),
                                    Font = OsuFont.GetFont(size: 13),
                                    Colour = colours.Gray8,
                                },
                            },
                        },
                    },
                },
            };

            this.FadeInFromZero(200);
        }

        protected override void Update()
        {
            base.Update();

            fill.Width = (float)Interpolation.Lerp(fill.Width, progress, Math.Clamp(Time.Elapsed / 120, 0, 1));
            percentText.Text = $"{(int)Math.Round(progress * 100)} %";
        }

        /// <summary>
        /// The download is complete; the game is about to exit into the updater.
        /// </summary>
        public void ShowRestarting()
        {
            progress = 1;
            fill.Width = 1;
            statusText.Text = ClickerStrings.Text("Version {0} downloaded, restarting ...", "Version {0} geladen, Neustart ...", version);
        }
    }
}

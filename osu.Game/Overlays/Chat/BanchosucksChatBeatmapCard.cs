// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Overlays.Chat
{
    /// <summary>
    /// Banchosucks: the compact beatmap card under a chat message with a beatmap link. Built from the parts of
    /// the beatmap listing's cards (<see cref="BeatmapCard"/>): cover thumbnail with preview button, download /
    /// go-to / favourite buttons, status pill and download progress. A click opens the beatmap overlay.
    /// </summary>
    public partial class BanchosucksChatBeatmapCard : BeatmapCard
    {
        public const float CARD_WIDTH = 400;
        public const float CARD_HEIGHT = 64;

        protected override Drawable IdleContent => idleBottomContent;
        protected override Drawable DownloadInProgressContent => downloadProgressBar;

        /// <summary>
        /// The texts shown, worked out from the set and the link (also read by tests).
        /// </summary>
        public readonly BanchosucksChatBeatmapPreviewInfo Info;

        private readonly BanchosucksChatBeatmapLink link;

        [Cached]
        private readonly BeatmapCardContent content;

        private BeatmapCardThumbnail thumbnail = null!;
        private CollapsibleButtonContainer buttonContainer = null!;
        private FillFlowContainer idleBottomContent = null!;
        private BeatmapCardDownloadProgressBar downloadProgressBar = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapSetOverlay? beatmapSetOverlay { get; set; }

        public BanchosucksChatBeatmapCard(APIBeatmapSet beatmapSet, BanchosucksChatBeatmapLink link)
            : base(beatmapSet, false)
        {
            this.link = link;
            Info = BanchosucksChatBeatmapPreviewInfo.From(beatmapSet, link);

            content = new BeatmapCardContent(CARD_HEIGHT);

            // a link to one difficulty opens the overlay on that difficulty, like clicking the link itself
            if (link.IsSet || Info.DifficultyName == null)
                Action = DefaultAction;
            else
                Action = () => beatmapSetOverlay?.FetchAndShowBeatmap(link.Id);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Width = CARD_WIDTH;
            Height = CARD_HEIGHT;

            var bottomRow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new BeatmapSetOnlineStatusPill
                    {
                        Status = BeatmapSet.Status,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        TextSize = 11f,
                    },
                }
            };

            if (Info.Stars != null)
            {
                bottomRow.AddRange(new Drawable[]
                {
                    new SpriteIcon
                    {
                        Icon = FontAwesome.Solid.Star,
                        Size = new Vector2(9),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Colour = colours.Yellow,
                        Margin = new MarginPadding { Right = -3 },
                    },
                    new OsuSpriteText
                    {
                        Text = Info.StarText,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Colour = colours.Yellow,
                    },
                });
            }

            bottomRow.Add(new OsuSpriteText
            {
                Text = ClickerStrings.Text("mapped by {0}", "gemappt von {0}", Info.Mapper),
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Colour = colourProvider.Content2,
            });

            Child = content.With(c =>
            {
                c.MainContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        thumbnail = new BeatmapCardThumbnail(BeatmapSet, BeatmapSet)
                        {
                            Name = @"Left (cover) area",
                            Size = new Vector2(CARD_HEIGHT),
                            Padding = new MarginPadding { Right = CORNER_RADIUS },
                        },
                        buttonContainer = new CollapsibleButtonContainer(BeatmapSet)
                        {
                            X = CARD_HEIGHT - CORNER_RADIUS,
                            Width = CARD_WIDTH - CARD_HEIGHT + CORNER_RADIUS,
                            FavouriteState = { BindTarget = FavouriteState },
                            ButtonsCollapsedWidth = CORNER_RADIUS,
                            ButtonsExpandedWidth = 24,
                            // the download / go-to button is always there, not only while hovering
                            ShowDetails = { Value = true },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new TruncatingSpriteText
                                        {
                                            Text = new RomanisableString(Info.TitleUnicode, Info.Title),
                                            Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                                            RelativeSizeAxes = Axes.X,
                                        },
                                        new TruncatingSpriteText
                                        {
                                            Text = createSecondLine(),
                                            Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                                            RelativeSizeAxes = Axes.X,
                                        },
                                    }
                                },
                                new Container
                                {
                                    Name = @"Bottom content",
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    Children = new Drawable[]
                                    {
                                        idleBottomContent = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            AlwaysPresent = true,
                                            Child = bottomRow,
                                        },
                                        downloadProgressBar = new BeatmapCardDownloadProgressBar
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 5,
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            State = { BindTarget = DownloadTracker.State },
                                            Progress = { BindTarget = DownloadTracker.Progress }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                // the card never expands (no difficulty list); the listing's cards still need the container
                c.ExpandedContent = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                };
                c.Expanded.BindTarget = Expanded;
            });
        }

        /// <summary>
        /// "Artist" for a set, "Artist · [Difficulty]" for a link to one difficulty.
        /// </summary>
        private LocalisableString createSecondLine()
        {
            LocalisableString artist = new RomanisableString(Info.ArtistUnicode, Info.Artist);

            if (Info.DifficultyName == null)
                return artist;

            return LocalisableString.Format("{0} · [{1}]", artist, Info.DifficultyName);
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            thumbnail.Dimmed.Value = IsHovered;
            buttonContainer.ShowDetails.Value = true;
        }
    }
}

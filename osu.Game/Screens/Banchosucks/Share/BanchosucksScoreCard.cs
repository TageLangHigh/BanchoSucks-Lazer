// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: the score card, a 1200 x 630 picture of one score (map, mods, rank, accuracy, combo, hit counts,
    /// pp, player, date). Built from <see cref="BanchosucksScoreCardData"/>; turned into an image by
    /// <see cref="BanchosucksCardExporter"/>.
    /// </summary>
    public partial class BanchosucksScoreCard : CompositeDrawable
    {
        public readonly BanchosucksScoreCardData Data;

        private readonly Func<Texture?>? loadBackground;

        private const float content_width = BanchosucksCardExporter.CARD_WIDTH - 2 * BanchosucksCardParts.MARGIN;
        private const float rank_width = 230;

        /// <param name="data">What the card says.</param>
        /// <param name="loadBackground">
        /// Gets the picture behind the card (the beatmap's background). Called while the card loads, off the update
        /// thread. May return null, and the card then has a plain backdrop.
        /// </param>
        public BanchosucksScoreCard(BanchosucksScoreCardData data, Func<Texture?>? loadBackground = null)
        {
            Data = data;
            this.loadBackground = loadBackground;

            Size = new Vector2(BanchosucksCardExporter.CARD_WIDTH, BanchosucksCardExporter.CARD_HEIGHT);
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Texture? background = null;

            try
            {
                background = loadBackground?.Invoke();
            }
            catch (Exception e)
            {
                // the picture is decoration; a broken image file must not cost the player the card
                Logger.Log($"Banchosucks: the score card goes without background ({e.Message})");
            }

            float textWidth = content_width - rank_width - 24;

            var subtitle = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(14, 0),
            };

            if (!string.IsNullOrEmpty(Data.StarText))
            {
                subtitle.Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Star,
                            Size = new Vector2(22),
                            Colour = colours.Yellow,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = Data.StarText,
                            Font = BanchosucksCardParts.Font(28, FontWeight.Bold),
                            Colour = colours.Yellow,
                            Shadow = false,
                        },
                    }
                });
            }

            if (!string.IsNullOrEmpty(Data.Mapper))
            {
                subtitle.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = $"{Data.Labels.MappedBy} {Data.Mapper}",
                    Font = BanchosucksCardParts.Font(26, FontWeight.SemiBold),
                    Colour = BanchosucksCardParts.TEXT_DIM,
                    Shadow = false,
                });
            }

            var stats = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(44, 0),
                Children = new[]
                {
                    BanchosucksCardParts.CreateStat(Data.Labels.Accuracy, Data.AccuracyText),
                    BanchosucksCardParts.CreateStat(Data.Labels.Combo, Data.ComboText, string.IsNullOrEmpty(Data.MaxComboText) ? (LocalisableString?)null : Data.MaxComboText),
                }
            };

            if (Data.PpText != null)
                stats.Add(BanchosucksCardParts.CreateStat("PP", Data.PpText, valueColour: BanchosucksCardParts.ACCENT));

            stats.Add(BanchosucksCardParts.CreateStat(Data.Labels.Score, Data.ScoreText));

            InternalChildren = new[]
            {
                BanchosucksCardParts.CreateBackground(background),
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(BanchosucksCardParts.MARGIN),
                    Children = new Drawable[]
                    {
                        // top left: the map
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                            Children = new Drawable[]
                            {
                                BanchosucksCardParts.CreateLine(new RomanisableString(Data.TitleUnicode, Data.Title), 50, FontWeight.Bold, textWidth),
                                BanchosucksCardParts.CreateLine(new RomanisableString(Data.ArtistUnicode, Data.Artist), 34, FontWeight.SemiBold, textWidth, BanchosucksCardParts.TEXT_DIM),
                                BanchosucksCardParts.CreateLine($"[{Data.Difficulty}]", 30, FontWeight.Bold, textWidth, colours.Yellow),
                                subtitle,
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8, 0),
                                    Margin = new MarginPadding { Top = 8 },
                                    ChildrenEnumerable = Data.Mods.Select(m => BanchosucksCardParts.CreatePill(m.Text, colours.ForModType(m.Type), 26)),
                                },
                            }
                        },
                        // top right: the rank
                        new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Size = new Vector2(rank_width, 150),
                            Masking = true,
                            CornerRadius = 32,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = OsuColour.ForRank(Data.Rank),
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = DrawableRank.GetRankLetter(Data.Rank),
                                    Font = BanchosucksCardParts.Font(110, FontWeight.Bold),
                                    Colour = DrawableRank.GetRankLetterColour(Data.Rank),
                                    Shadow = true,
                                    ShadowColour = Color4.Black.Opacity(0.3f),
                                },
                            }
                        },
                        // lower half: the numbers, the hit counts and the footer, from the bottom up
                        new FillFlowContainer
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 22),
                            Children = new Drawable[]
                            {
                                stats,
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10, 0),
                                    ChildrenEnumerable = Data.Hits.Select(h => createHit(h, colours)),
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(16, 0),
                                    Margin = new MarginPadding { Top = 6 },
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Text = Data.Labels.PlayedBy,
                                            Font = BanchosucksCardParts.Font(24, FontWeight.SemiBold),
                                            Colour = BanchosucksCardParts.TEXT_DIM,
                                            Shadow = false,
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Text = Data.Player,
                                            Font = BanchosucksCardParts.Font(34, FontWeight.Bold),
                                            Shadow = false,
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Text = $"{Data.DateText}  ·  {Data.RulesetName}",
                                            Font = BanchosucksCardParts.Font(24, FontWeight.SemiBold),
                                            Colour = BanchosucksCardParts.TEXT_DIM,
                                            Shadow = false,
                                        },
                                    }
                                },
                            }
                        },
                        // bottom right: where the score was set
                        BanchosucksCardParts.CreateMark().With(m =>
                        {
                            m.Anchor = Anchor.BottomRight;
                            m.Origin = Anchor.BottomRight;
                        }),
                    }
                },
            };
        }

        private static Drawable createHit(BanchosucksScoreCardData.HitEntry hit, OsuColour colours) => new Container
        {
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 10,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.45f),
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Margin = new MarginPadding { Horizontal = 14, Vertical = 6 },
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = hit.Name.ToUpper(),
                            Font = BanchosucksCardParts.Font(20, FontWeight.Bold),
                            Colour = colours.ForHitResult(hit.Result),
                            Shadow = false,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = hit.Count,
                            Font = BanchosucksCardParts.Font(28, FontWeight.Bold),
                            Shadow = false,
                        },
                    }
                },
            }
        };
    }
}

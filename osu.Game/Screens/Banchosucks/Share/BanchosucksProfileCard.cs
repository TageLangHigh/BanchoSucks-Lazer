// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: the profile card, a 1200 x 630 picture of a player (avatar, name, country, ranks, pp, accuracy,
    /// play count, level, best play). Built from <see cref="BanchosucksProfileCardData"/>; turned into an image by
    /// <see cref="BanchosucksCardExporter"/>.
    /// </summary>
    public partial class BanchosucksProfileCard : CompositeDrawable
    {
        public readonly BanchosucksProfileCardData Data;

        private const float content_width = BanchosucksCardExporter.CARD_WIDTH - 2 * BanchosucksCardParts.MARGIN;
        private const float avatar_size = 200;

        public BanchosucksProfileCard(BanchosucksProfileCardData data)
        {
            Data = data;

            Size = new Vector2(BanchosucksCardExporter.CARD_WIDTH, BanchosucksCardExporter.CARD_HEIGHT);
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, LargeTextureStore textures, OnlineAssetCachingStore onlineTextures)
        {
            Texture? avatar = null;

            try
            {
                // the same store and address the profile overlay showed the avatar from, so it is in memory already
                if (!string.IsNullOrEmpty(Data.AvatarUrl))
                    avatar = onlineTextures.Get(Data.AvatarUrl);
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks: the profile card goes without avatar ({e.Message})");
            }

            avatar ??= textures.Get(@"Online/avatar-guest");

            float nameWidth = content_width - avatar_size - 32;

            var countryRow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(14, 0),
            };

            if (!string.IsNullOrEmpty(Data.CountryName))
            {
                countryRow.AddRange(new Drawable[]
                {
                    new DrawableFlag(Data.CountryCode)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(54, 38),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = Data.CountryName,
                        Font = BanchosucksCardParts.Font(32, FontWeight.SemiBold),
                        Colour = BanchosucksCardParts.TEXT_DIM,
                        Shadow = false,
                    },
                });
            }

            var bestPlay = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
            };

            if (Data.BestTitle != null)
            {
                bool hasPp = !string.IsNullOrEmpty(Data.BestPpText);

                bestPlay.AddRange(new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = Data.Labels.BestPlay,
                        Font = BanchosucksCardParts.Font(22, FontWeight.SemiBold),
                        Colour = BanchosucksCardParts.TEXT_DIM,
                        Shadow = false,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(20, 0),
                        Children = new Drawable[]
                        {
                            BanchosucksCardParts.CreateLine(Data.BestTitle, 32, FontWeight.Bold, content_width - (hasPp ? 200 : 0)).With(l =>
                            {
                                l.Anchor = Anchor.BottomLeft;
                                l.Origin = Anchor.BottomLeft;
                            }),
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Text = Data.BestPpText,
                                Font = BanchosucksCardParts.Font(36, FontWeight.Bold),
                                Colour = BanchosucksCardParts.ACCENT,
                                Shadow = false,
                            },
                        }
                    },
                });
            }

            InternalChildren = new[]
            {
                BanchosucksCardParts.CreateBackground(null),
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(BanchosucksCardParts.MARGIN),
                    Children = new Drawable[]
                    {
                        // top: who
                        new Container
                        {
                            Size = new Vector2(avatar_size),
                            Masking = true,
                            CornerRadius = 36,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.4f),
                                },
                                new Sprite
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    FillMode = FillMode.Fill,
                                    Texture = avatar,
                                },
                            }
                        },
                        new FillFlowContainer
                        {
                            X = avatar_size + 32,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 10),
                            Margin = new MarginPadding { Top = 14 },
                            Children = new Drawable[]
                            {
                                BanchosucksCardParts.CreateLine(Data.Username, 72, FontWeight.Bold, nameWidth),
                                countryRow,
                                BanchosucksCardParts.CreatePill(Data.RulesetName, colours.Yellow, 24),
                            }
                        },
                        // lower half: the numbers, the best play and the footer, from the bottom up
                        new FillFlowContainer
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 26),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(38, 0),
                                    Children = new[]
                                    {
                                        BanchosucksCardParts.CreateStat(Data.Labels.GlobalRank, Data.GlobalRankText, valueSize: 42),
                                        BanchosucksCardParts.CreateStat(Data.Labels.CountryRank, Data.CountryRankText, valueSize: 42),
                                        BanchosucksCardParts.CreateStat("PP", Data.PpText, valueColour: BanchosucksCardParts.ACCENT, valueSize: 42),
                                        BanchosucksCardParts.CreateStat(Data.Labels.Accuracy, Data.AccuracyText, valueSize: 42),
                                        BanchosucksCardParts.CreateStat(Data.Labels.PlayCount, Data.PlayCountText, valueSize: 42),
                                        BanchosucksCardParts.CreateStat(Data.Labels.Level, Data.LevelText,
                                            string.IsNullOrEmpty(Data.LevelProgressText) ? (LocalisableString?)null : Data.LevelProgressText, valueSize: 42),
                                    }
                                },
                                bestPlay,
                                new OsuSpriteText
                                {
                                    Text = Data.DateText,
                                    Font = BanchosucksCardParts.Font(24, FontWeight.SemiBold),
                                    Colour = BanchosucksCardParts.TEXT_DIM,
                                    Shadow = false,
                                    Margin = new MarginPadding { Top = 2 },
                                },
                            }
                        },
                        // bottom right: where the player plays
                        BanchosucksCardParts.CreateMark().With(m =>
                        {
                            m.Anchor = Anchor.BottomRight;
                            m.Origin = Anchor.BottomRight;
                        }),
                    }
                },
            };
        }
    }
}

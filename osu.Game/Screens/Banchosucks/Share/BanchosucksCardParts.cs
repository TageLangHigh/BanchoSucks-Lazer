// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: the pieces both share cards are built from. Everything here is static on purpose: a card is
    /// photographed a few frames after it was created, so nothing may fade or slide in.
    /// </summary>
    public static class BanchosucksCardParts
    {
        /// <summary>
        /// The website's accent colour (logo ring).
        /// </summary>
        public static readonly Color4 ACCENT = Color4Extensions.FromHex("ff4964");

        public static readonly Color4 BACKGROUND_TOP = Color4Extensions.FromHex("2a1a24");
        public static readonly Color4 BACKGROUND_BOTTOM = Color4Extensions.FromHex("120c14");
        public static readonly Color4 TEXT_DIM = Color4Extensions.FromHex("c9bfc6");

        /// <summary>
        /// Distance of the content from the card's edges.
        /// </summary>
        public const float MARGIN = 48;

        public const string WEBSITE = "banchosucks.cc";

        /// <summary>
        /// The cards' typeface: Inter, the proportional one of the two the client ships (the chat uses it too).
        /// Long titles fit far better than in the client's monospaced default. Inter comes in Light, Regular,
        /// SemiBold and Bold only.
        /// </summary>
        public static FontUsage Font(float size, FontWeight weight = FontWeight.Bold) => OsuFont.GetFont(Typeface.Inter, size, weight);

        /// <summary>
        /// The card's backdrop: a dark gradient, with a picture under it when there is one (darkened so text stays readable).
        /// </summary>
        public static Drawable CreateBackground(Texture? picture)
        {
            var background = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(BACKGROUND_TOP, BACKGROUND_BOTTOM),
                    },
                }
            };

            if (picture != null)
            {
                background.AddRange(new Drawable[]
                {
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Texture = picture,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.55f), Color4.Black.Opacity(0.85f)),
                    },
                });
            }

            // a thin accent line along the top
            background.Add(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 8,
                Colour = ACCENT,
            });

            return background;
        }

        /// <summary>
        /// The small "banchosucks.cc" mark with the logo's ring and dot.
        /// </summary>
        public static Drawable CreateMark() => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(12, 0),
            Children = new Drawable[]
            {
                new Container
                {
                    Size = new Vector2(34),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        new Circle { RelativeSizeAxes = Axes.Both, Colour = ACCENT },
                        new Circle { Size = new Vector2(24), Anchor = Anchor.Centre, Origin = Anchor.Centre, Colour = BACKGROUND_BOTTOM },
                        new Circle { Size = new Vector2(10), Anchor = Anchor.Centre, Origin = Anchor.Centre, Colour = ACCENT },
                    }
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = WEBSITE,
                    Font = Font(28, FontWeight.Bold),
                    Colour = Color4.White,
                    Shadow = false,
                },
            }
        };

        /// <summary>
        /// A value with a small label above it.
        /// </summary>
        public static Drawable CreateStat(string label, LocalisableString value, LocalisableString? suffix = null, Color4? valueColour = null, float valueSize = 46)
        {
            var valueRow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Text = value,
                        Font = Font(valueSize, FontWeight.Bold),
                        Colour = valueColour ?? Color4.White,
                        Shadow = false,
                    },
                }
            };

            if (suffix != null)
            {
                valueRow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = suffix.Value,
                    Font = Font(valueSize * 0.5f, FontWeight.SemiBold),
                    Colour = TEXT_DIM,
                    Shadow = false,
                    Margin = new MarginPadding { Bottom = valueSize * 0.1f },
                });
            }

            return new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = label,
                        Font = Font(22, FontWeight.SemiBold),
                        Colour = TEXT_DIM,
                        Shadow = false,
                    },
                    valueRow,
                }
            };
        }

        /// <summary>
        /// A rounded label: coloured background, dark text.
        /// </summary>
        public static Drawable CreatePill(LocalisableString text, Color4 colour, float textSize = 24) => new Container
        {
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = textSize * 0.4f,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                },
                new OsuSpriteText
                {
                    Text = text,
                    Font = Font(textSize, FontWeight.Bold),
                    Colour = Color4.Black.Opacity(0.85f),
                    Shadow = false,
                    Margin = new MarginPadding { Horizontal = textSize * 0.5f, Vertical = textSize * 0.15f },
                },
            }
        };

        /// <summary>
        /// One line of text that is cut off with an ellipsis at <paramref name="width"/>.
        /// </summary>
        public static TruncatingSpriteText CreateLine(LocalisableString text, float size, FontWeight weight, float width, Color4? colour = null) => new TruncatingSpriteText
        {
            Text = text,
            Font = Font(size, weight),
            Colour = colour ?? Color4.White,
            Width = width,
            Shadow = false,
            ShowTooltip = false,
        };
    }
}

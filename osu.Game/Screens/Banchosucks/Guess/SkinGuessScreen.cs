// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Guess
{
    /// <summary>
    /// Banchosucks: the skin guesser. Library mode shows cursor, hit circle and judgements of one of the player's installed
    /// skins; popular mode shows the element images of a skin from the server's pool (see the plugin's skins/README.md).
    /// </summary>
    public partial class SkinGuessScreen : GuessScreen
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override string GameKey => "skin";
        protected override LocalisableString GameTitle => ClickerStrings.Text("Skin Guesser", "Skin-Guesser");

        protected override LocalisableString GameDescription => ClickerStrings.Text(
            "Cursor, hit circle and judgements are shown, four skin names are offered. Your own skins, or the popular ones from the server's pool.",
            "Cursor, Hitcircle und Trefferbilder werden gezeigt, vier Skin-Namen stehen zur Wahl. Deine eigenen Skins, oder die beliebten aus dem Pool des Servers.");

        protected override LocalisableString PopularDescription => ClickerStrings.Text("Well-known skins from the server, until you miss", "Bekannte Skins vom Server, bis zum ersten Fehler");
        protected override LocalisableString TooFewItemsText => ClickerStrings.Text("You need at least four imported skins.", "Du brauchst mindestens vier importierte Skins.");
        protected override IconUsage GameIcon => FontAwesome.Solid.PaintBrush;
        protected override Color4 Accent => Colours.Blue;

        protected override List<GuessItem> LoadItems()
        {
            return realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending && !s.Protected).AsEnumerable().Detach().ToList())
                        .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                        .Select(s => new GuessItem(s.ID.ToString(), s.Name, s))
                        .ToList();
        }

        protected override GuessPrompt CreatePrompt(GuessItem item) => new LocalSkinPrompt(skins.GetSkin((SkinInfo)item.Payload), item.Label, Accent);

        protected override GuessPrompt CreatePopularPrompt(GuessPoolItem item) => new OnlineSkinPrompt(item, Accent);

        /// <summary>Element cells in a row, the name once answered; subclasses fill the cells.</summary>
        private abstract partial class SkinPrompt : GuessPrompt
        {
            protected readonly Color4 Accent;
            private readonly string name;
            private OsuSpriteText nameText = null!;
            private OsuSpriteText statusText = null!;

            protected SkinPrompt(string name, Color4 accent)
            {
                this.name = name;
                Accent = accent;
            }

            /// <summary>The element pictures, up to five; may be empty.</summary>
            protected abstract IEnumerable<Drawable> CreateCells();

            [BackgroundDependencyLoader]
            private void load()
            {
                var cells = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(18, 0),
                    ChildrenEnumerable = CreateCells().Take(5),
                };

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("1a1d2a") },
                    cells,
                    statusText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 18,
                        Text = cells.Children.Count > 0
                            ? ClickerStrings.Text("Whose elements are these?", "Wessen Elemente sind das?")
                            : ClickerStrings.Text("No pictures for this skin. Guess anyway!", "Keine Bilder für diesen Skin. Rate trotzdem!"),
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                        Colour = Color4.White.Opacity(0.8f),
                    },
                    nameText = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -18,
                        Text = name,
                        Font = OsuFont.GetFont(size: 26, weight: FontWeight.Bold),
                        Shadow = true,
                        Alpha = 0,
                    },
                };
            }

            protected static Drawable Cell(IEnumerable<Drawable?> layers) => new Container
            {
                Size = new Vector2(120),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[] { new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.06f) } }
                    .Concat(layers.Where(l => l != null).Select(l => l!)).ToArray(),
            };

            protected static Drawable? Picture(Texture? texture, float scale = 1, Color4? colour = null)
            {
                if (texture == null) return null;

                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(12),
                    Child = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fit,
                        Scale = new Vector2(scale),
                        Texture = texture,
                        Colour = colour ?? Color4.White,
                    },
                };
            }

            public override void Reveal(bool correct)
            {
                statusText.FadeOut(200);
                nameText.FadeIn(300);
            }
        }

        /// <summary>An installed skin: its own textures, composed like the game draws them.</summary>
        private partial class LocalSkinPrompt : SkinPrompt
        {
            private static readonly string[] judgement_names = { "hit300", "hit100", "hit50", "hit0" };

            private readonly Skin skin;
            private readonly ISkin lookup;

            public LocalSkinPrompt(Skin skin, string name, Color4 accent)
                : base(name, accent)
            {
                this.skin = skin;
                lookup = skin;
            }

            protected override IEnumerable<Drawable> CreateCells()
            {
                var cursor = lookup.GetTexture("cursor");
                if (cursor != null) yield return Cell(new[] { Picture(cursor), Picture(lookup.GetTexture("cursormiddle")) });

                var circle = lookup.GetTexture("hitcircle");

                if (circle != null)
                {
                    string prefix = lookup.GetConfig<SkinConfiguration.LegacySetting, string>(SkinConfiguration.LegacySetting.HitCirclePrefix)?.Value ?? "default";
                    var combo = lookup.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;
                    var tint = combo != null && combo.Count > 0 ? combo[0] : Accent;
                    yield return Cell(new[] { Picture(circle, 1, tint), Picture(lookup.GetTexture("hitcircleoverlay")), Picture(lookup.GetTexture($"{prefix}-1"), 0.8f) });
                }

                foreach (string judgement in judgement_names)
                {
                    var texture = lookup.GetTexture(judgement) ?? lookup.GetTexture($"{judgement}-0");
                    if (texture != null) yield return Cell(new[] { Picture(texture) });
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                skin.Dispose();
            }
        }

        /// <summary>A skin from the server's pool: its element pictures, fetched by url while the prompt loads.</summary>
        private partial class OnlineSkinPrompt : SkinPrompt
        {
            [Resolved]
            private LargeTextureStore textures { get; set; } = null!;

            private readonly GuessPoolItem item;

            public OnlineSkinPrompt(GuessPoolItem item, Color4 accent)
                : base(item.Name, accent)
            {
                this.item = item;
            }

            protected override IEnumerable<Drawable> CreateCells()
            {
                foreach (string url in item.Images.Take(5))
                {
                    Texture? texture;

                    try
                    {
                        texture = textures.Get(url);
                    }
                    catch (Exception)
                    {
                        texture = null;
                    }

                    if (texture != null) yield return Cell(new[] { Picture(texture) });
                }
            }
        }
    }
}

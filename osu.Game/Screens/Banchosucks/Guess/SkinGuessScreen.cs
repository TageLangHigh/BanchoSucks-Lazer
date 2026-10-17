// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

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
    /// Banchosucks: the skin guesser. Cursor, hit circle and judgement images of one of the player's installed skins are
    /// shown, four skin names are offered.
    /// </summary>
    public partial class SkinGuessScreen : GuessScreen
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override string Kind => "skin";
        protected override LocalisableString GameTitle => ClickerStrings.Text("Skin Guesser", "Skin-Guesser");

        protected override LocalisableString GameDescription => ClickerStrings.Text(
            "Cursor, hit circle and judgements of one of your installed skins. Which skin is it? Works with the skins you have imported.",
            "Cursor, Hitcircle und Trefferbilder eines deiner installierten Skins. Welcher Skin ist das? Funktioniert mit den Skins, die du importiert hast.");

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

        protected override GuessPrompt CreatePrompt(GuessItem item) => new SkinPrompt(skins.GetSkin((SkinInfo)item.Payload), item.Label, Accent);

        /// <summary>The skin's elements in a row; the name appears once answered.</summary>
        private partial class SkinPrompt : GuessPrompt
        {
            private static readonly string[] judgement_names = { "hit300", "hit100", "hit50", "hit0" };

            private readonly Skin skin;
            private readonly ISkin lookup;
            private readonly string name;
            private readonly Color4 accent;
            private OsuSpriteText nameText = null!;
            private OsuSpriteText statusText = null!;

            public SkinPrompt(Skin skin, string name, Color4 accent)
            {
                this.skin = skin;
                lookup = skin;
                this.name = name;
                this.accent = accent;
            }

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
                };

                var cursor = lookup.GetTexture("cursor");
                if (cursor != null) cells.Add(cell(new[] { sprite(cursor, 1), sprite(lookup.GetTexture("cursormiddle"), 1) }));

                var circle = lookup.GetTexture("hitcircle");

                if (circle != null)
                {
                    string prefix = lookup.GetConfig<SkinConfiguration.LegacySetting, string>(SkinConfiguration.LegacySetting.HitCirclePrefix)?.Value ?? "default";
                    var combo = lookup.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;
                    var tint = combo != null && combo.Count > 0 ? combo[0] : accent;
                    cells.Add(cell(new[] { sprite(circle, 1, tint), sprite(lookup.GetTexture("hitcircleoverlay"), 1), sprite(lookup.GetTexture($"{prefix}-1"), 0.8f) }));
                }

                foreach (string judgement in judgement_names)
                {
                    var texture = lookup.GetTexture(judgement) ?? lookup.GetTexture($"{judgement}-0");
                    if (texture != null) cells.Add(cell(new[] { sprite(texture, 1) }));
                    if (cells.Count >= 5) break;
                }

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("1a1d2a") },
                    cells,
                    statusText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 18,
                        Text = cells.Count > 0
                            ? ClickerStrings.Text("Whose elements are these?", "Wessen Elemente sind das?")
                            : ClickerStrings.Text("This skin has no standard elements. Guess anyway!", "Dieser Skin hat keine Standard-Elemente. Rate trotzdem!"),
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

            private static Drawable cell(IEnumerable<Drawable?> layers) => new Container
            {
                Size = new Vector2(120),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[] { new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.06f) } }
                    .Concat(layers.Where(l => l != null).Select(l => l!)).ToArray(),
            };

            private static Drawable? sprite(Texture? texture, float scale, Color4? colour = null)
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

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                skin.Dispose();
            }
        }
    }
}

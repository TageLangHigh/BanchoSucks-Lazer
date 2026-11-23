// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// A single selectable background preview in the background editor.
    /// </summary>
    public partial class BackgroundOption : ClickableContainer, IHasTooltip
    {
        /// <summary>
        /// The height of the preview. Exposed so that the editor can size the scroll container holding the options.
        /// </summary>
        public const float OPTION_HEIGHT = 128;

        private const float crossfade_duration = 300;

        public BackgroundOptionType Type { get; }

        /// <summary>
        /// The index of the seasonal background this option displays, for <see cref="BackgroundOptionType.SeasonalPinned"/>.
        /// </summary>
        public int Index { get; init; }

        public LocalisableString TooltipText { get; set; }

        private readonly Sprite firstSprite;
        private readonly Sprite secondSprite;
        private readonly Box hoverLayer;
        private readonly Box flashLayer;
        private readonly LoadingSpinner loadingSpinner;

        /// <summary>
        /// The sprite currently showing an image. Textures are always written into the other one,
        /// as a <see cref="Sprite"/> disposes the texture it replaces.
        /// </summary>
        private Sprite visibleSprite;

        /// <summary>
        /// Identifies the image currently displayed, used to avoid restarting a transition for the same image.
        /// </summary>
        private string? displayedKey;

        public BackgroundOption(BackgroundOptionType type, Texture? texture = null, IconUsage? badgeIcon = null)
        {
            Type = type;

            Size = new Vector2(180, OPTION_HEIGHT);
            Masking = true;
            CornerRadius = 6;
            CornerExponent = 2.5f;
            BorderThickness = Selected ? 3 : 0;

            Children = new[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.4f),
                },
                // two sprites are crossfaded whenever the image changes. A texture instance is never
                // shared between them (or handed out twice), because assigning a texture to a sprite
                // disposes the one it replaces.
                firstSprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fill,
                    Texture = texture,
                },
                secondSprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fill,
                    Alpha = 0,
                },
                badgeIcon != null ? createBadge(badgeIcon.Value) : Empty(),
                hoverLayer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Colour = Color4.White.Opacity(0.12f),
                },
                loadingSpinner = new LoadingSpinner
                {
                    Size = new Vector2(24),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                flashLayer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Colour = Color4.White.Opacity(0.25f),
                },
            };

            visibleSprite = firstSprite;
        }

        /// <summary>
        /// A small marker in the bottom right corner denoting where the preview's image comes from.
        /// </summary>
        private static Container createBadge(IconUsage icon) => new Container
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Size = new Vector2(22),
            Margin = new MarginPadding(6),
            Masking = true,
            CornerRadius = 4,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.6f),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(12),
                    Icon = icon,
                },
            },
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            BorderColour = colourProvider.Highlight1;
        }

        /// <summary>
        /// Show a loading indicator, for textures which are fetched asynchronously.
        /// </summary>
        public void BeginAsyncLoad() => loadingSpinner.Show();

        /// <summary>
        /// Stop the loading indicator without setting a texture (i.e. the fetch failed).
        /// </summary>
        public void EndAsyncLoad() => loadingSpinner.Hide();

        /// <summary>
        /// Set (or replace) the displayed texture, fading between the two images.
        /// The option takes ownership of <paramref name="texture"/>.
        /// </summary>
        /// <param name="texture">The texture to display. <c>null</c> clears the preview.</param>
        /// <param name="key">Identifies the image, so that re-showing the current one does not restart a transition.</param>
        public void SetTexture(Texture? texture, string? key = null)
        {
            loadingSpinner.Hide();

            if (texture == null)
            {
                ClearTexture();
                return;
            }

            Sprite inactive = ReferenceEquals(visibleSprite, firstSprite) ? secondSprite : firstSprite;

            // a texture may only ever be owned by a single sprite, as that sprite disposes it once replaced.
            if (ReferenceEquals(visibleSprite.Texture, texture))
                return;

            if (ReferenceEquals(inactive.Texture, texture))
            {
                // the very same instance is held by the sprite which was faded out last time (i.e. a skin caching
                // its textures); bring that sprite back instead of moving the texture across sprites.
                displayedKey = key;
                inactive.FadeIn(crossfade_duration, Easing.OutQuint);
                visibleSprite.FadeOut(crossfade_duration, Easing.OutQuint);
                visibleSprite = inactive;
                return;
            }

            if (key != null && key == displayedKey)
            {
                // the store handed out a (new) reference to an image which is already displayed.
                texture.Dispose();
                return;
            }

            displayedKey = key;

            inactive.Texture = texture;
            inactive.ClearTransforms();
            inactive.Alpha = 0;
            inactive.FadeIn(crossfade_duration, Easing.OutQuint);

            visibleSprite.ClearTransforms();
            visibleSprite.FadeOut(crossfade_duration, Easing.OutQuint);
            visibleSprite = inactive;
        }

        /// <summary>
        /// Remove the displayed image.
        /// </summary>
        public void ClearTexture()
        {
            loadingSpinner.Hide();
            displayedKey = null;

            foreach (var sprite in new[] { firstSprite, secondSprite })
            {
                sprite.ClearTransforms();
                sprite.Alpha = 0;
                sprite.Texture = null;
            }

            visibleSprite = firstSprite;
        }

        public bool Selected
        {
            get;
            set
            {
                field = value;
                BorderThickness = value ? 3 : 0;
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!Enabled.Value)
                return base.OnHover(e);

            hoverLayer.FadeIn(120, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            hoverLayer.FadeOut(120, Easing.OutQuint);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!base.OnClick(e))
                return false;

            flashLayer.FadeOutFromOne(300, Easing.OutQuint);
            return true;
        }
    }

    /// <summary>
    /// The kind of background a <see cref="BackgroundOption"/> offers.
    /// </summary>
    public enum BackgroundOptionType
    {
        /// <summary>
        /// The current skin's menu background.
        /// </summary>
        Skin,

        /// <summary>
        /// The background of the currently selected beatmap.
        /// </summary>
        Beatmap,

        /// <summary>
        /// A seasonal background picked at random.
        /// </summary>
        SeasonalRandom,

        /// <summary>
        /// One specific seasonal background, pinned by index.
        /// </summary>
        SeasonalPinned,
    }
}

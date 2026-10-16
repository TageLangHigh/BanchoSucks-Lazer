// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Cursor;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// skin.ini keys the skin cursor and circle read, named exactly like the keys (legacy skins answer generic lookups by name).
    /// </summary>
    public enum CircleRushSkinSetting
    {
        CursorRotate,
        CursorCentre,
        HitCircleOverlayAboveNumber,
    }

    /// <summary>
    /// Hosts the play area and, while a run is on, replaces the menu cursor with the skin's gameplay cursor,
    /// the same way <c>DrawableRuleset</c> does for gameplay. Without a skin cursor texture the menu cursor stays.
    /// </summary>
    internal partial class CircleRushCursorHost : Container, IProvideCursor
    {
        private readonly Container content;
        private readonly SkinCursorContainer cursor;

        protected override Container<Drawable> Content => content;

        /// <summary>True during countdown and run.</summary>
        public bool ProvideCursor { get; set; }

        /// <summary>Screen-space scale of the cursor: the field's scale times osu!'s 640 / 800 window ratio times the cursor size setting.</summary>
        public float CursorScale
        {
            set => cursor.ActiveCursor.Scale = new Vector2(value);
        }

        public CircleRushCursorHost()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                content = new Container { RelativeSizeAxes = Axes.Both },
                cursor = new SkinCursorContainer(),
            };
        }

        protected override bool OnHover(HoverEvent e) => true; // required for IProvideCursor

        public CursorContainer Cursor => cursor;

        public bool ProvidingUserCursor => ProvideCursor && cursor.HasSkinCursor;

        private partial class SkinCursorContainer : CursorContainer
        {
            private SkinCursor skinCursor = null!;

            public bool HasSkinCursor => skinCursor.HasTexture;

            protected override Drawable CreateCursor() => skinCursor = new SkinCursor();
        }

        /// <summary>The current skin's <c>cursor</c> and <c>cursormiddle</c>, rotating unless skin.ini says otherwise.</summary>
        private partial class SkinCursor : CompositeDrawable
        {
            public bool HasTexture { get; private set; }

            private Sprite? rotating;
            private bool rotate;

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                var texture = skin.GetTexture("cursor");
                HasTexture = texture != null;

                if (texture == null)
                    return;

                bool centre = skin.GetConfig<CircleRushSkinSetting, bool>(CircleRushSkinSetting.CursorCentre)?.Value ?? true;
                rotate = skin.GetConfig<CircleRushSkinSetting, bool>(CircleRushSkinSetting.CursorRotate)?.Value ?? true;
                var origin = centre ? Anchor.Centre : Anchor.TopLeft;

                InternalChildren = new Drawable[]
                {
                    rotating = new Sprite
                    {
                        Texture = texture,
                        Anchor = Anchor.Centre,
                        Origin = origin,
                    },
                    new Sprite
                    {
                        Texture = skin.GetTexture("cursormiddle"),
                        Anchor = Anchor.Centre,
                        Origin = origin,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (rotate)
                    rotating?.Spin(10000, RotationDirection.Clockwise);
            }
        }
    }
}

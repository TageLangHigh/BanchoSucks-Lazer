// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
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
    /// Hosts the play area and, while a run is on, replaces the menu cursor with the skin's gameplay cursor and trail,
    /// the same way <c>DrawableRuleset</c> does for gameplay. Without a skin cursor texture the menu cursor stays.
    /// </summary>
    internal partial class CircleRushCursorHost : Container, IProvideCursor
    {
        private readonly Container content;
        private readonly SkinCursorContainer cursor;
        private int pressedButtons;

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

        /// <summary>An osu! button went down or up: the skin cursor grows while a button is held, like in gameplay.</summary>
        public void SetButtonPressed(bool pressed)
        {
            pressedButtons = Math.Max(0, pressedButtons + (pressed ? 1 : -1));
            cursor.SetPressed(pressedButtons > 0);
        }

        protected override bool OnHover(HoverEvent e) => true; // required for IProvideCursor

        public CursorContainer Cursor => cursor;

        public bool ProvidingUserCursor => ProvideCursor && cursor.HasSkinCursor;

        /// <summary>The skin cursor plus its trail: single sprites every frame for skins with a cursormiddle (stable's "disjoint" trail), a dense line otherwise.</summary>
        private partial class SkinCursorContainer : CursorContainer
        {
            private const double disjoint_separation = 1000 / 60.0;
            private const double disjoint_fade = 150;
            private const double line_fade = 500;

            private readonly Container trailLayer;
            private SkinCursor skinCursor = null!;
            private Texture? trailTexture;
            private bool disjoint;
            private bool hasLastPart;
            private double lastPartTime;
            private Vector2 lastPartPosition;

            public bool HasSkinCursor => skinCursor.HasTexture;

            public SkinCursorContainer()
            {
                Add(trailLayer = new Container { RelativeSizeAxes = Axes.Both, Depth = 1 });
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                trailTexture = skin.GetTexture("cursortrail");
                disjoint = skin.GetTexture("cursormiddle") != null;
            }

            protected override Drawable CreateCursor() => skinCursor = new SkinCursor();

            public void SetPressed(bool pressed) => skinCursor.SetPressed(pressed);

            protected override void PopOut()
            {
                base.PopOut();
                hasLastPart = false;
            }

            protected override void Update()
            {
                base.Update();

                if (trailTexture == null || !HasSkinCursor || State.Value != Visibility.Visible)
                    return;

                var position = ActiveCursor.Position;
                float scale = ActiveCursor.Scale.X;

                if (!hasLastPart)
                {
                    addPart(position, scale, disjoint ? disjoint_fade : line_fade);
                    return;
                }

                float moved = Vector2.Distance(lastPartPosition, position);
                if (moved < 1)
                    return;

                if (disjoint)
                {
                    if (Time.Current - lastPartTime >= disjoint_separation)
                        addPart(position, scale, disjoint_fade);

                    return;
                }

                // a line: parts along the way since the last one, so fast movements do not fall apart into dots
                float spacing = Math.Max(2, trailTexture.DisplayWidth * 0.25f * scale);
                int parts = Math.Min(24, (int)(moved / spacing));

                if (parts == 0)
                    return;

                var from = lastPartPosition;

                for (int i = 1; i <= parts; i++)
                    addPart(Vector2.Lerp(from, position, (float)i / parts), scale, line_fade);
            }

            private void addPart(Vector2 position, float scale, double fade)
            {
                hasLastPart = true;
                lastPartTime = Time.Current;
                lastPartPosition = position;

                var part = new Sprite
                {
                    Texture = trailTexture,
                    Origin = Anchor.Centre,
                    Position = position,
                    Scale = new Vector2(scale),
                };

                trailLayer.Add(part);
                part.FadeOut(fade).Expire();
            }
        }

        /// <summary>The current skin's <c>cursor</c> and <c>cursormiddle</c>, rotating unless skin.ini says otherwise, growing while a button is held.</summary>
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

            public void SetPressed(bool pressed) => rotating?.ScaleTo(pressed ? 1.3f : 1f, 100, Easing.Out);
        }
    }
}

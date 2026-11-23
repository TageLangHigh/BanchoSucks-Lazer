// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Banchosucks: the ghost of the replay comparison. Draws the cursor of a second replay over the playfield
    /// while another replay is being watched: a ring in its own colour with the player's name, a short trail, and
    /// a filled centre while a button is held.
    /// </summary>
    /// <remarks>
    /// Lives in the playfield's coordinate space and on the gameplay clock, like the replay analysis overlay, so it
    /// follows seeking, pausing and playback speed by itself. It is drawing only: the ghost replay is never fed
    /// to an input handler, so it cannot hit objects, play hit sounds, produce judgements or change the score.
    /// </remarks>
    public partial class BanchosucksGhostCursorOverlay : CompositeDrawable
    {
        /// <summary>
        /// Unlike the default cursors (pink, yellow) and the analysis overlay's markers.
        /// </summary>
        public static readonly Color4 GHOST_COLOUR = Color4Extensions.FromHex("3ad6ff");

        /// <summary>
        /// How far back the trail reaches, in milliseconds.
        /// </summary>
        private const double trail_length = 220;

        public readonly BanchosucksGhostTrack Track;

        private readonly string playerName;

        private Container cursor = null!;
        private Circle centre = null!;
        private SmoothPath trail = null!;
        private OsuSpriteText label = null!;

        private readonly List<Vector2> trailPoints = new List<Vector2>();

        /// <summary>
        /// Where the ghost cursor is drawn right now, in playfield coordinates (for tests).
        /// </summary>
        public Vector2 CursorPosition => cursor.Position;

        /// <summary>
        /// Whether the ghost is shown as holding a button right now (for tests).
        /// </summary>
        public bool ShownPressed { get; private set; }

        public BanchosucksGhostCursorOverlay(Score ghost)
        {
            RelativeSizeAxes = Axes.Both;

            Track = new BanchosucksGhostTrack(ghost.Replay.Frames);
            playerName = ghost.ScoreInfo.User.Username;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                trail = new SmoothPath
                {
                    PathRadius = 1.5f,
                    Colour = GHOST_COLOUR,
                    Alpha = 0.45f,
                },
                cursor = new Container
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(22),
                    Children = new Drawable[]
                    {
                        new CircularContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            BorderThickness = 2.5f,
                            BorderColour = GHOST_COLOUR,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = GHOST_COLOUR,
                                Alpha = 0.12f,
                            },
                        },
                        centre = new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Scale = new Vector2(0.45f),
                            Colour = GHOST_COLOUR,
                            Alpha = 0.25f,
                        },
                    }
                },
                label = new OsuSpriteText
                {
                    Origin = Anchor.TopCentre,
                    Text = playerName,
                    Font = OsuFont.GetFont(size: 9, weight: FontWeight.Bold),
                    Colour = GHOST_COLOUR,
                    Alpha = 0.9f,
                },
            };

            // nothing to show until the clock says where the cursor is. an invisible drawable is not updated,
            // so it has to count as present to ever become visible.
            Alpha = 0;
            AlwaysPresent = true;
        }

        protected override void Update()
        {
            base.Update();

            double time = Time.Current;

            if (Track.IsEmpty)
            {
                Alpha = 0;
                return;
            }

            // the ghost is translucent, so what it plays over stays readable
            Alpha = 0.8f;

            Vector2 position = Track.PositionAt(time);

            cursor.Position = position;
            label.Position = position + new Vector2(0, 13);

            ShownPressed = Track.PressedAt(time);
            centre.Alpha = ShownPressed ? 0.95f : 0.25f;
            cursor.Scale = new Vector2(ShownPressed ? 0.88f : 1);

            Track.CopyTrail(time, trail_length, trailPoints);

            trail.ClearVertices();

            foreach (var point in trailPoints)
                trail.AddVertex(point);

            // the path draws its vertices relative to its bounding box; this puts (0, 0) of the playfield where it belongs
            trail.OriginPosition = trail.PositionInBoundingBox(Vector2.Zero);
        }
    }
}

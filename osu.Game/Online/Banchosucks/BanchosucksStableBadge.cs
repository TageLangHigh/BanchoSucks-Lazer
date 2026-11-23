// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK.Graphics;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: a small "Stable" pill for scores that were imported from the stable server (song select leaderboard rows,
    /// results screen). Takes no room and shows nothing until <see cref="BanchosucksScoreOrigins"/> says the score is an import.
    /// </summary>
    /// <remarks>
    /// The lookup is only asked for once the badge is updated, which the framework does not do for rows scrolled out of a
    /// list, and it is withdrawn when the badge is disposed, so only scores on screen cost a request.
    /// </remarks>
    public partial class BanchosucksStableBadge : CompositeDrawable, IHasTooltip
    {
        private static readonly Color4 background_colour = Color4Extensions.FromHex(@"ff4964");

        private readonly ScoreInfo score;

        // absent in plain test scenes; the game always provides it
        [Resolved]
        private BanchosucksScoreOrigins? origins { get; set; }

        private Container pill = null!;

        private bool asked;
        private bool waiting;

        // null until the framework has checked it for the first time
        private bool? maskedAway;

        /// <summary>Whether the pill is shown (the score is known to come from stable).</summary>
        public bool Shown { get; private set; }

        public LocalisableString TooltipText { get; private set; }

        public BanchosucksStableBadge(ScoreInfo score)
        {
            this.score = score;

            // zero while the pill is hidden: hidden children do not count
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = pill = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 3,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = background_colour,
                    },
                    new OsuSpriteText
                    {
                        Text = @"STABLE",
                        Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Margin = new MarginPadding { Horizontal = 4, Vertical = 1 },
                        UseFullGlyphHeight = false,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (origins != null)
                origins.Resolved += onResolved;
        }

        protected override void Update()
        {
            base.Update();

            if (asked || origins == null)
                return;

            // not before the framework has said whether the badge is inside the visible part of its list
            if (maskedAway != false)
                return;

            asked = true;

            if (origins.Lookup(score) is bool known)
            {
                apply(known);
                return;
            }

            waiting = true;
            origins.Request(score);
        }

        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds)
        {
            bool result = base.ComputeIsMaskedAway(maskingBounds);
            maskedAway = result;
            return result;
        }

        private void onResolved(long scoreId, bool fromStable)
        {
            if (scoreId != score.OnlineID || IsDisposed)
                return;

            waiting = false;
            apply(fromStable);
        }

        private void apply(bool fromStable)
        {
            Shown = fromStable;

            if (!fromStable)
            {
                pill.FadeOut(100);
                return;
            }

            long? stableId = origins?.StableScoreId(score);

            TooltipText = stableId != null
                ? ClickerStrings.Text("Played in the stable client (stable score {0}) and taken over to lazer.", "Im Stable-Client gespielt (Stable-Score {0}) und nach Lazer übernommen.", stableId.Value.ToString())
                : ClickerStrings.Text("Played in the stable client and taken over to lazer.", "Im Stable-Client gespielt und nach Lazer übernommen.");

            pill.FadeIn(200, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (origins != null)
            {
                origins.Resolved -= onResolved;

                if (waiting)
                    origins.WithdrawLater(score);
            }

            base.Dispose(isDisposing);
        }
    }
}

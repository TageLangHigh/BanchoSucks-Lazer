// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Practice;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Banchosucks: the miss review of the extended statistics. Lists every miss and combo break of a score with its
    /// map time and, where the hit events allow it, the reason. A click opens the replay shortly before that miss if
    /// the replay is on this PC.
    /// </summary>
    /// <remarks>
    /// Everything is computed locally from <see cref="ScoreInfo.HitEvents"/> (see <see cref="BanchosucksMissClassifier"/>).
    /// </remarks>
    public partial class BanchosucksMissReview : CompositeDrawable
    {
        /// <summary>
        /// More lines than this are not shown (a failed run through a hard map can have hundreds of misses).
        /// </summary>
        public const int MAX_LINES = 100;

        private const float line_height = 22;

        public readonly IReadOnlyList<BanchosucksMiss> Misses;

        private readonly ScoreInfo score;

        private readonly Bindable<DownloadState> replayState = new Bindable<DownloadState>();

        private OsuSpriteText hint = null!;
        private MissLine[] lines = Array.Empty<MissLine>();

        [Resolved]
        private OsuGame? game { get; set; }

        /// <summary>
        /// Whether a click on a line can open the replay.
        /// </summary>
        public bool ReplayAvailable => replayState.Value == DownloadState.LocallyAvailable;

        /// <summary>
        /// Creates the entry for the statistics panel. It needs hit events, so the panel hides it for scores without them.
        /// </summary>
        public static StatisticItem CreateStatisticItem(ScoreInfo score) => new StatisticItem(
            ClickerStrings.Text("Miss review", "Miss-Auswertung"),
            () => new BanchosucksMissReview(score), requiresHitEvents: true);

        public BanchosucksMissReview(ScoreInfo score)
        {
            this.score = score;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            List<BanchosucksMiss> misses;

            try
            {
                misses = BanchosucksMissClassifier.Analyse(score.HitEvents, score.Ruleset.CreateInstance() as IBanchosucksHitAreaProvider);
            }
            catch (Exception e)
            {
                // statistics are extra; a score this cannot read must not take the results screen down
                Logger.Error(e, "Miss review could not read the hit events.");
                misses = new List<BanchosucksMiss>();
            }

            Misses = misses;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            string ruleset = score.Ruleset.ShortName;

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            InternalChildren = new Drawable[]
            {
                new ScoreDownloadTracker(score)
                {
                    State = { BindTarget = replayState },
                },
                flow,
            };

            if (Misses.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = ClickerStrings.Text("No misses and no combo breaks.", "Keine Misses und keine Combo-Breaks."),
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold),
                });
                return;
            }

            flow.Add(hint = new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE),
                Colour = colours.Gray9,
            });

            flow.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Children = lines = Misses.Take(MAX_LINES).Select(miss =>
                {
                    (string english, string german) = BanchosucksMissClassifier.Describe(miss, ruleset);

                    return new MissLine(miss, ClickerStrings.Text(english, german), colours)
                    {
                        Action = () => openReplay(miss),
                        Enabled = { Value = false },
                    };
                }).ToArray(),
            });

            if (Misses.Count > MAX_LINES)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = ClickerStrings.Text("... and {0} more", "... und {0} weitere", Misses.Count - MAX_LINES),
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE),
                    Colour = colours.Gray9,
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            replayState.BindValueChanged(_ =>
            {
                foreach (var line in lines)
                    line.Enabled.Value = ReplayAvailable && game != null;

                if (Misses.Count == 0)
                    return;

                hint.Text = ReplayAvailable && game != null
                    ? ClickerStrings.Text("Click a line to watch the replay from one second before it.", "Klick auf eine Zeile öffnet das Replay eine Sekunde davor.")
                    : ClickerStrings.Text("No replay of this score on this PC, so the lines cannot be opened.", "Zu diesem Score liegt kein Replay auf diesem PC, die Zeilen lassen sich nicht öffnen.");
            }, true);
        }

        private void openReplay(BanchosucksMiss miss)
        {
            if (!ReplayAvailable)
                return;

            game?.PresentScore(score, ScorePresentType.Gameplay, miss.Time);
        }

        public partial class MissLine : OsuClickableContainer
        {
            public readonly BanchosucksMiss Miss;

            private readonly Box hover;

            public MissLine(BanchosucksMiss miss, osu.Framework.Localisation.LocalisableString reason, OsuColour colours)
            {
                Miss = miss;

                // two columns
                RelativeSizeAxes = Axes.X;
                Width = 0.5f;
                Height = line_height;

                Children = new Drawable[]
                {
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 6,
                        Text = BanchosucksPracticeSection.FormatTime(miss.Time),
                        Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE + 1, weight: FontWeight.Bold, fixedWidth: true),
                        Colour = colours.Yellow,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 56, Right = 6 },
                        Child = new TruncatingSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            Text = reason,
                            Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE + 1),
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value)
                    hover.FadeTo(0.1f, 100);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(100);
                base.OnHoverLost(e);
            }

            // A click on a line that cannot open anything must not fall through to the panel behind it, which would close the statistics.
            protected override bool OnClick(ClickEvent e)
            {
                base.OnClick(e);
                return true;
            }
        }
    }
}

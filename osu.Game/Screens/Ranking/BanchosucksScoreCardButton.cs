// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Share;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Banchosucks: makes a picture of the shown score (<see cref="BanchosucksScoreCard"/>), saved to
    /// <c>screenshots/cards</c> and copied to the clipboard by <see cref="BanchosucksCardExporter"/>.
    /// </summary>
    public partial class BanchosucksScoreCardButton : GrayButton
    {
        public readonly Bindable<ScoreInfo?> Score = new Bindable<ScoreInfo?>();

        [Resolved]
        private BanchosucksCardExporter? exporter { get; set; }

        [Resolved]
        private BeatmapDifficultyCache? difficultyCache { get; set; }

        [Resolved]
        private BeatmapManager? beatmaps { get; set; }

        private bool creating;

        public BanchosucksScoreCardButton()
            : base(FontAwesome.Solid.Image)
        {
            Size = new Vector2(75, 30);
            TooltipText = ClickerStrings.Text("save score card (image)", "Score-Karte speichern (Bild)");
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = create;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Score.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);
        }

        /// <summary>
        /// Whether the button is offered for the current score (for tests).
        /// </summary>
        public bool Available { get; private set; }

        private void updateState()
        {
            Available = exporter != null && Score.Value != null;

            // an invisible button takes no room in the button row
            Enabled.Value = Available && !creating;
            this.FadeTo(Available ? 1 : 0, 200);
        }

        private void create()
        {
            var score = Score.Value;

            if (score == null || exporter == null || creating)
                return;

            creating = true;
            updateState();

            bool german = ClickerStrings.IsGerman;

            Task.Run(async () =>
            {
                var (stars, pp) = await calculate(score).ConfigureAwait(false);

                Schedule(() =>
                {
                    var data = BanchosucksScoreCardData.From(score, stars, pp, german);
                    var card = new BanchosucksScoreCard(data, () => loadBackground(score));

                    exporter.Export(card, data.FileName, ("Score card", "Score-Karte")).ContinueWith(_ => Schedule(() =>
                    {
                        creating = false;
                        updateState();
                    }));
                });
            });
        }

        /// <summary>
        /// Star rating with the score's mods, and pp for scores that carry none (what the results screen shows as well).
        /// Both stay unknown when the map is not on this PC or the calculation takes too long.
        /// </summary>
        private async Task<(double? stars, double? pp)> calculate(ScoreInfo score)
        {
            double? stars = null;
            double? pp = null;

            if (difficultyCache == null || score.BeatmapInfo == null)
                return (stars, pp);

            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));

                var difficulty = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo, score.Ruleset, score.Mods, cancellation.Token).ConfigureAwait(false);

                if (difficulty == null)
                    return (stars, pp);

                if (difficulty.Value.Stars > 0)
                    stars = difficulty.Value.Stars;

                if (score.PP == null && difficulty.Value.DifficultyAttributes != null)
                {
                    var calculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

                    if (calculator != null)
                        pp = (await calculator.CalculateAsync(score, difficulty.Value.DifficultyAttributes, cancellation.Token).ConfigureAwait(false)).Total;
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks: star rating / pp for the score card could not be calculated ({e.Message})");
            }

            return (stars, pp);
        }

        /// <summary>
        /// The beatmap's own background, when the map is on this PC. Runs while the card loads.
        /// </summary>
        private Texture? loadBackground(ScoreInfo score)
        {
            if (beatmaps == null || score.BeatmapInfo == null)
                return null;

            var working = beatmaps.GetWorkingBeatmap(score.BeatmapInfo);

            return working is DummyWorkingBeatmap ? null : working.GetBackground();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: what a difficulty is worth with a full combo at 95 %, 98 % and 100 % accuracy, for the pp preview in song select.
    /// </summary>
    /// <remarks>
    /// The numbers come from the ruleset's own performance calculator (in osu!mania that is the server's formula), fed with
    /// the difficulty attributes of <see cref="BeatmapDifficultyCache"/> and the judgement counts of
    /// <see cref="BanchosucksFullComboPlay"/>. Nothing is asked from the server.
    /// </remarks>
    public static class BanchosucksPerformancePreview
    {
        /// <summary>
        /// The accuracies shown, in display order.
        /// </summary>
        public static readonly IReadOnlyList<double> ACCURACIES = new[] { 0.95, 0.98, 1.0 };

        /// <summary>
        /// One entry of the preview.
        /// </summary>
        /// <param name="TargetAccuracy">The accuracy asked for (0.95, 0.98, 1).</param>
        /// <param name="Accuracy">The accuracy of the judgement counts that were used; differs slightly from the target on short maps.</param>
        /// <param name="Performance">The pp, or <c>null</c> when there is nothing to show (no full combo at this accuracy, no pp for this ruleset or these mods).</param>
        public readonly record struct Value(double TargetAccuracy, double Accuracy, double? Performance);

        /// <summary>
        /// Calculates the preview. Loads and converts the beatmap, so it belongs on a background thread.
        /// </summary>
        /// <param name="working">The beatmap.</param>
        /// <param name="ruleset">The ruleset to play it in.</param>
        /// <param name="mods">The mods to play it with. Must not change during the call.</param>
        /// <param name="attributes">The difficulty attributes for this beatmap, ruleset and mods.</param>
        /// <param name="cancellationToken">Stops the beatmap conversion.</param>
        public static Value[] Calculate(IWorkingBeatmap working, Ruleset ruleset, IReadOnlyList<Mod> mods, DifficultyAttributes attributes, CancellationToken cancellationToken = default)
        {
            var performanceCalculator = ruleset.CreatePerformanceCalculator();

            if (performanceCalculator == null || working.BeatmapInfo is not BeatmapInfo beatmapInfo)
                return ACCURACIES.Select(a => new Value(a, a, null)).ToArray();

            var playableBeatmap = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // the same way upstream builds its perfect play: let the score processor play the map with every judgement at its maximum.
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playableBeatmap);
            cancellationToken.ThrowIfCancellationRequested();

            var maximum = scoreProcessor.MaximumStatistics;
            var rule = BanchosucksFullComboPlay.GetRule(ruleset.ShortName);

            var values = new Value[ACCURACIES.Count];

            for (int i = 0; i < ACCURACIES.Count; i++)
            {
                double target = ACCURACIES[i];
                var statistics = BanchosucksFullComboPlay.CreateStatistics(rule, maximum, target, scoreProcessor.GetBaseScoreForResult);

                if (statistics == null)
                {
                    values[i] = new Value(target, target, null);
                    continue;
                }

                double accuracy = BanchosucksFullComboPlay.ComputeAccuracy(statistics, maximum, scoreProcessor.GetBaseScoreForResult);

                var score = new ScoreInfo(beatmapInfo, ruleset.RulesetInfo)
                {
                    Passed = true,
                    Accuracy = accuracy,
                    Mods = mods.ToArray(),
                    MaxCombo = scoreProcessor.MaximumCombo,
                    Combo = scoreProcessor.MaximumCombo,
                    // no performance calculator reads the total score of a lazer play; only the perfect play has an exact one.
                    TotalScore = target == 1 ? scoreProcessor.MaximumTotalScore : 0,
                    Statistics = statistics,
                    MaximumStatistics = maximum,
                };

                double total = performanceCalculator.Calculate(score, attributes).Total;

                // 0 means "this play gets no pp" (osu!mania converts and mods outside the server's list), which is shown as a dash.
                values[i] = new Value(target, accuracy, double.IsFinite(total) && total > 0 ? total : null);
            }

            return values;
        }
    }
}

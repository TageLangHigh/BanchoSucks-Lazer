// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: the rule behind the "pp if FC" counter. Takes the state of a running play and answers
    /// "what would this run look like up to here if nothing had broken the combo?". Pure; the pp itself always comes
    /// from the ruleset's own performance calculator, which is handed the projected score.
    /// </summary>
    /// <remarks>
    /// The exact rule, applied to the judgements made so far (nothing is assumed about the rest of the map):
    /// <list type="number">
    /// <item>
    /// Every combo-breaking result becomes the best result of the same kind of judgement that keeps the combo:
    /// <see cref="HitResult.Miss"/> becomes the ruleset's best basic result (<see cref="HitResult.Perfect"/> in osu!mania,
    /// <see cref="HitResult.Great"/> elsewhere, read from the beatmap's maximum statistics);
    /// <see cref="HitResult.LargeTickMiss"/> becomes <see cref="HitResult.LargeTickHit"/> (a slider break in osu!: a missed
    /// tick or repeat, with the Classic mod also a missed slider head; a missed droplet in osu!catch);
    /// <see cref="HitResult.ComboBreak"/> becomes <see cref="HitResult.IgnoreHit"/> (a hold note let go in osu!mania).
    /// </item>
    /// <item>
    /// Everything that did not break the combo stays exactly as it was played: every 100 and 50, small tick misses,
    /// dropped slider ends, bonus results. The projection is a full combo, not a perfect play.
    /// </item>
    /// <item>
    /// Accuracy is recalculated the way the score processor calculates it, from the base score of each result:
    /// (base score so far + what the replaced results gain) / maximum base score so far. So it rises by exactly what
    /// the removed misses cost and by nothing else.
    /// </item>
    /// <item>
    /// Max combo is the number of combo-increasing results after the replacement: the highest combo that could have
    /// been reached up to this point. A dropped slider end does not break combo but does not add to it either, and
    /// it stays dropped.
    /// </item>
    /// </list>
    /// What the data does not allow: knock-on effects of a break are not undone. With the Classic mod a missed slider
    /// tick also lowers the judgement of the slider itself (300 to 100); that lower judgement stays, because nothing
    /// records which 100 came from a slider break. The value is therefore a slightly careful estimate there, never a flattering one.
    /// </remarks>
    public static class BanchosucksFullComboProjection
    {
        /// <summary>
        /// The projected state of the run.
        /// </summary>
        /// <param name="Statistics">Result counts after the replacement.</param>
        /// <param name="Accuracy">0 to 1.</param>
        /// <param name="MaxCombo">Highest combo reachable up to this point.</param>
        /// <param name="Replaced">How many combo-breaking results were replaced (0 = the run is a full combo so far).</param>
        public readonly record struct Projection(Dictionary<HitResult, int> Statistics, double Accuracy, int MaxCombo, int Replaced);

        /// <summary>
        /// The result a combo-breaking result is replaced by, or null when <paramref name="result"/> does not break combo.
        /// </summary>
        public static HitResult? ReplacementFor(HitResult result, HitResult bestBasicResult)
        {
            if (!result.BreaksCombo())
                return null;

            switch (result)
            {
                case HitResult.Miss:
                    return bestBasicResult;

                case HitResult.LargeTickMiss:
                    return HitResult.LargeTickHit;

                case HitResult.ComboBreak:
                    return HitResult.IgnoreHit;

                default:
                    // a combo-breaking result this rule does not know (none exists today): leave it alone rather than guess
                    return null;
            }
        }

        /// <summary>
        /// The best basic result a hit object of this beatmap can give: the highest one in its maximum statistics.
        /// </summary>
        public static HitResult BestBasicResult(IReadOnlyDictionary<HitResult, int> maximumStatistics)
        {
            HitResult best = HitResult.None;

            foreach (var (result, count) in maximumStatistics)
            {
                if (count > 0 && result.IsBasic() && result.IsHit() && result > best)
                    best = result;
            }

            return best == HitResult.None ? HitResult.Great : best;
        }

        /// <param name="statistics">Result counts of the run so far.</param>
        /// <param name="bestBasicResult">See <see cref="BestBasicResult"/>.</param>
        /// <param name="baseScore">Sum of the base scores of the accuracy-affecting results so far.</param>
        /// <param name="maximumBaseScore">The same sum had every judgement so far been its maximum result.</param>
        /// <param name="baseScoreFor">The ruleset's base score of a result (<see cref="ScoreProcessor.GetBaseScoreForResult"/>).</param>
        public static Projection Project(IReadOnlyDictionary<HitResult, int> statistics, HitResult bestBasicResult, double baseScore, double maximumBaseScore, Func<HitResult, int> baseScoreFor)
        {
            var projected = new Dictionary<HitResult, int>(statistics);
            int replaced = 0;

            foreach (var (result, count) in statistics)
            {
                if (count <= 0)
                    continue;

                if (ReplacementFor(result, bestBasicResult) is not HitResult replacement)
                    continue;

                projected[result] = 0;
                projected[replacement] = projected.GetValueOrDefault(replacement) + count;
                replaced += count;

                // mirrors ScoreProcessor: only accuracy-affecting results carry base score
                double before = result.AffectsAccuracy() ? baseScoreFor(result) : 0;
                double after = replacement.AffectsAccuracy() ? baseScoreFor(replacement) : 0;
                baseScore += count * (after - before);
            }

            double accuracy = maximumBaseScore > 0 ? Math.Clamp(baseScore / maximumBaseScore, 0, 1) : 1;
            int maxCombo = projected.Where(kvp => kvp.Key.IncreasesCombo()).Sum(kvp => Math.Max(0, kvp.Value));

            return new Projection(projected, accuracy, maxCombo, replaced);
        }

        /// <summary>
        /// Projects a score that was just populated by <paramref name="processor"/> (see <see cref="ScoreProcessor.PopulateScore"/>) in place.
        /// </summary>
        public static Projection ApplyTo(ScoreInfo score, ScoreProcessor processor)
        {
            var processorStatistics = processor.GetScoreProcessorStatistics();

            var projection = Project(new Dictionary<HitResult, int>(score.Statistics), BestBasicResult(score.MaximumStatistics),
                processorStatistics.BaseScore, processorStatistics.MaximumBaseScore, processor.GetBaseScoreForResult);

            foreach (var (result, count) in projection.Statistics)
                score.Statistics[result] = count;

            score.Accuracy = projection.Accuracy;
            score.MaxCombo = projection.MaxCombo;
            score.Combo = projection.MaxCombo;

            return projection;
        }
    }
}

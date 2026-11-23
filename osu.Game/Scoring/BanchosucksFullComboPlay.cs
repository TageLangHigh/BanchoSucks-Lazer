// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: builds the judgement counts of a full combo at a given accuracy, for the pp preview in song select.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A performance calculator wants judgement counts, not an accuracy, and many different counts give the same accuracy.
    /// Upstream only ever builds the perfect play (every judgement at its maximum, see <c>BeatmapDifficultyCache</c>). This class
    /// starts from that perfect play and lowers judgements that do not break the combo until the accuracy, as the game
    /// itself would display it for these counts, is as close to the wanted one as the map allows. Which judgements are
    /// lowered is a fixed rule per ruleset:
    /// </para>
    /// <list type="bullet">
    /// <item>osu! and osu!taiko (<see cref="Rule.GreatAndOk"/>): GREAT becomes OK (100). No MEH, no miss; slider ticks and
    /// slider ends stay hit. This is the usual "x100, full combo" convention of pp calculators.</item>
    /// <item>osu!mania (<see cref="Rule.PerfectGreatAndGood"/>): no miss. Close to 100 % only PERFECT becomes GREAT, up to
    /// <see cref="MANIA_PERFECT_TO_GREAT_RATIO"/> PERFECT per GREAT. Below that the ratio stays and GOOD (200) fills the rest.
    /// PERFECT and GREAT are almost the same for the displayed accuracy (305 against 300) but not for the pp formula, so a
    /// rule without a fixed ratio would be either far too generous (no GREAT at all) or far too strict (no PERFECT at all).</item>
    /// <item>osu!catch (<see cref="Rule.TinyDroplets"/>): every fruit and droplet is caught, only tiny droplets are missed,
    /// because they are the only objects that cost accuracy without breaking the combo. A map with too few tiny droplets
    /// cannot be full-comboed at that accuracy and gets no value.</item>
    /// </list>
    /// <para>
    /// No pp formula lives here: the counts go to the ruleset's own performance calculator.
    /// </para>
    /// </remarks>
    public static class BanchosucksFullComboPlay
    {
        /// <summary>
        /// PERFECT judgements per GREAT judgement in the osu!mania rule, once the accuracy is low enough to need GOOD judgements.
        /// </summary>
        public const int MANIA_PERFECT_TO_GREAT_RATIO = 2;

        public enum Rule
        {
            /// <summary>
            /// The ruleset is not known here; only the perfect play can be built.
            /// </summary>
            None,

            GreatAndOk,
            PerfectGreatAndGood,
            TinyDroplets,
        }

        /// <summary>
        /// The rule for a ruleset, by its short name.
        /// </summary>
        public static Rule GetRule(string rulesetShortName)
        {
            switch (rulesetShortName)
            {
                case "osu":
                case "taiko":
                    return Rule.GreatAndOk;

                case "mania":
                    return Rule.PerfectGreatAndGood;

                case "fruits":
                    return Rule.TinyDroplets;

                default:
                    return Rule.None;
            }
        }

        /// <summary>
        /// Builds the judgement counts of a full combo.
        /// </summary>
        /// <param name="rule">Which judgements may be lowered.</param>
        /// <param name="maximum">The judgement counts of the perfect play.</param>
        /// <param name="accuracy">The wanted accuracy, from 0 to 1. 1 always returns the perfect play.</param>
        /// <param name="baseScore">What a judgement is worth for accuracy (the score processor's <c>GetBaseScoreForResult</c>).</param>
        /// <returns>The counts, or <c>null</c> when no full combo with this accuracy exists under the rule.</returns>
        public static Dictionary<HitResult, int>? CreateStatistics(Rule rule, IReadOnlyDictionary<HitResult, int> maximum, double accuracy, Func<HitResult, int> baseScore)
        {
            if (double.IsNaN(accuracy) || accuracy < 0 || accuracy > 1)
                return null;

            var statistics = maximum.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (accuracy == 1)
                return statistics;

            // what the play has to lose compared to the perfect one, in the units the score processor counts accuracy in.
            double loss = (1 - accuracy) * maximumBaseScore(maximum, baseScore);

            switch (rule)
            {
                case Rule.GreatAndOk:
                    return lower(statistics, HitResult.Great, HitResult.Ok, loss, baseScore) ? statistics : null;

                case Rule.TinyDroplets:
                    return lower(statistics, HitResult.SmallTickHit, HitResult.SmallTickMiss, loss, baseScore) ? statistics : null;

                case Rule.PerfectGreatAndGood:
                    return lowerMania(statistics, loss, baseScore) ? statistics : null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// The accuracy the game displays for the given counts: the same sum the score processor keeps during play.
        /// </summary>
        public static double ComputeAccuracy(IReadOnlyDictionary<HitResult, int> statistics, IReadOnlyDictionary<HitResult, int> maximum, Func<HitResult, int> baseScore)
        {
            double max = maximumBaseScore(maximum, baseScore);

            if (max == 0)
                return 1;

            return statistics.Where(kvp => kvp.Key.AffectsAccuracy()).Sum(kvp => (double)kvp.Value * baseScore(kvp.Key)) / max;
        }

        private static double maximumBaseScore(IReadOnlyDictionary<HitResult, int> maximum, Func<HitResult, int> baseScore)
            => maximum.Where(kvp => kvp.Key.AffectsAccuracy()).Sum(kvp => (double)kvp.Value * baseScore(kvp.Key));

        /// <summary>
        /// Turns as many <paramref name="from"/> judgements into <paramref name="to"/> as come closest to the wanted loss.
        /// </summary>
        private static bool lower(Dictionary<HitResult, int> statistics, HitResult from, HitResult to, double loss, Func<HitResult, int> baseScore)
        {
            int available = statistics.GetValueOrDefault(from);
            int step = baseScore(from) - baseScore(to);

            if (step <= 0)
                return false;

            int count = (int)Math.Round(loss / step, MidpointRounding.AwayFromZero);

            if (count > available)
                return false;

            move(statistics, from, to, count);
            return true;
        }

        private static bool lowerMania(Dictionary<HitResult, int> statistics, double loss, Func<HitResult, int> baseScore)
        {
            int notes = statistics.GetValueOrDefault(HitResult.Perfect);

            int greatStep = baseScore(HitResult.Perfect) - baseScore(HitResult.Great);
            int goodStep = baseScore(HitResult.Perfect) - baseScore(HitResult.Good);

            if (greatStep <= 0 || goodStep <= greatStep)
                return false;

            const double great_share = 1.0 / (MANIA_PERFECT_TO_GREAT_RATIO + 1);

            // near 100 %: GREAT alone is enough, as long as there are no more of them than the ratio allows.
            int greatsAlone = (int)Math.Round(loss / greatStep, MidpointRounding.AwayFromZero);

            if (greatsAlone <= ratioGreats(notes))
            {
                move(statistics, HitResult.Perfect, HitResult.Great, greatsAlone);
                return true;
            }

            // below: every note that is not a GOOD is a PERFECT or a GREAT in the fixed ratio.
            //   loss = goods * goodStep + (notes - goods) * great_share * greatStep
            int goods = (int)Math.Round((loss - notes * great_share * greatStep) / (goodStep - great_share * greatStep), MidpointRounding.AwayFromZero);

            if (goods < 0 || goods > notes)
                return false;

            move(statistics, HitResult.Perfect, HitResult.Good, goods);
            move(statistics, HitResult.Perfect, HitResult.Great, ratioGreats(notes - goods));
            return true;

            static int ratioGreats(int count) => (int)Math.Round(count * great_share, MidpointRounding.AwayFromZero);
        }

        private static void move(Dictionary<HitResult, int> statistics, HitResult from, HitResult to, int count)
        {
            if (count == 0)
                return;

            statistics[from] = statistics.GetValueOrDefault(from) - count;
            statistics[to] = statistics.GetValueOrDefault(to) + count;
        }
    }
}

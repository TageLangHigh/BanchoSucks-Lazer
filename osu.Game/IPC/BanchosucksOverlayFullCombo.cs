// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: the hit counts behind "pp if FC" of the overlay data source.
    /// </summary>
    /// <remarks>
    /// The question an overlay asks with that number is: what would this play be worth if it ended now without a single
    /// miss, the way it went so far? The answer here: every judgement that was not a miss stays as it is; misses and
    /// everything not played yet become the best judgement; slider ticks, slider ends and the like are all hit. Bonus
    /// (spinner) results stay as they are, they do not affect combo or accuracy. This is an estimate in the same spirit
    /// as tosu's, not the same formula.
    /// </remarks>
    public static class BanchosucksOverlayFullCombo
    {
        /// <param name="current">The hit counts of the play so far.</param>
        /// <param name="maximum">The hit counts of a perfect play of the whole map.</param>
        public static Dictionary<HitResult, int> Statistics(IReadOnlyDictionary<HitResult, int> current, IReadOnlyDictionary<HitResult, int> maximum)
        {
            var fullCombo = new Dictionary<HitResult, int>();

            HitResult? best = null;
            int basicTotal = 0;

            foreach (var (result, count) in maximum)
            {
                if (result.IsBasic())
                {
                    basicTotal += count;

                    if (best == null || result > best)
                        best = result;
                }
                else if (!result.IsBonus())
                {
                    // ticks, slider ends, ignored judgements: all hit in a full combo
                    fullCombo[result] = count;
                }
            }

            int kept = 0;

            foreach (var (result, count) in current)
            {
                if (result.IsBonus())
                    fullCombo[result] = count;
                else if (result.IsBasic() && result != HitResult.Miss)
                {
                    fullCombo[result] = count;
                    kept += count;
                }
            }

            if (best != null)
                fullCombo[best.Value] = fullCombo.GetValueOrDefault(best.Value) + Math.Max(0, basicTotal - kept);

            return fullCombo;
        }
    }
}

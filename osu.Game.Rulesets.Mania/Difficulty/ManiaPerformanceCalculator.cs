// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty.Banchosucks;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;
        private double scoreAccuracy;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Miss));
            scoreAccuracy = Math.Clamp(calculateCustomAccuracy(), 0, 1);

            // Banchosucks: the server's formula (ManiaPerformance). Converts from other rulesets and plays with mods
            // outside the neutral list get no pp, exactly like the server.
            bool maniaSpecific = score.BeatmapInfo == null || score.BeatmapInfo.Ruleset.OnlineID == 3;

            if (!maniaSpecific || !ManiaPerformance.ModsGivePerformance(score.Mods))
                return new ManiaPerformanceAttributes { Difficulty = 0, Total = 0 };

            // the map's note count (a long note once) when the difficulty calculation knew it, else the judgement count
            double notes = maniaAttributes.NoteCount > 0 ? maniaAttributes.NoteCount : totalHits;
            double difficultyValue = ManiaPerformance.DifficultyValue(maniaAttributes.StarRating, notes);
            double totalValue = difficultyValue * ManiaPerformance.AccuracyTerm(scoreAccuracy) * ManiaPerformance.ModMultiplier(score.Mods);

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                Total = totalValue
            };
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;

        /// <summary>
        /// Accuracy used to weight judgements independently from the score's actual accuracy.
        /// </summary>
        private double calculateCustomAccuracy()
        {
            if (totalHits == 0)
                return 0;

            return (countPerfect * 320 + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * 320);
        }
    }
}

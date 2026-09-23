// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    internal static class RelaxRhythmEvaluator
    {
        private const int history_time_max = 5 * 1000;
        private const int history_objects_max = 32;
        private const double rhythm_overall_multiplier = 0.95;
        private const double rhythm_ratio_multiplier = 12.0;

        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current, double hitWindow)
        {
            if (current.BaseObject is Spinner)
                return 0;

            double rhythmComplexitySum = 0;
            double deltaDifferenceEps = hitWindow * 0.3;

            RhythmIsland island = new RhythmIsland(deltaDifferenceEps);
            RhythmIsland prevIsland = new RhythmIsland(deltaDifferenceEps);
            var islandCounts = new List<IslandCount>();

            double startRatio = 0;
            bool firstDeltaSwitch = false;

            int historicalNoteCount = Math.Min(current.Index, history_objects_max);
            int rhythmStart = 0;

            while (current.Previous(rhythmStart) is OsuDifficultyHitObject prev && rhythmStart + 2 < historicalNoteCount && current.StartTime - prev.StartTime < history_time_max)
                rhythmStart++;

            if (current.Previous(rhythmStart) is not OsuDifficultyHitObject previous || current.Previous(rhythmStart + 1) is not OsuDifficultyHitObject last)
                return adjust(rhythmComplexitySum);

            for (int i = rhythmStart; i >= 1; i--)
            {
                if (current.Previous(i - 1) is not OsuDifficultyHitObject currObj)
                    break;

                double timeDecay = (history_time_max - (current.StartTime - currObj.StartTime)) / history_time_max;
                double noteDecay = (double)(historicalNoteCount - i) / historicalNoteCount;
                double currHistoricalDecay = Math.Min(noteDecay, timeDecay);

                double currDelta = currObj.AdjustedDeltaTime;
                double prevDelta = previous.AdjustedDeltaTime;
                double lastDelta = last.AdjustedDeltaTime;

                double deltaDifferenceRatio = Math.Min(prevDelta, currDelta) / Math.Max(prevDelta, currDelta);
                double currRatio = 1 + rhythm_ratio_multiplier * Math.Min(DiffUtils.Pow(Math.Sin(Math.PI / deltaDifferenceRatio), 2), 0.5);

                double fraction = Math.Max(prevDelta / currDelta, currDelta / prevDelta);
                double fractionMultiplier = Math.Clamp(2.0 - fraction / 8.0, 0.0, 1.0);

                double windowPenalty = Math.Min(Math.Max(Math.Abs(prevDelta - currDelta) - deltaDifferenceEps, 0) / deltaDifferenceEps, 1.0);
                double effectiveRatio = windowPenalty * currRatio * fractionMultiplier;

                if (firstDeltaSwitch)
                {
                    if (Math.Abs(prevDelta - currDelta) < deltaDifferenceEps)
                    {
                        island.AddDelta((int)currDelta);
                    }
                    else
                    {
                        if (currObj.BaseObject is Slider)
                            effectiveRatio *= 0.125;

                        if (previous.BaseObject is Slider)
                            effectiveRatio *= 0.3;

                        if (island.IsSimilarPolarity(prevIsland))
                            effectiveRatio *= 0.5;

                        if (lastDelta > prevDelta + deltaDifferenceEps && prevDelta > currDelta + deltaDifferenceEps)
                            effectiveRatio *= 0.125;

                        if (prevIsland.DeltaCount == island.DeltaCount)
                            effectiveRatio *= 0.5;

                        int entryIndex = islandCounts.FindIndex(entry => entry.Island.Equals(island) && !entry.Island.IsDefault);

                        if (entryIndex >= 0)
                        {
                            IslandCount entry = islandCounts[entryIndex];
                            if (prevIsland.Equals(island))
                                entry.Count += 1;

                            double power = DiffUtils.Logistic(island.Delta, 58.33, 0.24, 2.75);
                            effectiveRatio *= Math.Min(3.0 / entry.Count, DiffUtils.Pow(1.0 / entry.Count, power));

                            islandCounts[entryIndex] = entry;
                        }
                        else
                        {
                            islandCounts.Add(new IslandCount(island));
                        }

                        double doubletapness = previous.CalculateDoubleTapFeasibility(currObj);
                        effectiveRatio *= 1.0 - doubletapness * 0.75;

                        rhythmComplexitySum += Math.Sqrt(effectiveRatio * startRatio) * currHistoricalDecay;

                        startRatio = effectiveRatio;
                        prevIsland = island;

                        if (prevDelta + deltaDifferenceEps < currDelta)
                            firstDeltaSwitch = false;

                        island = RhythmIsland.CreateWithDelta((int)currDelta, deltaDifferenceEps);
                    }
                }
                else if (prevDelta > currDelta + deltaDifferenceEps)
                {
                    firstDeltaSwitch = true;

                    if (currObj.BaseObject is Slider)
                        effectiveRatio *= 0.6;

                    if (previous.BaseObject is Slider)
                        effectiveRatio *= 0.6;

                    startRatio = effectiveRatio;
                    island = RhythmIsland.CreateWithDelta((int)currDelta, deltaDifferenceEps);
                }

                last = previous;
                previous = currObj;
            }

            return adjust(rhythmComplexitySum);

            static double adjust(double complexitySum) => Math.Sqrt(4.0 + complexitySum * rhythm_overall_multiplier) / 2.0;
        }

        private struct RhythmIsland : IEquatable<RhythmIsland>
        {
            private const int min_delta_time = OsuDifficultyHitObject.MIN_DELTA_TIME;

            public RhythmIsland(double deltaDifferenceEps)
            {
                DeltaDifferenceEps = deltaDifferenceEps;
                Delta = int.MaxValue;
                DeltaCount = 0;
            }

            private RhythmIsland(double deltaDifferenceEps, int delta, int deltaCount)
            {
                DeltaDifferenceEps = deltaDifferenceEps;
                Delta = delta;
                DeltaCount = deltaCount;
            }

            public double DeltaDifferenceEps { get; }
            public int Delta { get; private set; }
            public int DeltaCount { get; private set; }

            public static RhythmIsland CreateWithDelta(int delta, double deltaDifferenceEps)
                => new RhythmIsland(deltaDifferenceEps, Math.Max(delta, min_delta_time), 1);

            public void AddDelta(int delta)
            {
                if (Delta == int.MaxValue)
                    Delta = Math.Max(delta, min_delta_time);

                DeltaCount++;
            }

            public bool IsSimilarPolarity(RhythmIsland other) => DeltaCount % 2 == other.DeltaCount % 2;

            public bool IsDefault => Math.Abs(DeltaDifferenceEps) < double.Epsilon && Delta == int.MaxValue && DeltaCount == 0;

            public bool Equals(RhythmIsland other) => Math.Abs(Delta - other.Delta) < DeltaDifferenceEps && DeltaCount == other.DeltaCount;
        }

        private struct IslandCount
        {
            public IslandCount(RhythmIsland island)
            {
                Island = island;
                Count = 1;
            }

            public RhythmIsland Island { get; }
            public double Count { get; set; }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Banchosucks
{
    /// <summary>
    /// Banchosucks: the star rating of the part of a map played so far, for the timed attributes behind the in-game pp
    /// counter. The timeline of <see cref="ManiaRating.Result"/> is aggregated incrementally (corners are added in time
    /// order, percentiles come from a weight histogram), so the thousands of calls of a timed calculation stay cheap.
    /// The value at the end differs from the exact rating only by the histogram resolution; the calculator returns the
    /// exact value once the whole map is in.
    /// </summary>
    public sealed class ManiaRatingProgress
    {
        private const int bucket_bits = 13;
        private const int buckets = 1 << bucket_bits;

        private readonly ManiaRating.Result full;
        private readonly double bucketWidth;
        private readonly double[] tree = new double[buckets + 1];

        private double totalWeight;
        private double powerSum;
        private double effectiveNotes;
        private int nextCorner;
        private int nextNote;

        public ManiaRatingProgress(ManiaRating.Result full)
        {
            this.full = full;
            double maxDifficulty = full.Difficulty.Length > 0 ? full.Difficulty.Max() : 0;
            bucketWidth = maxDifficulty > 0 ? maxDifficulty / (buckets - 1) : 1;
        }

        /// <summary>
        /// Takes every corner and note up to <paramref name="timeLimit"/> (ms at the played rate) into the aggregation.
        /// </summary>
        public void Advance(double timeLimit)
        {
            while (nextCorner < full.CornerTimes.Length && full.CornerTimes[nextCorner] <= timeLimit)
            {
                double d = full.Difficulty[nextCorner];
                double w = full.Weight[nextCorner];
                add(bucketOf(d), w);
                totalWeight += w;
                powerSum += Math.Pow(d, 5) * w;
                nextCorner++;
            }

            while (nextNote < full.Heads.Length && full.Heads[nextNote] <= timeLimit)
            {
                effectiveNotes = full.EffectiveNotesPrefix[nextNote];
                nextNote++;
            }
        }

        public double StarRating()
        {
            if (totalWeight <= 0)
                return 0;

            double percentile93 = (quantile(0.945) + quantile(0.935) + quantile(0.925) + quantile(0.915)) / 4;
            double percentile83 = (quantile(0.845) + quantile(0.835) + quantile(0.825) + quantile(0.815)) / 4;
            double powerMean = Math.Pow(powerSum / totalWeight, 1 / 5.0);
            return ManiaRating.FinishStarRating(percentile93, percentile83, powerMean, effectiveNotes);
        }

        private int bucketOf(double difficulty) => Math.Clamp((int)(difficulty / bucketWidth), 0, buckets - 1);

        private void add(int bucket, double weight)
        {
            for (int i = bucket + 1; i <= buckets; i += i & -i)
                tree[i] += weight;
        }

        /// <summary>The centre of the first bucket at which the cumulative weight reaches the quantile.</summary>
        private double quantile(double q)
        {
            double remaining = q * totalWeight;
            int position = 0;

            for (int step = buckets; step > 0; step >>= 1)
            {
                if (position + step <= buckets && tree[position + step] < remaining)
                {
                    position += step;
                    remaining -= tree[position];
                }
            }

            return (Math.Min(position, buckets - 1) + 0.5) * bucketWidth;
        }
    }
}

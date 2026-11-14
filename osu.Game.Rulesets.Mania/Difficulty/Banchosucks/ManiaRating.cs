// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Banchosucks
{
    /// <summary>
    /// Banchosucks: osu!mania star rating after sunnyxxy's "Star Rating Rebirth" (1-10 keys, long notes included).
    /// A line-by-line port of the server's Python package (bancho.py <c>app/maniadiff/difficulty.py</c>, algorithm
    /// version 1), so song select shows the stars the server rates and pays pp for. Server and client must stay
    /// numerically identical: a change on one side needs the same change on the other and a version bump on both.
    /// The Python file documents the five series J (same-column), X (cross-column), P (pressing), A (unevenness)
    /// and R (release) and the aggregation; the names here follow it.
    /// </summary>
    public static class ManiaRating
    {
        public const int ALGORITHM_VERSION = 1;

        public const int MAX_KEYS = 10;

        private const double smooth_window_ms = 500;
        private const double unevenness_window_ms = 250;
        private const double norm = 5.0;
        private const double jack_intercept = 0.11;
        private const double ln_body_weight = 6.0;
        private const double chord_penalty = 24.0;
        private const double release_weight = 0.8;
        private const double w0 = 0.4;
        private const double w1 = 2.7;
        private const double p1 = 1.5;
        private const double w2 = 0.27;

        private static readonly double[][] cross_coefficients =
        {
            Array.Empty<double>(),
            new[] { 0.075, 0.075 },
            new[] { 0.125, 0.05, 0.125 },
            new[] { 0.125, 0.125, 0.125, 0.125 },
            new[] { 0.175, 0.25, 0.05, 0.25, 0.175 },
            new[] { 0.175, 0.25, 0.175, 0.175, 0.25, 0.175 },
            new[] { 0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225 },
            new[] { 0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225 },
            new[] { 0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275 },
            new[] { 0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275 },
            new[] { 0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325 },
        };

        /// <summary>
        /// A note as the algorithm sees it: column, head time and tail time in whole milliseconds (tail -1 for a tap).
        /// </summary>
        public readonly struct Note
        {
            public readonly int Column;
            public readonly int Head;
            public readonly int Tail;

            public bool IsLong => Tail >= 0;

            public Note(int column, int head, int tail)
            {
                Column = column;
                Head = head;
                Tail = tail;
            }
        }

        /// <summary>
        /// The rating of one map at one clock rate, plus the difficulty timeline the progressive (timed) calculation
        /// aggregates from.
        /// </summary>
        public sealed class Result
        {
            public double StarRating;
            public double Rate;
            public int KeyCount;
            public int NoteCount;
            public int LongNoteCount;

            public double Jack;
            public double Cross;
            public double Press;
            public double Uneven;
            public double Release;

            /// <summary>Corner times (ms at the played rate) the difficulty was evaluated on.</summary>
            public double[] CornerTimes = Array.Empty<double>();

            /// <summary>Difficulty D at every corner.</summary>
            public double[] Difficulty = Array.Empty<double>();

            /// <summary>Density weight of every corner.</summary>
            public double[] Weight = Array.Empty<double>();

            /// <summary>Head times of the notes in algorithm order (ascending).</summary>
            public int[] Heads = Array.Empty<int>();

            /// <summary>Effective note count after the first i+1 notes (notes plus half the capped long-note length share).</summary>
            public double[] EffectiveNotesPrefix = Array.Empty<double>();
        }

        /// <summary>
        /// x in the paper: the hit leniency derived from the map's OD (the two-sided 300 window).
        /// </summary>
        public static double HitLeniency(double overallDifficulty)
        {
            double x = 0.3 * Math.Pow((64.5 - Math.Ceiling(overallDifficulty * 3)) / 500, 0.5);
            return Math.Min(x, 0.6 * (x - 0.09) + 0.09);
        }

        /// <summary>
        /// Makes a note list legal the way the server's parser does: one note per column and time, long-note tails end
        /// before the next head in the column, result sorted by head then column.
        /// </summary>
        public static Note[] Sanitise(IEnumerable<Note> notes, int keyCount)
        {
            var byColumn = new List<Note>[keyCount];
            for (int k = 0; k < keyCount; k++)
                byColumn[k] = new List<Note>();

            var seen = new HashSet<(int column, int head)>();

            foreach (var note in notes.OrderBy(n => n.Head).ThenBy(n => n.Column).ThenBy(n => n.Tail))
            {
                if (note.Column < 0 || note.Column >= keyCount)
                    continue;
                if (!seen.Add((note.Column, note.Head)))
                    continue;

                byColumn[note.Column].Add(note);
            }

            var cleaned = new List<Note>();

            for (int k = 0; k < keyCount; k++)
            {
                var column = byColumn[k];

                for (int i = 0; i < column.Count; i++)
                {
                    var note = column[i];
                    int tail = note.Tail;

                    if (tail >= 0)
                    {
                        if (tail < note.Head)
                            tail = note.Head;

                        if (i + 1 < column.Count && tail >= column[i + 1].Head)
                        {
                            tail = column[i + 1].Head - 1;
                            if (tail < note.Head)
                                tail = -1;
                        }
                    }

                    cleaned.Add(new Note(note.Column, note.Head, tail));
                }
            }

            return cleaned.OrderBy(n => n.Head).ThenBy(n => n.Column).ToArray();
        }

        /// <summary>
        /// The final blend of the percentiles and the power mean, with the note-count scaling, the 9+ rescale and the 0.975.
        /// Shared with the progressive aggregation.
        /// </summary>
        public static double FinishStarRating(double percentile93, double percentile83, double powerMean, double effectiveNotes)
        {
            double starRating = 0.88 * percentile93 * 0.25 + 0.94 * percentile83 * 0.2 + powerMean * 0.55;
            starRating *= effectiveNotes / (effectiveNotes + 60);
            if (starRating > 9)
                starRating = 9 + (starRating - 9) / 1.2;
            return starRating * 0.975;
        }

        /// <summary>
        /// Star rating and skill values of a mania map at a clock rate (1.0 NM, 1.5 DT, 0.75 HT).
        /// </summary>
        /// <param name="rawNotes">The notes at normal speed, in any order.</param>
        /// <param name="keyCount">Columns of the map (1 to 10).</param>
        /// <param name="overallDifficulty">The map's OD as written in the file, without mod adjustments.</param>
        /// <param name="rate">The clock rate; times are divided by it and truncated to whole milliseconds.</param>
        public static Result Calculate(IReadOnlyList<Note> rawNotes, int keyCount, double overallDifficulty, double rate = 1.0)
        {
            if (keyCount < 1 || keyCount > MAX_KEYS)
                throw new ArgumentOutOfRangeException(nameof(keyCount), $"unsupported key count {keyCount}");
            if (rate <= 0)
                throw new ArgumentOutOfRangeException(nameof(rate), "rate must be positive");

            Note[] notes = Sanitise(rawNotes, keyCount);

            if (rate != 1.0)
            {
                for (int i = 0; i < notes.Length; i++)
                {
                    var n = notes[i];
                    notes[i] = new Note(n.Column, (int)Math.Floor(n.Head / rate), n.IsLong ? (int)Math.Floor(n.Tail / rate) : -1);
                }

                notes = notes.OrderBy(n => n.Head).ThenBy(n => n.Column).ToArray();
            }

            int count = notes.Length;
            var result = new Result { Rate = rate, KeyCount = keyCount, NoteCount = count, LongNoteCount = notes.Count(n => n.IsLong) };

            if (count < 2)
                return result;

            double x = HitLeniency(overallDifficulty);
            int[] columns = notes.Select(n => n.Column).ToArray();
            int[] heads = notes.Select(n => n.Head).ToArray();
            int[] tails = notes.Select(n => n.Tail).ToArray();
            int maxTail = tails.Max();
            int t = Math.Max(heads.Max(), maxTail) + 1;

            // ---- corners: where the piecewise-linear series can bend --------------------------------
            var boundaryList = new List<int>(count * 2);
            boundaryList.AddRange(heads);
            boundaryList.AddRange(tails.Where(v => v >= 0));
            int[] boundaries = boundaryList.Distinct().OrderBy(v => v).ToArray();

            var baseSet = new List<double>(boundaries.Length * 4 + 2) { 0, t };

            foreach (int b in boundaries)
            {
                baseSet.Add(b);
                baseSet.Add(b + 501);
                baseSet.Add(b - 499);
                baseSet.Add(b + 1);
            }

            double[] baseX = baseSet.Where(v => v >= 0 && v <= t).Distinct().OrderBy(v => v).ToArray();

            var aSet = new List<double>(boundaries.Length * 3 + 2) { 0, t };

            foreach (int b in boundaries)
            {
                aSet.Add(b);
                aSet.Add(b + 1000);
                aSet.Add(b - 1000);
            }

            double[] aCorners = aSet.Where(v => v >= 0 && v <= t).Distinct().OrderBy(v => v).ToArray();
            double[] corners = baseX.Concat(aCorners).Distinct().OrderBy(v => v).ToArray();
            int nBase = baseX.Length;

            var perColumn = new List<int>[keyCount];
            for (int k = 0; k < keyCount; k++)
                perColumn[k] = new List<int>();
            for (int i = 0; i < count; i++)
                perColumn[columns[i]].Add(i);

            // ---- key usage: active columns (+-150 ms around notes and long-note bodies) ------------
            var usage = new bool[keyCount, nBase];
            var usage400 = new double[keyCount, nBase];

            for (int k = 0; k < keyCount; k++)
            {
                foreach (int j in perColumn[k])
                {
                    int h = heads[j];
                    int tl = tails[j];
                    bool isLong = tl >= 0;

                    int start = Math.Max(h - 150, 0);
                    int end = isLong ? Math.Min(tl + 150, t - 1) : h + 150;

                    for (int i = lowerBound(baseX, start); i < lowerBound(baseX, end); i++)
                        usage[k, i] = true;

                    int start2 = Math.Max(h, 0);
                    int end2 = isLong ? Math.Min(tl, t - 1) : h;
                    int left400 = lowerBound(baseX, start2 - 400);
                    int left = lowerBound(baseX, start2);
                    int right = lowerBound(baseX, end2);
                    int right400 = lowerBound(baseX, end2 + 400);
                    double plateau = 3.75 + Math.Min(end2 - start2, 1500) / 150.0;

                    for (int i = left; i < right; i++)
                        usage400[k, i] += plateau;

                    for (int i = left400; i < left; i++)
                    {
                        double d = baseX[i] - start2;
                        usage400[k, i] += 3.75 - 3.75 / (400.0 * 400.0) * (d * d);
                    }

                    for (int i = right; i < right400; i++)
                    {
                        double d = Math.Abs(baseX[i] - end2);
                        usage400[k, i] += 3.75 - 3.75 / (400.0 * 400.0) * (d * d);
                    }
                }
            }

            // ---- anchor: how evenly the busy columns share the load ----------------------------------
            var anchor = new double[nBase];
            var columnLoad = new double[keyCount];

            for (int i = 0; i < nBase; i++)
            {
                for (int k = 0; k < keyCount; k++)
                    columnLoad[k] = usage400[k, i];

                Array.Sort(columnLoad);

                double walk = 0, maxWalk = 0;

                for (int j = keyCount - 1; j >= 1; j--)
                {
                    double upper = columnLoad[j];
                    double lower = columnLoad[j - 1];

                    if (lower > 0)
                    {
                        double ratio = lower / (upper > 0 ? upper : 1.0);
                        double centred = 0.5 - ratio;
                        walk += upper * (1 - 4 * (centred * centred));
                        maxWalk += upper;
                    }
                }

                double a = maxWalk > 0 ? walk / maxWalk : 0;
                anchor[i] = 1 + Math.Min(a - 0.18, 5 * Math.Pow(a - 0.22, 3));
            }

            // ---- J: same-column pressure ------------------------------------------------------------
            var jCols = new double[keyCount][];
            var deltaCols = new double[keyCount][];

            for (int k = 0; k < keyCount; k++)
            {
                jCols[k] = new double[nBase];
                deltaCols[k] = new double[nBase];
                Array.Fill(deltaCols[k], 1e9);

                var idx = perColumn[k];
                if (idx.Count < 2)
                    continue;

                double intercept = jack_intercept * Math.Pow(x, 0.25);

                for (int m = 0; m + 1 < idx.Count; m++)
                {
                    int h0 = heads[idx[m]], h1 = heads[idx[m + 1]];
                    double delta = 0.001 * (h1 - h0);
                    double value = 1 / delta * (1 / (delta + intercept));
                    double nerf = 1 - 7e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);
                    fill(jCols[k], baseX, h0, h1, value * nerf, false);
                    fill(deltaCols[k], baseX, h0, h1, delta, false);
                }
            }

            var jSmooth = new double[keyCount][];
            for (int k = 0; k < keyCount; k++)
                jSmooth[k] = smooth(baseX, jCols[k], smooth_window_ms, 0.001, false);

            var jSeries = new double[nBase];

            for (int i = 0; i < nBase; i++)
            {
                double weightedSum = 0, weightSum = 0;

                for (int k = 0; k < keyCount; k++)
                {
                    double columnWeight = 1 / deltaCols[k][i];
                    weightedSum += Math.Pow(Math.Max(jSmooth[k][i], 0), norm) * columnWeight;
                    weightSum += columnWeight;
                }

                jSeries[i] = Math.Pow(weightedSum / Math.Max(1e-9, weightSum), 1 / norm);
            }

            // ---- X: cross-column pressure -----------------------------------------------------------
            double[] coefficients = cross_coefficients[keyCount];
            var xPairs = new double[keyCount + 1][];
            var fastCross = new double[keyCount + 1][];

            for (int k = 0; k <= keyCount; k++)
            {
                xPairs[k] = new double[nBase];
                fastCross[k] = new double[nBase];

                List<int> idx;

                if (k == 0)
                    idx = perColumn[0];
                else if (k == keyCount)
                    idx = perColumn[keyCount - 1];
                else
                    idx = mergeByHead(perColumn[k - 1], perColumn[k], heads);

                if (idx.Count < 2)
                    continue;

                for (int m = 0; m + 1 < idx.Count; m++)
                {
                    int s = heads[idx[m]], e = heads[idx[m + 1]];
                    double delta = 0.001 * (e - s);
                    double value = 0.16 * Math.Pow(Math.Max(x, delta), -2);
                    int left = lowerBound(baseX, s);
                    int right = lowerBound(baseX, e);

                    bool inactivePair = (!active(k - 1, left) && !active(k - 1, right)) || (!active(k, left) && !active(k, right));
                    if (inactivePair)
                        value *= 1 - coefficients[k];

                    fill(xPairs[k], baseX, s, e, value, false);
                    double fast = Math.Max(0, 0.4 * Math.Pow(Math.Max(delta, Math.Max(0.06, 0.75 * x)), -2) - 80);
                    fill(fastCross[k], baseX, s, e, fast, false);
                }
            }

            bool active(int column, int at) => column >= 0 && column < keyCount && usage[column, Math.Min(at, nBase - 1)];

            var xBase = new double[nBase];

            for (int i = 0; i < nBase; i++)
            {
                double sum = 0;
                for (int k = 0; k <= keyCount; k++)
                    sum += xPairs[k][i] * coefficients[k];
                for (int k = 0; k < keyCount; k++)
                    sum += Math.Sqrt(fastCross[k][i] * coefficients[k] * fastCross[k + 1][i] * coefficients[k + 1]);
                xBase[i] = sum;
            }

            double[] xSeries = smooth(baseX, xBase, smooth_window_ms, 0.001, false);

            // ---- P: pressing intensity --------------------------------------------------------------
            var lnHeads = new List<int>();
            var lnTails = new List<int>();

            for (int i = 0; i < count; i++)
            {
                if (tails[i] >= 0)
                {
                    lnHeads.Add(heads[i]);
                    lnTails.Add(tails[i]);
                }
            }

            var changeAt = new List<int>(lnHeads.Count * 3);
            var changeBy = new List<double>(lnHeads.Count * 3);

            for (int i = 0; i < lnHeads.Count; i++)
            {
                changeAt.Add(Math.Min(lnHeads[i] + 60, lnTails[i]));
                changeBy.Add(1.3);
            }

            for (int i = 0; i < lnHeads.Count; i++)
            {
                changeAt.Add(Math.Min(lnHeads[i] + 120, lnTails[i]));
                changeBy.Add(-0.3);
            }

            for (int i = 0; i < lnHeads.Count; i++)
            {
                changeAt.Add(lnTails[i]);
                changeBy.Add(-1.0);
            }

            double[] points = changeAt.Select(v => (double)v).Concat(new double[] { 0, t }).Distinct().OrderBy(v => v).ToArray();
            var deltas = new double[points.Length];
            for (int i = 0; i < changeAt.Count; i++)
                deltas[lowerBound(points, changeAt[i])] += changeBy[i];

            var bodyValues = new double[points.Length];
            double running = 0;

            for (int i = 0; i < points.Length; i++)
            {
                running += deltas[i];
                bodyValues[i] = Math.Min(running, 2.5 + 0.5 * running);
            }

            var bodyCumulative = new double[points.Length];
            for (int i = 1; i < points.Length; i++)
                bodyCumulative[i] = bodyCumulative[i - 1] + bodyValues[i - 1] * (points[i] - points[i - 1]);

            double bodyIntegral(double a, double b)
            {
                int i = upperBound(points, a) - 1;
                int j = upperBound(points, b) - 1;

                // A note head before 0 ms lies left of every point (i = -1). The server's Python reads index -1 there, which
                // is the last value (0, after the end of all long notes); reading bodyValues[-1] here threw instead, the map
                // kept no stored star rating and showed none. Reading the last value keeps both sides identical.
                double first = bodyValues[i < 0 ? points.Length - 1 : i];

                if (i == j)
                    return (b - a) * first;

                int next = Math.Min(i + 1, points.Length - 1);
                return (points[next] - a) * first + (bodyCumulative[j] - bodyCumulative[next]) + (b - points[j]) * bodyValues[j];
            }

            var pStep = new double[nBase];
            double spike = 1000 * Math.Pow(0.02 * (4 / x - chord_penalty), 0.25);

            for (int m = 0; m + 1 < count; m++)
            {
                int hLeft = heads[m], hRight = heads[m + 1];
                int gap = hRight - hLeft;

                if (gap == 0)
                {
                    pStep[lowerBound(baseX, hLeft)] += spike;
                    continue;
                }

                double delta = 0.001 * gap;
                double lnValue = 1 + ln_body_weight * 0.001 * bodyIntegral(hLeft, hRight);
                double nps = 7.5 / delta;
                double booster = nps > 160 && nps < 360 ? 1 + 1.7e-7 * (nps - 160) * ((nps - 360) * (nps - 360)) : 1.0;
                double bracket = delta < 2 * x / 3 ? (delta - x / 2) * (delta - x / 2) : (x / 6) * (x / 6);
                double increment = 1 / delta * Math.Pow(0.08 / x * (1 - chord_penalty / x * bracket), 0.25) * Math.Max(booster, lnValue);
                int left = lowerBound(baseX, hLeft);
                int right = lowerBound(baseX, hRight);

                for (int i = left; i < right; i++)
                    pStep[i] += Math.Min(increment * anchor[i], Math.Max(increment, increment * 2 - 10));
            }

            double[] pSeries = smooth(baseX, pStep, smooth_window_ms, 0.001, false);

            // ---- A: unevenness ----------------------------------------------------------------------
            var nextActive = new int[keyCount + 1, nBase];
            for (int i = 0; i < nBase; i++)
                nextActive[keyCount, i] = keyCount;

            for (int k = keyCount - 1; k >= 0; k--)
            {
                for (int i = 0; i < nBase; i++)
                    nextActive[k, i] = usage[k, i] ? k : nextActive[k + 1, i];
            }

            var aBase = new double[nBase];
            Array.Fill(aBase, 1.0);

            for (int k0 = 0; k0 + 1 < keyCount; k0++)
            {
                for (int i = 0; i < nBase; i++)
                {
                    int k1 = nextActive[k0 + 1, i];
                    if (!usage[k0, i] || k1 >= keyCount)
                        continue;

                    double d0 = deltaCols[k0][i];
                    double d1 = deltaCols[Math.Min(k1, keyCount - 1)][i];
                    double largest = Math.Max(d0, d1);
                    double d = Math.Abs(d0 - d1) + 0.4 * Math.Max(0, largest - 0.11);
                    double f = d < 0.02 ? Math.Min(0.75 + 0.5 * largest, 1) : d < 0.07 ? Math.Min(0.65 + 5 * d + 0.5 * largest, 1) : 1.0;
                    aBase[i] *= f;
                }
            }

            var aStep = new double[aCorners.Length];
            for (int j = 0; j < aCorners.Length; j++)
                aStep[j] = aBase[Math.Min(lowerBound(baseX, aCorners[j]), nBase - 1)];

            double[] aSeries = smooth(aCorners, aStep, unevenness_window_ms, 1.0, true);

            // ---- R: release factor ------------------------------------------------------------------
            var rStep = new double[nBase];

            if (result.LongNoteCount >= 2)
            {
                var nextHead = new long[count];
                Array.Fill(nextHead, 1_000_000_000L);

                for (int k = 0; k < keyCount; k++)
                {
                    var idx = perColumn[k];
                    for (int m = 0; m + 1 < idx.Count; m++)
                        nextHead[idx[m]] = heads[idx[m + 1]];
                }

                int[] order = Enumerable.Range(0, count).Where(i => tails[i] >= 0).OrderBy(i => tails[i]).ToArray();
                var releaseIndex = new double[order.Length];

                for (int m = 0; m < order.Length; m++)
                {
                    int i = order[m];
                    double spacingHead = 0.001 * Math.Abs(tails[i] - heads[i] - 80) / x;
                    double spacingTail = 0.001 * Math.Abs(nextHead[i] - tails[i] - 80) / x;
                    releaseIndex[m] = 2 / (2 + Math.Exp(-5 * (spacingHead - 0.75)) + Math.Exp(-5 * (spacingTail - 0.75)));
                }

                for (int m = 0; m + 1 < order.Length; m++)
                {
                    int t0 = tails[order[m]], t1 = tails[order[m + 1]];
                    double deltaR = 0.001 * (t1 - t0);
                    double value = 0.08 * Math.Pow(deltaR, -0.5) / x * (1 + release_weight * (releaseIndex[m] + releaseIndex[m + 1]));
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        value = 0;
                    fill(rStep, baseX, t0, t1, value, false);
                }
            }

            double[] rSeries = smooth(baseX, rStep, smooth_window_ms, 0.001, false);

            // ---- C: notes within +-500 ms, K: keys in use -------------------------------------------
            var cStep = new double[nBase];
            var kStep = new double[nBase];

            for (int i = 0; i < nBase; i++)
            {
                cStep[i] = lowerBound(heads, baseX[i] + 500) - lowerBound(heads, baseX[i] - 500);
                int keysInUse = 0;
                for (int k = 0; k < keyCount; k++)
                    if (usage[k, i])
                        keysInUse++;
                kStep[i] = Math.Max(keysInUse, 1);
            }

            // ---- combine on the union of corners ---------------------------------------------------
            double[] jAll = interp(corners, baseX, jSeries);
            double[] xAll = interp(corners, baseX, xSeries);
            double[] pAll = interp(corners, baseX, pSeries);
            double[] aAll = interp(corners, aCorners, aSeries);
            double[] rAll = interp(corners, baseX, rSeries);
            double[] cAll = stepInterp(corners, baseX, cStep);
            double[] kAll = stepInterp(corners, baseX, kStep);

            int nCorners = corners.Length;
            var difficulty = new double[nCorners];
            var weight = new double[nCorners];
            double totalWeight = 0;

            for (int i = 0; i < nCorners; i++)
            {
                double aPowK = Math.Pow(aAll[i], 3 / kAll[i]);
                double strain = Math.Pow(
                    w0 * Math.Pow(aPowK * Math.Min(jAll[i], 8 + 0.85 * jAll[i]), 1.5)
                    + (1 - w0) * Math.Pow(Math.Pow(aAll[i], 2.0 / 3) * (0.8 * pAll[i] + rAll[i] * 35 / (cAll[i] + 8)), 1.5),
                    2.0 / 3);
                double twist = aPowK * xAll[i] / (xAll[i] + strain + 1);
                difficulty[i] = w1 * Math.Sqrt(strain) * Math.Pow(twist, p1) + w2 * strain;

                double gap = i == 0 ? (corners[1] - corners[0]) / 2
                    : i == nCorners - 1 ? (corners[nCorners - 1] - corners[nCorners - 2]) / 2
                    : (corners[i + 1] - corners[i - 1]) / 2;
                weight[i] = cAll[i] * gap;
                totalWeight += weight[i];
            }

            result.CornerTimes = corners;
            result.Difficulty = difficulty;
            result.Weight = weight;
            result.Heads = heads;

            var effectivePrefix = new double[count];
            double effective = 0;

            for (int i = 0; i < count; i++)
            {
                effective += 1;
                if (tails[i] >= 0)
                    effective += 0.5 * (Math.Min(tails[i] - heads[i], 1000) / 200.0);
                effectivePrefix[i] = effective;
            }

            result.EffectiveNotesPrefix = effectivePrefix;

            if (totalWeight <= 0)
                return result;

            int[] byDifficulty = Enumerable.Range(0, nCorners).OrderBy(i => difficulty[i]).ToArray();
            var cumulative = new double[nCorners];
            double runningWeight = 0;

            for (int m = 0; m < nCorners; m++)
            {
                runningWeight += weight[byDifficulty[m]];
                cumulative[m] = runningWeight / totalWeight;
            }

            double[] quantiles = { 0.945, 0.935, 0.925, 0.915, 0.845, 0.835, 0.825, 0.815 };
            var picked = new double[quantiles.Length];
            for (int q = 0; q < quantiles.Length; q++)
                picked[q] = difficulty[byDifficulty[Math.Min(lowerBound(cumulative, quantiles[q]), nCorners - 1)]];

            double percentile93 = (picked[0] + picked[1] + picked[2] + picked[3]) / 4;
            double percentile83 = (picked[4] + picked[5] + picked[6] + picked[7]) / 4;

            double powerSum = 0;
            for (int m = 0; m < nCorners; m++)
                powerSum += Math.Pow(difficulty[byDifficulty[m]], norm) * weight[byDifficulty[m]];
            double powerMean = Math.Pow(powerSum / totalWeight, 1 / norm);

            result.StarRating = FinishStarRating(percentile93, percentile83, powerMean, effective);
            result.Jack = weightedPowerMean(jAll, weight, totalWeight);
            result.Cross = weightedPowerMean(xAll, weight, totalWeight);
            result.Press = weightedPowerMean(pAll, weight, totalWeight);
            result.Release = weightedPowerMean(rAll, weight, totalWeight);

            double unevenSum = 0;
            for (int i = 0; i < nCorners; i++)
                unevenSum += aAll[i] * weight[i];
            result.Uneven = unevenSum / totalWeight;

            return result;
        }

        private static double weightedPowerMean(double[] series, double[] weight, double totalWeight)
        {
            double sum = 0;
            for (int i = 0; i < series.Length; i++)
                sum += Math.Pow(Math.Max(series[i], 0), norm) * weight[i];
            return Math.Pow(sum / totalWeight, 1 / norm);
        }

        /// <summary>Indices of two columns merged by head time; on equal heads the first column comes first (a stable sort).</summary>
        private static List<int> mergeByHead(List<int> first, List<int> second, int[] heads)
        {
            var merged = new List<int>(first.Count + second.Count);
            int a = 0, b = 0;

            while (a < first.Count || b < second.Count)
            {
                if (b >= second.Count || (a < first.Count && heads[first[a]] <= heads[second[b]]))
                    merged.Add(first[a++]);
                else
                    merged.Add(second[b++]);
            }

            return merged;
        }

        /// <summary>Writes (or adds) a value onto every corner in [start, end).</summary>
        private static void fill(double[] target, double[] corners, double start, double end, double value, bool add)
        {
            int left = lowerBound(corners, start);
            int right = lowerBound(corners, end);

            for (int i = left; i < right; i++)
                target[i] = add ? target[i] + value : value;
        }

        /// <summary>
        /// Sliding-window integral of f (constant on [x[i], x[i+1])) evaluated at every x; <paramref name="average"/>
        /// divides by the window length inside [x[0], x[^1]] instead of scaling.
        /// </summary>
        private static double[] smooth(double[] x, double[] f, double window, double scale, bool average)
        {
            int n = x.Length;
            var cumulative = new double[n];
            for (int i = 1; i < n; i++)
                cumulative[i] = cumulative[i - 1] + f[i - 1] * (x[i] - x[i - 1]);

            double integralTo(double point)
            {
                double clipped = Math.Clamp(point, x[0], x[n - 1]);
                int index = Math.Clamp(upperBound(x, clipped) - 1, 0, n - 1);
                return cumulative[index] + f[index] * (clipped - x[index]);
            }

            var result = new double[n];

            for (int i = 0; i < n; i++)
            {
                double lower = Math.Max(x[i] - window, x[0]);
                double upper = Math.Min(x[i] + window, x[n - 1]);
                double value = integralTo(upper) - integralTo(lower);

                if (average)
                {
                    double width = upper - lower;
                    result[i] = width > 0 ? value / width : 0;
                }
                else
                    result[i] = scale * value;
            }

            return result;
        }

        /// <summary>Linear interpolation like numpy's interp (clamped to the end values outside the range).</summary>
        private static double[] interp(double[] xs, double[] xp, double[] fp)
        {
            int m = xp.Length;
            var result = new double[xs.Length];

            for (int i = 0; i < xs.Length; i++)
            {
                double v = xs[i];

                if (v <= xp[0])
                    result[i] = fp[0];
                else if (v >= xp[m - 1])
                    result[i] = fp[m - 1];
                else
                {
                    int j = upperBound(xp, v) - 1;
                    double slope = (fp[j + 1] - fp[j]) / (xp[j + 1] - xp[j]);
                    result[i] = slope * (v - xp[j]) + fp[j];
                }
            }

            return result;
        }

        /// <summary>The value of the last old corner at or before each new corner.</summary>
        private static double[] stepInterp(double[] xs, double[] oldX, double[] oldValues)
        {
            var result = new double[xs.Length];
            for (int i = 0; i < xs.Length; i++)
                result[i] = oldValues[Math.Clamp(upperBound(oldX, xs[i]) - 1, 0, oldValues.Length - 1)];
            return result;
        }

        /// <summary>First index whose value is not below <paramref name="value"/> (numpy searchsorted, side left).</summary>
        private static int lowerBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        private static int lowerBound(int[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>First index whose value is above <paramref name="value"/> (numpy searchsorted, side right).</summary>
        private static int upperBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] <= value)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;

namespace osu.Game.Performance
{
    /// <summary>
    /// Banchosucks: the frame times of one thread during one play, kept as a histogram.
    /// </summary>
    /// <remarks>
    /// A histogram instead of a ring buffer of samples: adding a frame is one array increment without allocation, the
    /// memory is fixed (8 kB) however long the map is and however high the frame rate, and nothing from the start of a
    /// long map is overwritten. The median and "how many frames were longer than x" are all the report needs, and both
    /// come out of the buckets to within <see cref="BUCKET_WIDTH"/>; the longest frame is kept exactly.
    /// One thread writes; read it only after that thread has stopped adding.
    /// </remarks>
    public sealed class BanchosucksFrameTimeHistogram
    {
        /// <summary>
        /// Width of one bucket in milliseconds.
        /// </summary>
        public const double BUCKET_WIDTH = 0.25;

        /// <summary>
        /// The last bucket takes every frame of 500 ms and longer.
        /// </summary>
        public const int BUCKET_COUNT = 2001;

        private readonly int[] buckets = new int[BUCKET_COUNT];

        /// <summary>
        /// Number of frames added.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// The longest frame in milliseconds.
        /// </summary>
        public double Longest { get; private set; }

        public void Add(double milliseconds)
        {
            if (!(milliseconds >= 0))
                return;

            int index = milliseconds >= (BUCKET_COUNT - 1) * BUCKET_WIDTH ? BUCKET_COUNT - 1 : (int)(milliseconds / BUCKET_WIDTH);

            buckets[index]++;
            Count++;

            if (milliseconds > Longest)
                Longest = milliseconds;
        }

        public void Reset()
        {
            Array.Clear(buckets);
            Count = 0;
            Longest = 0;
        }

        /// <summary>
        /// The median frame time in milliseconds (centre of the bucket the middle frame fell into), 0 without frames.
        /// </summary>
        public double Median()
        {
            if (Count == 0)
                return 0;

            int middle = (Count + 1) / 2;
            int seen = 0;

            for (int i = 0; i < BUCKET_COUNT; i++)
            {
                seen += buckets[i];

                if (seen >= middle)
                    return (i + 0.5) * BUCKET_WIDTH;
            }

            return Longest;
        }

        /// <summary>
        /// How many frames took longer than <paramref name="milliseconds"/>
        /// (frames in the bucket the limit falls into count as not longer).
        /// </summary>
        public int CountLongerThan(double milliseconds)
        {
            int first = milliseconds >= (BUCKET_COUNT - 1) * BUCKET_WIDTH ? BUCKET_COUNT : (int)(Math.Max(0, milliseconds) / BUCKET_WIDTH) + 1;
            int total = 0;

            for (int i = first; i < BUCKET_COUNT; i++)
                total += buckets[i];

            return total;
        }
    }

    /// <summary>
    /// Banchosucks: the stutters of one thread.
    /// </summary>
    public readonly struct BanchosucksThreadStutters
    {
        public readonly int Frames;
        public readonly double Median;

        /// <summary>
        /// Frames longer than this count as a stutter.
        /// </summary>
        public readonly double Threshold;

        public readonly int Stutters;

        /// <summary>
        /// The longest frame in milliseconds.
        /// </summary>
        public readonly double Worst;

        public BanchosucksThreadStutters(int frames, double median, double threshold, int stutters, double worst)
        {
            Frames = frames;
            Median = median;
            Threshold = threshold;
            Stutters = stutters;
            Worst = worst;
        }
    }

    /// <summary>
    /// Banchosucks: a known cause of stutters that applies to the current settings.
    /// </summary>
    public enum BanchosucksStutterHint
    {
        None,

        /// <summary>
        /// The window lost focus during the play.
        /// </summary>
        WindowInactive,

        /// <summary>
        /// Threading mode "single thread".
        /// </summary>
        SingleThreaded,

        /// <summary>
        /// Frame limiter "unlimited".
        /// </summary>
        UnlimitedFrameLimiter,

        /// <summary>
        /// Input or audio thread rate of <see cref="BanchosucksStutterAnalysis.HIGH_THREAD_RATE"/> Hz or more.
        /// </summary>
        HighThreadRate,

        /// <summary>
        /// The map has a storyboard or video and it is shown.
        /// </summary>
        Storyboard,
    }

    /// <summary>
    /// Banchosucks: the settings and circumstances of a play the hint is derived from.
    /// </summary>
    public readonly struct BanchosucksStutterEnvironment
    {
        public bool WindowWasInactive { get; init; }
        public bool SingleThreaded { get; init; }
        public bool UnlimitedFrameLimiter { get; init; }
        public int InputRateHz { get; init; }
        public int AudioRateHz { get; init; }

        /// <summary>
        /// Storyboards are enabled and this map has one (videos are part of the storyboard).
        /// </summary>
        public bool StoryboardShown { get; init; }
    }

    /// <summary>
    /// Banchosucks: what is shown after the map.
    /// </summary>
    public sealed class BanchosucksStutterReport
    {
        public BanchosucksThreadStutters Update { get; init; }
        public BanchosucksThreadStutters Draw { get; init; }

        /// <summary>
        /// Number of stutters: the larger of the two threads (a long update frame usually delays a draw frame too, adding both would count it twice).
        /// </summary>
        public int Stutters { get; init; }

        /// <summary>
        /// The longest frame of either thread in milliseconds.
        /// </summary>
        public double Worst { get; init; }

        public BanchosucksStutterHint Hint { get; init; }

        /// <summary>
        /// For <see cref="BanchosucksStutterHint.HighThreadRate"/>: the higher of the two rates.
        /// </summary>
        public int HintRateHz { get; init; }
    }

    /// <summary>
    /// Banchosucks: turns the recorded frame times of a play into the stutter report. No drawables, no settings access.
    /// </summary>
    public static class BanchosucksStutterAnalysis
    {
        /// <summary>
        /// A frame shorter than this is never a stutter, whatever the median (25 ms = a dropped frame even at 40 fps).
        /// </summary>
        public const double MIN_STUTTER_MS = 25;

        /// <summary>
        /// A stutter is also longer than this many times the thread's median frame time.
        /// </summary>
        public const double MEDIAN_FACTOR = 3;

        /// <summary>
        /// Fewer stutters than this are not reported...
        /// </summary>
        public const int MIN_STUTTERS = 3;

        /// <summary>
        /// ...unless one of them was at least this long.
        /// </summary>
        public const double SINGLE_STUTTER_MS = 100;

        /// <summary>
        /// A thread with fewer frames than this is ignored (the play was over before it began).
        /// </summary>
        public const int MIN_FRAMES = 120;

        /// <summary>
        /// Input / audio rates from here on get the <see cref="BanchosucksStutterHint.HighThreadRate"/> hint.
        /// </summary>
        public const int HIGH_THREAD_RATE = 4000;

        public static BanchosucksThreadStutters Analyse(BanchosucksFrameTimeHistogram frames)
        {
            if (frames.Count < MIN_FRAMES)
                return new BanchosucksThreadStutters(frames.Count, 0, 0, 0, 0);

            double median = frames.Median();
            double threshold = Math.Max(MIN_STUTTER_MS, MEDIAN_FACTOR * median);
            int stutters = frames.CountLongerThan(threshold);

            return new BanchosucksThreadStutters(frames.Count, median, threshold, stutters, frames.Longest);
        }

        /// <summary>
        /// The report for a play, or null when there were no noticeable stutters.
        /// </summary>
        public static BanchosucksStutterReport? CreateReport(BanchosucksFrameTimeHistogram updateFrames, BanchosucksFrameTimeHistogram drawFrames, BanchosucksStutterEnvironment environment)
        {
            var update = Analyse(updateFrames);
            var draw = Analyse(drawFrames);

            int stutters = Math.Max(update.Stutters, draw.Stutters);

            if (stutters == 0)
                return null;

            double worst = Math.Max(update.Stutters > 0 ? update.Worst : 0, draw.Stutters > 0 ? draw.Worst : 0);

            if (stutters < MIN_STUTTERS && worst < SINGLE_STUTTER_MS)
                return null;

            return new BanchosucksStutterReport
            {
                Update = update,
                Draw = draw,
                Stutters = stutters,
                Worst = worst,
                Hint = PickHint(environment),
                HintRateHz = Math.Max(environment.InputRateHz, environment.AudioRateHz),
            };
        }

        /// <summary>
        /// The most likely known cause, most direct first.
        /// </summary>
        public static BanchosucksStutterHint PickHint(BanchosucksStutterEnvironment environment)
        {
            if (environment.WindowWasInactive)
                return BanchosucksStutterHint.WindowInactive;

            if (environment.SingleThreaded)
                return BanchosucksStutterHint.SingleThreaded;

            if (environment.UnlimitedFrameLimiter)
                return BanchosucksStutterHint.UnlimitedFrameLimiter;

            if (Math.Max(environment.InputRateHz, environment.AudioRateHz) >= HIGH_THREAD_RATE)
                return BanchosucksStutterHint.HighThreadRate;

            if (environment.StoryboardShown)
                return BanchosucksStutterHint.Storyboard;

            return BanchosucksStutterHint.None;
        }

        /// <summary>
        /// The text of the notification, in German or English.
        /// </summary>
        public static string Describe(BanchosucksStutterReport report, bool german)
        {
            string worst = Math.Round(report.Worst).ToString(CultureInfo.InvariantCulture);

            string line = german
                ? $"Ruckler-Bericht: {report.Stutters} Ruckler in dieser Map, längster Frame {worst} ms."
                : $"Stutter report: {report.Stutters} {(report.Stutters == 1 ? "stutter" : "stutters")} in this map, longest frame {worst} ms.";

            string hint = describeHint(report, german);

            return string.IsNullOrEmpty(hint) ? line : $"{line} {hint}";
        }

        private static string describeHint(BanchosucksStutterReport report, bool german)
        {
            switch (report.Hint)
            {
                case BanchosucksStutterHint.WindowInactive:
                    return german
                        ? "Das Fenster war zwischendurch nicht im Vordergrund."
                        : "The window was not in the foreground for part of the play.";

                case BanchosucksStutterHint.SingleThreaded:
                    return german
                        ? "Der Threading-Modus steht auf Single-Thread, Multithreading läuft meist flüssiger. Klicken öffnet die Einstellung."
                        : "The threading mode is single thread, multithreaded usually runs smoother. Click to open the setting.";

                case BanchosucksStutterHint.UnlimitedFrameLimiter:
                    return german
                        ? "Der Frame-Limiter steht auf Unbegrenzt, ein fester Wert läuft meist gleichmäßiger. Klicken öffnet die Einstellung."
                        : "The frame limiter is unlimited, a fixed limit usually runs more evenly. Click to open the setting.";

                case BanchosucksStutterHint.HighThreadRate:
                    return german
                        ? $"Eingabe- oder Audio-Rate steht auf {report.HintRateHz} Hz, das kostet viel CPU (Standard: 1000 Hz). Klicken öffnet die Einstellung."
                        : $"The input or audio rate is {report.HintRateHz} Hz, which costs a lot of CPU (default: 1000 Hz). Click to open the setting.";

                case BanchosucksStutterHint.Storyboard:
                    return german
                        ? "Storyboard oder Video dieser Map war an. Klicken öffnet die Einstellung."
                        : "This map's storyboard or video was on. Click to open the setting.";

                default:
                    return string.Empty;
            }
        }
    }
}

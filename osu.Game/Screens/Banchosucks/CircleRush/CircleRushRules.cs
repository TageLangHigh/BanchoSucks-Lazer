// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// How a run ends: after one minute, or after five misses.
    /// </summary>
    public enum CircleRushMode
    {
        Minute = 0,
        Endless = 1,
    }

    /// <summary>
    /// Circle size and how long a circle waits before it counts as a miss (osu! AR 8 / 9 / 10 / 11 at the start).
    /// </summary>
    public enum CircleRushDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
        Extreme = 3,
    }

    /// <summary>
    /// The four mods Circle Rush knows. Easy and HardRock exclude each other, like in osu!.
    /// </summary>
    [Flags]
    public enum CircleRushMods
    {
        None = 0,
        Easy = 1,
        Hidden = 2,
        HardRock = 4,
        Flashlight = 8,
    }

    /// <summary>How quickly a circle was hit, as a share of its lifetime.</summary>
    public enum CircleRushJudgement
    {
        Perfect,
        Good,
        Late,
    }

    /// <summary>
    /// Milliseconds and playfield pixels of one difficulty, before mods and before the speed ramp.
    /// </summary>
    public readonly record struct CircleRushDifficultyParams(int StartLifetime, int FloorLifetime, int StartInterval, int FloorInterval, int Radius);

    /// <summary>When a circle appears and where it sits in its group (singles, bursts of two, streams of three to five).</summary>
    public readonly record struct CircleRushCircleInfo(int Spawn, int GroupSize, int GroupIndex);

    /// <summary>
    /// Banchosucks: the rules of Circle Rush. Everything here is integer arithmetic so that the server plugin
    /// (<c>banchosucks_circlerush/rules.py</c>) recomputes a run from its event log and arrives at the same score.
    /// </summary>
    /// <remarks>
    /// A run is a schedule of circles drawn from the run's seed: singles, bursts of two and streams of three to five.
    /// Inside a group the gap is a fraction of the base interval, after a group a full one; base interval and
    /// lifetime shrink with the run time (the ramp) and, in endless mode, keep shrinking slowly afterwards so every run
    /// ends. A hit scores more the faster it came and the longer the combo; a miss (an expired circle or a click into
    /// nothing) resets the combo and, in endless mode, costs one of <see cref="CircleRushRules.ENDLESS_LIVES"/> lives.
    /// </remarks>
    public static class CircleRushRules
    {
        public const int MINUTE_DURATION = 60_000;
        public const int ENDLESS_LIVES = 5;

        /// <summary>A hit faster than this after the circle appeared is not a human reaction; the server rejects such runs.</summary>
        public const int MIN_REACTION = 50;

        public const int RAMP_MINUTE = 40_000;
        public const int RAMP_ENDLESS = 75_000;

        /// <summary>Endless mode after the ramp: lifetime and base interval lose one millisecond per this many milliseconds.</summary>
        public const int SQUEEZE_DIVISOR = 250;

        public const int MIN_LIFETIME = 100;
        public const int MIN_SPAWN = 90;

        /// <summary>Gap inside a burst / a stream, in percent of the base interval.</summary>
        public const int BURST_PERCENT = 60;
        public const int STREAM_PERCENT = 45;

        /// <summary>A hit within this share of the lifetime is perfect, within the next one good, later than that late.</summary>
        public const int PERFECT_PERCENT = 35;
        public const int GOOD_PERCENT = 70;

        public const int PLAYFIELD_WIDTH = 800;
        public const int PLAYFIELD_HEIGHT = 600;

        public const int MAX_COMBO_BONUS = 100;

        /// <summary>Score multipliers in percent, applied in this order: EZ, HD, HR, FL.</summary>
        public const int EASY_PERCENT = 120;
        public const int HIDDEN_PERCENT = 106;
        public const int HARDROCK_PERCENT = 106;
        public const int FLASHLIGHT_PERCENT = 112;

        public static CircleRushDifficultyParams Params(CircleRushDifficulty difficulty) => difficulty switch
        {
            CircleRushDifficulty.Easy => new CircleRushDifficultyParams(850, 550, 800, 380, 50),
            CircleRushDifficulty.Medium => new CircleRushDifficultyParams(700, 460, 650, 300, 42),
            CircleRushDifficulty.Hard => new CircleRushDifficultyParams(550, 380, 520, 240, 34),
            _ => new CircleRushDifficultyParams(400, 300, 400, 180, 28),
        };

        public static bool ModsValid(CircleRushMods mods) => (mods & (CircleRushMods.Easy | CircleRushMods.HardRock)) != (CircleRushMods.Easy | CircleRushMods.HardRock);

        public static string ModeKey(CircleRushMode mode) => mode == CircleRushMode.Minute ? "minute" : "endless";

        public static string DifficultyKey(CircleRushDifficulty difficulty) => difficulty switch
        {
            CircleRushDifficulty.Easy => "easy",
            CircleRushDifficulty.Medium => "medium",
            CircleRushDifficulty.Hard => "hard",
            _ => "extreme",
        };

        /// <summary>"HDHR", "EZ" or an empty string.</summary>
        public static string ModsAcronyms(CircleRushMods mods)
        {
            string text = string.Empty;
            if (mods.HasFlag(CircleRushMods.Easy)) text += "EZ";
            if (mods.HasFlag(CircleRushMods.Hidden)) text += "HD";
            if (mods.HasFlag(CircleRushMods.HardRock)) text += "HR";
            if (mods.HasFlag(CircleRushMods.Flashlight)) text += "FL";
            return text;
        }

        /// <summary>The combined score multiplier, for display (the score itself uses <see cref="ApplyMods"/>).</summary>
        public static double ModMultiplier(CircleRushMods mods)
        {
            double value = 1;
            if (mods.HasFlag(CircleRushMods.Easy)) value *= EASY_PERCENT / 100.0;
            if (mods.HasFlag(CircleRushMods.Hidden)) value *= HIDDEN_PERCENT / 100.0;
            if (mods.HasFlag(CircleRushMods.HardRock)) value *= HARDROCK_PERCENT / 100.0;
            if (mods.HasFlag(CircleRushMods.Flashlight)) value *= FLASHLIGHT_PERCENT / 100.0;
            return value;
        }

        private static int ramp(CircleRushMode mode) => mode == CircleRushMode.Minute ? RAMP_MINUTE : RAMP_ENDLESS;

        private static int lerp(int start, int floor, int time, int rampMs)
        {
            if (time <= 0) return start;
            if (time >= rampMs) return floor;

            return start - (int)((long)(start - floor) * time / rampMs);
        }

        private static int squeeze(CircleRushMode mode, int value, int floor, int time)
        {
            if (mode != CircleRushMode.Endless || time <= RAMP_ENDLESS)
                return value;

            return Math.Max(floor / 2, floor - (time - RAMP_ENDLESS) / SQUEEZE_DIVISOR);
        }

        /// <summary>Milliseconds a circle that appears at run time <paramref name="time"/> waits before it counts as a miss.</summary>
        public static int Lifetime(CircleRushMode mode, CircleRushDifficulty difficulty, CircleRushMods mods, int time)
        {
            var p = Params(difficulty);
            int start = p.StartLifetime, floor = p.FloorLifetime;

            if (mods.HasFlag(CircleRushMods.Easy))
            {
                start = start * 160 / 100;
                floor = floor * 160 / 100;
            }

            if (mods.HasFlag(CircleRushMods.HardRock))
            {
                start = start * 75 / 100;
                floor = floor * 75 / 100;
            }

            int value = squeeze(mode, lerp(start, floor, time, ramp(mode)), floor, time);
            return Math.Max(MIN_LIFETIME, value);
        }

        /// <summary>Milliseconds after the last circle of a group until the next group; inside a group a fraction of this.</summary>
        public static int BaseInterval(CircleRushMode mode, CircleRushDifficulty difficulty, CircleRushMods mods, int time)
        {
            var p = Params(difficulty);
            int start = p.StartInterval, floor = p.FloorInterval;

            if (mods.HasFlag(CircleRushMods.Easy))
            {
                start = start * 130 / 100;
                floor = floor * 130 / 100;
            }

            if (mods.HasFlag(CircleRushMods.HardRock))
            {
                start = start * 85 / 100;
                floor = floor * 85 / 100;
            }

            int value = squeeze(mode, lerp(start, floor, time, ramp(mode)), floor, time);
            return Math.Max(MIN_SPAWN, value);
        }

        /// <summary>Circle radius in playfield pixels (<see cref="PLAYFIELD_WIDTH"/> × <see cref="PLAYFIELD_HEIGHT"/>).</summary>
        public static int Radius(CircleRushDifficulty difficulty, CircleRushMods mods)
        {
            int radius = Params(difficulty).Radius;
            if (mods.HasFlag(CircleRushMods.Easy)) radius = radius * 140 / 100;
            if (mods.HasFlag(CircleRushMods.HardRock)) radius = radius * 80 / 100;
            return radius;
        }

        /// <summary>
        /// Points for one hit: 100 to 200 depending on how early it came, times a combo multiplier
        /// that grows from 1.00 to 3.00 over the first <see cref="MAX_COMBO_BONUS"/> consecutive hits.
        /// </summary>
        public static int HitScore(int reaction, int lifetime, int comboBefore)
        {
            int clamped = Math.Clamp(reaction, 0, lifetime);
            int basePoints = 100 + 100 * (lifetime - clamped) / lifetime;
            int combo = Math.Min(Math.Max(comboBefore, 0), MAX_COMBO_BONUS);
            return basePoints * (100 + 2 * combo) / 100;
        }

        public static CircleRushJudgement Judgement(int reaction, int lifetime)
        {
            if (reaction * 100 <= lifetime * PERFECT_PERCENT) return CircleRushJudgement.Perfect;
            if (reaction * 100 <= lifetime * GOOD_PERCENT) return CircleRushJudgement.Good;

            return CircleRushJudgement.Late;
        }

        /// <summary>The mod multipliers, applied one after the other with integer rounding down.</summary>
        public static int ApplyMods(int total, CircleRushMods mods)
        {
            long value = total;
            if (mods.HasFlag(CircleRushMods.Easy)) value = value * EASY_PERCENT / 100;
            if (mods.HasFlag(CircleRushMods.Hidden)) value = value * HIDDEN_PERCENT / 100;
            if (mods.HasFlag(CircleRushMods.HardRock)) value = value * HARDROCK_PERCENT / 100;
            if (mods.HasFlag(CircleRushMods.Flashlight)) value = value * FLASHLIGHT_PERCENT / 100;
            return (int)value;
        }
    }

    /// <summary>
    /// xorshift32, identical on the server: the only randomness that shapes the schedule (which groups come when).
    /// </summary>
    public sealed class CircleRushXorshift
    {
        private uint state;

        public CircleRushXorshift(long seed)
        {
            state = (uint)(seed & 0xffffffff);
            if (state == 0) state = 1;
        }

        public uint Next()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }
    }

    /// <summary>
    /// When each circle of a run appears, which group it belongs to and when it expires; generated group by group and cached.
    /// </summary>
    public class CircleRushSchedule
    {
        public readonly CircleRushMode Mode;
        public readonly CircleRushDifficulty Difficulty;
        public readonly CircleRushMods Mods;
        public readonly long Seed;

        private readonly CircleRushXorshift rng;
        private readonly List<CircleRushCircleInfo> circles = new List<CircleRushCircleInfo>();
        private int nextSpawn;

        public CircleRushSchedule(CircleRushMode mode, CircleRushDifficulty difficulty, CircleRushMods mods, long seed)
        {
            Mode = mode;
            Difficulty = difficulty;
            Mods = mods;
            Seed = seed;
            rng = new CircleRushXorshift(seed);
        }

        private void generateGroup()
        {
            uint roll = rng.Next() % 100;
            int count, factor;

            if (roll < 55)
            {
                count = 1;
                factor = 100;
            }
            else if (roll < 85)
            {
                count = 2;
                factor = CircleRushRules.BURST_PERCENT;
            }
            else
            {
                count = 3 + (int)(rng.Next() % 3);
                factor = CircleRushRules.STREAM_PERCENT;
            }

            for (int position = 0; position < count; position++)
            {
                int spawn = nextSpawn;
                circles.Add(new CircleRushCircleInfo(spawn, count, position));
                int baseInterval = CircleRushRules.BaseInterval(Mode, Difficulty, Mods, spawn);
                int gap = position == count - 1 ? baseInterval : Math.Max(CircleRushRules.MIN_SPAWN, baseInterval * factor / 100);
                nextSpawn = spawn + gap;
            }
        }

        public CircleRushCircleInfo Circle(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            while (circles.Count <= index)
                generateGroup();

            return circles[index];
        }

        public int SpawnTime(int index) => Circle(index).Spawn;

        public int Lifetime(int index) => CircleRushRules.Lifetime(Mode, Difficulty, Mods, SpawnTime(index));

        public int Deadline(int index) => SpawnTime(index) + Lifetime(index);

        /// <summary>How many circles have appeared by run time <paramref name="time"/> (inclusive).</summary>
        public int SpawnedCount(int time)
        {
            if (time < 0) return 0;

            int count = 0;
            while (SpawnTime(count) <= time) count++;
            return count;
        }
    }

    /// <summary>
    /// One entry of the event log: a hit (kind 1) with its reaction time, an expired circle (kind 2) or a click into nothing (kind 3).
    /// </summary>
    public readonly record struct CircleRushEvent(int Kind, int Index, int Time, int Reaction)
    {
        public const int HIT = 1;
        public const int EXPIRED = 2;
        public const int EMPTY_CLICK = 3;
    }

    /// <summary>
    /// The state of one run: score, combo, lives and the event log the server verifies.
    /// </summary>
    public class CircleRushRun
    {
        public readonly CircleRushSchedule Schedule;

        public long Seed => Schedule.Seed;
        public CircleRushMode Mode => Schedule.Mode;
        public CircleRushDifficulty Difficulty => Schedule.Difficulty;
        public CircleRushMods Mods => Schedule.Mods;

        /// <summary>Points before the mod multipliers.</summary>
        public int BaseScore { get; private set; }

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Late { get; private set; }
        public long ReactionSum { get; private set; }
        public bool Ended { get; private set; }
        public int DurationMs { get; private set; }

        public readonly List<CircleRushEvent> Events = new List<CircleRushEvent>();

        private readonly HashSet<int> resolved = new HashSet<int>();

        public CircleRushRun(CircleRushMode mode, CircleRushDifficulty difficulty, CircleRushMods mods, long seed)
        {
            Schedule = new CircleRushSchedule(mode, difficulty, mods, seed);
        }

        public int Score => CircleRushRules.ApplyMods(BaseScore, Mods);

        public int LivesLeft => Mode == CircleRushMode.Endless ? Math.Max(0, CircleRushRules.ENDLESS_LIVES - Misses) : int.MaxValue;

        public int AverageReaction => Hits > 0 ? (int)(ReactionSum / Hits) : 0;

        /// <summary>Whether circle <paramref name="index"/> was hit or has expired already.</summary>
        public bool IsResolved(int index) => resolved.Contains(index);

        /// <summary>Points a hit on circle <paramref name="index"/> at run time <paramref name="time"/> would give now.</summary>
        public int HitScoreAt(int index, int time) => CircleRushRules.HitScore(time - Schedule.SpawnTime(index), Schedule.Lifetime(index), Combo);

        /// <summary>Registers a hit; null when the circle is not on the field at that time or was resolved before.</summary>
        public CircleRushJudgement? Hit(int index, int time)
        {
            if (Ended || resolved.Contains(index)) return null;
            if (time < Schedule.SpawnTime(index) || time > Schedule.Deadline(index)) return null;

            int reaction = time - Schedule.SpawnTime(index);
            int lifetime = Schedule.Lifetime(index);
            BaseScore += CircleRushRules.HitScore(reaction, lifetime, Combo);
            Combo++;
            MaxCombo = Math.Max(MaxCombo, Combo);
            Hits++;
            ReactionSum += reaction;
            resolved.Add(index);
            Events.Add(new CircleRushEvent(CircleRushEvent.HIT, index, time, reaction));

            var judgement = CircleRushRules.Judgement(reaction, lifetime);

            switch (judgement)
            {
                case CircleRushJudgement.Perfect:
                    Perfect++;
                    break;

                case CircleRushJudgement.Good:
                    Good++;
                    break;

                default:
                    Late++;
                    break;
            }

            return judgement;
        }

        /// <summary>Circle <paramref name="index"/> waited too long: a miss at exactly its deadline.</summary>
        public bool Expire(int index)
        {
            if (Ended || resolved.Contains(index)) return false;

            resolved.Add(index);
            int time = Schedule.Deadline(index);
            Events.Add(new CircleRushEvent(CircleRushEvent.EXPIRED, index, time, 0));
            miss(time);
            return true;
        }

        /// <summary>A click that hit no circle.</summary>
        public bool EmptyClick(int time)
        {
            if (Ended) return false;

            Events.Add(new CircleRushEvent(CircleRushEvent.EMPTY_CLICK, -1, time, 0));
            miss(time);
            return true;
        }

        /// <summary>Minute mode: the clock ran out.</summary>
        public void FinishMinute()
        {
            if (Ended || Mode != CircleRushMode.Minute) return;

            end(CircleRushRules.MINUTE_DURATION);
        }

        /// <summary>The player left early; the run is over and never submitted.</summary>
        public void Abort(int time)
        {
            if (!Ended) end(time);
        }

        private void miss(int time)
        {
            Combo = 0;
            Misses++;

            if (Mode == CircleRushMode.Endless && Misses >= CircleRushRules.ENDLESS_LIVES)
                end(time);
        }

        private void end(int time)
        {
            Ended = true;
            DurationMs = time;
        }
    }
}

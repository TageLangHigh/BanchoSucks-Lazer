// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using osu.Game.Online.API;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: answer of <c>GET /api/plugins/banchosucks_motw/current</c>.
    /// </summary>
    public class BanchosucksMapOfTheWeekResponse
    {
        /// <summary>The current map of the week, or null between two weeks or when none was picked.</summary>
        [JsonProperty("current")]
        public BanchosucksMapOfTheWeek? Current { get; set; }

        /// <summary>Maps nominated for next week, oldest nomination first.</summary>
        [JsonProperty("nominations")]
        public List<BanchosucksMapNomination> Nominations { get; set; } = new List<BanchosucksMapNomination>();
    }

    public class BanchosucksMapOfTheWeek
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonProperty("beatmapset_id")]
        public int BeatmapSetId { get; set; }

        /// <summary>The seven day playlist room BanchoBot opened for the map.</summary>
        [JsonProperty("room_id")]
        public long RoomId { get; set; }

        /// <summary>"Artist - Title [Difficulty]".</summary>
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("stars")]
        public double Stars { get; set; }

        /// <summary>Name of the player who nominated it, or null when it is the most played map of the week.</summary>
        [JsonProperty("nominated_by")]
        public string? NominatedBy { get; set; }

        [JsonProperty("votes")]
        public int Votes { get; set; }

        /// <summary>ISO 8601; see <see cref="BanchosucksWeekly.ParseTime"/>.</summary>
        [JsonProperty("ends_at")]
        public string? EndsAt { get; set; }
    }

    public class BanchosucksMapNomination
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("stars")]
        public double? Stars { get; set; }

        [JsonProperty("by")]
        public string? By { get; set; }

        [JsonProperty("votes")]
        public int Votes { get; set; }
    }

    /// <summary>
    /// Banchosucks: answer of <c>GET /api/plugins/banchosucks_goal/current</c>.
    /// </summary>
    public class BanchosucksCommunityGoalResponse
    {
        /// <summary>The goal of the running week, or null until the plugin has started one.</summary>
        [JsonProperty("goal")]
        public BanchosucksCommunityGoal? Goal { get; set; }
    }

    /// <summary>
    /// Passed plays on stable and lazer together, Monday 00:00 UTC to Sunday. The plugin refreshes it every ten minutes.
    /// </summary>
    public class BanchosucksCommunityGoal
    {
        [JsonProperty("target")]
        public int Target { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("stable")]
        public int Stable { get; set; }

        [JsonProperty("lazer")]
        public int Lazer { get; set; }

        [JsonProperty("participants")]
        public int Participants { get; set; }

        /// <summary>ISO 8601 time the target was reached, or null.</summary>
        [JsonProperty("reached_at")]
        public string? ReachedAt { get; set; }

        [JsonProperty("ends_at")]
        public string? EndsAt { get; set; }

        [JsonProperty("updated_at")]
        public string? UpdatedAt { get; set; }
    }

    public class GetBanchosucksMapOfTheWeekRequest : APIRequest<BanchosucksMapOfTheWeekResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => @"banchosucks_motw/current";
    }

    public class GetBanchosucksCommunityGoalRequest : APIRequest<BanchosucksCommunityGoalResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => @"banchosucks_goal/current";
    }

    /// <summary>
    /// Banchosucks: the arithmetic of the weekly tile. Pure.
    /// </summary>
    public static class BanchosucksWeekly
    {
        /// <summary>
        /// Reads a time as the plugins write it (Python <c>isoformat()</c>). A time without an offset is UTC: the server works in UTC throughout.
        /// </summary>
        public static DateTimeOffset? ParseTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time) ? time : null;
        }

        /// <summary>
        /// Time left until <paramref name="endsAt"/>, zero when it has passed, null when the server gave no (readable) end.
        /// </summary>
        public static TimeSpan? Remaining(string? endsAt, DateTimeOffset now)
        {
            if (ParseTime(endsAt) is not DateTimeOffset end)
                return null;

            return end > now ? end - now : TimeSpan.Zero;
        }

        /// <summary>
        /// Whether a map of the week is still running. The plugin already withholds an ended one; this covers the time between two answers.
        /// </summary>
        public static bool IsRunning(BanchosucksMapOfTheWeek? map, DateTimeOffset now)
            => map != null && map.BeatmapId > 0 && Remaining(map.EndsAt, now) != TimeSpan.Zero;

        /// <summary>
        /// Progress of the goal from 0 to 1 for the bar.
        /// </summary>
        public static double Fraction(BanchosucksCommunityGoal? goal)
        {
            if (goal == null || goal.Target <= 0)
                return 0;

            return Math.Clamp((double)Math.Max(0, goal.Total) / goal.Target, 0, 1);
        }

        /// <summary>
        /// Whole percent the way the server's <c>!goal</c> says it: rounded down, 100 at most.
        /// </summary>
        public static int Percent(BanchosucksCommunityGoal? goal)
        {
            if (goal == null || goal.Target <= 0)
                return 0;

            return (int)Math.Min(100, Math.Max(0, (long)goal.Total) * 100 / goal.Target);
        }

        public static bool Reached(BanchosucksCommunityGoal? goal) => goal != null && (!string.IsNullOrEmpty(goal.ReachedAt) || (goal.Target > 0 && goal.Total >= goal.Target));

        /// <summary>
        /// "3d 4h", "5h 12min", "12min" (German: "3 T 4 Std", "5 Std 12 Min", "12 Min"); less than a minute counts as one.
        /// </summary>
        public static string FormatRemaining(TimeSpan left, bool german)
        {
            if (left <= TimeSpan.Zero)
                return german ? "vorbei" : "over";

            int days = (int)left.TotalDays;
            int hours = left.Hours;
            int minutes = Math.Max(1, left.Minutes);

            if (days > 0)
                return german ? $"{days} T {hours} Std" : $"{days}d {hours}h";

            if (hours > 0)
                return german ? $"{hours} Std {left.Minutes} Min" : $"{hours}h {left.Minutes}min";

            return german ? $"{minutes} Min" : $"{minutes}min";
        }
    }
}

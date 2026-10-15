// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// A finished run as the lazer server plugin <c>banchosucks_circlerush</c> expects it (POST /submit).
    /// The server replays the event log and only keeps the run when it arrives at the same score.
    /// </summary>
    public class CircleRushSubmission
    {
        [JsonProperty("mode")] public string Mode { get; set; } = "minute";
        [JsonProperty("difficulty")] public string Difficulty { get; set; } = "medium";
        [JsonProperty("mods")] public int Mods { get; set; }
        [JsonProperty("seed")] public long Seed { get; set; }
        [JsonProperty("duration_ms")] public int DurationMs { get; set; }
        [JsonProperty("score")] public int Score { get; set; }
        [JsonProperty("hits")] public int Hits { get; set; }
        [JsonProperty("misses")] public int Misses { get; set; }
        [JsonProperty("max_combo")] public int MaxCombo { get; set; }
        [JsonProperty("avg_reaction_ms")] public int AverageReactionMs { get; set; }

        /// <summary>[kind, circle index, run time ms, reaction ms] per event, in order.</summary>
        [JsonProperty("events")] public List<int[]> Events { get; set; } = new List<int[]>();

        public static CircleRushSubmission FromRun(CircleRushRun run) => new CircleRushSubmission
        {
            Mode = CircleRushRules.ModeKey(run.Mode),
            Difficulty = CircleRushRules.DifficultyKey(run.Difficulty),
            Mods = (int)run.Mods,
            Seed = run.Seed,
            DurationMs = run.DurationMs,
            Score = run.Score,
            Hits = run.Hits,
            Misses = run.Misses,
            MaxCombo = run.MaxCombo,
            AverageReactionMs = run.AverageReaction,
            Events = run.Events.Select(e => new[] { e.Kind, e.Index, e.Time, e.Reaction }).ToList(),
        };
    }

    public class CircleRushSubmitResponse
    {
        [JsonProperty("accepted")] public bool Accepted { get; set; }
        [JsonProperty("reason")] public string? Reason { get; set; }
        [JsonProperty("rank")] public int? Rank { get; set; }
        [JsonProperty("best_score")] public int BestScore { get; set; }
        [JsonProperty("personal_best")] public bool PersonalBest { get; set; }
    }

    public class CircleRushLeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank { get; set; }
        [JsonProperty("user_id")] public long UserId { get; set; }
        [JsonProperty("username")] public string Username { get; set; } = string.Empty;
        [JsonProperty("country_code")] public string? CountryCode { get; set; }
        [JsonProperty("avatar_url")] public string? AvatarUrl { get; set; }
        [JsonProperty("score")] public int Score { get; set; }
        [JsonProperty("mods")] public int Mods { get; set; }
        [JsonProperty("hits")] public int Hits { get; set; }
        [JsonProperty("misses")] public int Misses { get; set; }
        [JsonProperty("max_combo")] public int MaxCombo { get; set; }
        [JsonProperty("duration_ms")] public int DurationMs { get; set; }
        [JsonProperty("avg_reaction_ms")] public int AverageReactionMs { get; set; }
        [JsonProperty("runs")] public int Runs { get; set; }
    }

    public class CircleRushLeaderboardResponse
    {
        [JsonProperty("mode")] public string Mode { get; set; } = "minute";
        [JsonProperty("difficulty")] public string Difficulty { get; set; } = "medium";
        [JsonProperty("total")] public int Total { get; set; }
        [JsonProperty("entries")] public List<CircleRushLeaderboardEntry> Entries { get; set; } = new List<CircleRushLeaderboardEntry>();
        [JsonProperty("own")] public CircleRushLeaderboardEntry? Own { get; set; }
    }

    public class CircleRushBestResponse
    {
        /// <summary>Keyed "mode:difficulty", e.g. "minute:hard".</summary>
        [JsonProperty("best")] public Dictionary<string, CircleRushLeaderboardEntry> Best { get; set; } = new Dictionary<string, CircleRushLeaderboardEntry>();
    }

    /// <summary>
    /// Banchosucks: sends a finished Circle Rush run to the lazer server plugin.
    /// </summary>
    public class SubmitCircleRushRequest : APIRequest<CircleRushSubmitResponse>
    {
        public readonly CircleRushSubmission Submission;

        public SubmitCircleRushRequest(CircleRushSubmission submission)
        {
            Submission = submission;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_circlerush/submit";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.ContentType = "application/json";
            req.AddRaw(JsonConvert.SerializeObject(Submission));
            return req;
        }
    }

    /// <summary>
    /// Banchosucks: Circle Rush leaderboard of one mode and difficulty (top 50 plus the player's own row).
    /// </summary>
    public class GetCircleRushLeaderboardRequest : APIRequest<CircleRushLeaderboardResponse>
    {
        public readonly string Mode;
        public readonly string Difficulty;

        public GetCircleRushLeaderboardRequest(CircleRushMode mode, CircleRushDifficulty difficulty)
        {
            Mode = CircleRushRules.ModeKey(mode);
            Difficulty = CircleRushRules.DifficultyKey(difficulty);
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_circlerush/leaderboard";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.AddParameter("mode", Mode);
            req.AddParameter("difficulty", Difficulty);
            req.AddParameter("limit", "50");
            return req;
        }
    }

    /// <summary>
    /// Banchosucks: the player's best Circle Rush runs on every board.
    /// </summary>
    public class GetCircleRushBestRequest : APIRequest<CircleRushBestResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => "banchosucks_circlerush/best";
    }
}

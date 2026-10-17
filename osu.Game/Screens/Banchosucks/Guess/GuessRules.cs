// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Screens.Banchosucks.Guess
{
    /// <summary>
    /// Banchosucks: scoring of the guessing games, mirrored by the plugin's <c>banchosucks_guess/rules.py</c>.
    /// </summary>
    public static class GuessRules
    {
        public const int ROUNDS = 10;
        public const int ROUND_MS = 15_000;
        public const int BASE_POINTS = 100;
        public const int POINTS_PER_SECOND = 10;
        public const int STREAK_BONUS = 10;
        public const int STREAK_BONUS_CAP = 50;
        public const int MAX_ROUND_POINTS = BASE_POINTS + ROUND_MS / 1000 * POINTS_PER_SECOND + STREAK_BONUS_CAP;
        public const int MAX_SCORE = ROUNDS * MAX_ROUND_POINTS;
        public const int MIN_ANSWER_MS = 300;

        /// <summary>Points for a correct answer with <paramref name="remainingMs"/> left; <paramref name="streak"/> counts this answer too.</summary>
        public static int RoundPoints(int remainingMs, int streak)
        {
            int remaining = Math.Clamp(remainingMs, 0, ROUND_MS);
            int bonus = Math.Min(STREAK_BONUS_CAP, STREAK_BONUS * Math.Max(0, streak - 1));
            return BASE_POINTS + remaining / 1000 * POINTS_PER_SECOND + bonus;
        }
    }

    /// <summary>One finished run of a guessing game.</summary>
    public class GuessRun
    {
        public int Score { get; private set; }
        public int Correct { get; private set; }
        public int Rounds { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        private long answerMsSum;

        public int AverageAnswerMs => Correct > 0 ? (int)(answerMsSum / Correct) : 0;

        public bool Finished => Rounds >= GuessRules.ROUNDS;

        /// <summary>Records an answer; returns the points it gave.</summary>
        public int Answer(bool correct, int elapsedMs)
        {
            Rounds++;

            if (!correct)
            {
                Streak = 0;
                return 0;
            }

            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
            Correct++;
            answerMsSum += Math.Clamp(elapsedMs, 0, GuessRules.ROUND_MS);
            int points = GuessRules.RoundPoints(GuessRules.ROUND_MS - elapsedMs, Streak);
            Score += points;
            return points;
        }
    }

    public class GuessSubmission
    {
        [JsonProperty("kind")] public string Kind { get; set; } = "song";
        [JsonProperty("score")] public int Score { get; set; }
        [JsonProperty("correct")] public int Correct { get; set; }
        [JsonProperty("rounds")] public int Rounds { get; set; }
        [JsonProperty("avg_ms")] public int AverageMs { get; set; }
    }

    public class GuessSubmitResponse
    {
        [JsonProperty("accepted")] public bool Accepted { get; set; }
        [JsonProperty("reason")] public string? Reason { get; set; }
        [JsonProperty("rank")] public int? Rank { get; set; }
        [JsonProperty("best_score")] public int BestScore { get; set; }
        [JsonProperty("personal_best")] public bool PersonalBest { get; set; }
    }

    public class GuessLeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank { get; set; }
        [JsonProperty("user_id")] public long UserId { get; set; }
        [JsonProperty("username")] public string Username { get; set; } = string.Empty;
        [JsonProperty("country_code")] public string? CountryCode { get; set; }
        [JsonProperty("score")] public int Score { get; set; }
        [JsonProperty("correct")] public int Correct { get; set; }
        [JsonProperty("rounds")] public int Rounds { get; set; }
        [JsonProperty("avg_ms")] public int AverageMs { get; set; }
        [JsonProperty("runs")] public int Runs { get; set; }
    }

    public class GuessLeaderboardResponse
    {
        [JsonProperty("kind")] public string Kind { get; set; } = "song";
        [JsonProperty("total")] public int Total { get; set; }
        [JsonProperty("entries")] public List<GuessLeaderboardEntry> Entries { get; set; } = new List<GuessLeaderboardEntry>();
        [JsonProperty("own")] public GuessLeaderboardEntry? Own { get; set; }
    }

    public class GuessBestResponse
    {
        [JsonProperty("best")] public Dictionary<string, GuessLeaderboardEntry> Best { get; set; } = new Dictionary<string, GuessLeaderboardEntry>();
    }

    /// <summary>Banchosucks: sends a finished guessing run to the lazer server plugin.</summary>
    public class SubmitGuessRequest : APIRequest<GuessSubmitResponse>
    {
        public readonly GuessSubmission Submission;

        public SubmitGuessRequest(GuessSubmission submission)
        {
            Submission = submission;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_guess/submit";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.ContentType = "application/json";
            req.AddRaw(JsonConvert.SerializeObject(Submission));
            return req;
        }
    }

    /// <summary>Banchosucks: leaderboard of one guessing game.</summary>
    public class GetGuessLeaderboardRequest : APIRequest<GuessLeaderboardResponse>
    {
        public readonly string Kind;

        public GetGuessLeaderboardRequest(string kind)
        {
            Kind = kind;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_guess/leaderboard";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.AddParameter("kind", Kind);
            req.AddParameter("limit", "50");
            return req;
        }
    }

    /// <summary>Banchosucks: the player's best runs of both guessing games.</summary>
    public class GetGuessBestRequest : APIRequest<GuessBestResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => "banchosucks_guess/best";
    }
}

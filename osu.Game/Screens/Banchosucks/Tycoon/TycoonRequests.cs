// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    public class TycoonSaveResponse
    {
        [JsonProperty("save")] public TycoonState? Save { get; set; }
        [JsonProperty("updated_at")] public long? UpdatedAt { get; set; }
        [JsonProperty("lifetime_total")] public double LifetimeTotal { get; set; }
    }

    public class TycoonSubmitResponse
    {
        [JsonProperty("accepted")] public bool Accepted { get; set; }
        [JsonProperty("reason")] public string? Reason { get; set; }
        [JsonProperty("clamped")] public bool Clamped { get; set; }
        [JsonProperty("lifetime_total")] public double? LifetimeTotal { get; set; }
        [JsonProperty("rank_total")] public int? RankTotal { get; set; }
        [JsonProperty("rank_players")] public int? RankPlayers { get; set; }
    }

    public class TycoonLeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank { get; set; }
        [JsonProperty("user_id")] public long UserId { get; set; }
        [JsonProperty("username")] public string Username { get; set; } = string.Empty;
        [JsonProperty("country_code")] public string? CountryCode { get; set; }
        [JsonProperty("avatar_url")] public string? AvatarUrl { get; set; }
        [JsonProperty("lifetime_total")] public double LifetimeTotal { get; set; }
        [JsonProperty("max_players")] public double MaxPlayers { get; set; }
        [JsonProperty("prestige")] public int Prestige { get; set; }
        [JsonProperty("legacy")] public int Legacy { get; set; }
        [JsonProperty("hardware")] public string Hardware { get; set; } = "pi";
        [JsonProperty("saves")] public int Saves { get; set; }
    }

    public class TycoonLeaderboardResponse
    {
        [JsonProperty("sort")] public string Sort { get; set; } = "total";
        [JsonProperty("total")] public int Total { get; set; }
        [JsonProperty("entries")] public List<TycoonLeaderboardEntry> Entries { get; set; } = new List<TycoonLeaderboardEntry>();
        [JsonProperty("own")] public TycoonLeaderboardEntry? Own { get; set; }
    }

    /// <summary>Banchosucks: the player's tycoon cloud save.</summary>
    public class GetTycoonSaveRequest : APIRequest<TycoonSaveResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => "banchosucks_tycoon/save";
    }

    /// <summary>Banchosucks: stores the tycoon save; the plugin checks it against the time since the last save.</summary>
    public class SubmitTycoonSaveRequest : APIRequest<TycoonSubmitResponse>
    {
        public readonly TycoonState State;

        public SubmitTycoonSaveRequest(TycoonState state)
        {
            State = state;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_tycoon/submit";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.ContentType = "application/json";
            req.AddRaw(JsonConvert.SerializeObject(new { save = State }));
            return req;
        }
    }

    /// <summary>Banchosucks: tycoon leaderboard by lifetime donations, player record or seasons.</summary>
    public class GetTycoonLeaderboardRequest : APIRequest<TycoonLeaderboardResponse>
    {
        public readonly string Sort;

        public GetTycoonLeaderboardRequest(string sort)
        {
            Sort = sort;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_tycoon/leaderboard";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.AddParameter("sort", Sort);
            req.AddParameter("limit", "50");
            return req;
        }
    }
}

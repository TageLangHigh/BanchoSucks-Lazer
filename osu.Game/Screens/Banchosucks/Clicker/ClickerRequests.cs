// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Screens.Banchosucks.Clicker
{
    /// <summary>
    /// Progress snapshot sent to the lazer server plugin <c>banchosucks_clicker</c> (POST /submit).
    /// Field names are the plugin's pydantic names; new fields need defaults on the server so older clients still parse.
    /// </summary>
    public class ClickerSubmission
    {
        [JsonProperty("season")] public int Season { get; set; }
        [JsonProperty("balance_version")] public int BalanceVersion { get; set; }
        [JsonProperty("bpm_epoch")] public int BpmEpoch { get; set; }
        [JsonProperty("total_earned")] public double TotalEarned { get; set; }
        [JsonProperty("run_earned")] public double RunEarned { get; set; }
        [JsonProperty("points")] public double Points { get; set; }
        [JsonProperty("clicks")] public long Clicks { get; set; }
        [JsonProperty("best_bpm")] public double BestBpm { get; set; }
        [JsonProperty("best_combo")] public int BestCombo { get; set; }
        [JsonProperty("producers")] public Dictionary<string, int> Producers { get; set; } = new Dictionary<string, int>();
        [JsonProperty("stars")] public Dictionary<string, int> Stars { get; set; } = new Dictionary<string, int>();
        [JsonProperty("upgrades")] public List<string> Upgrades { get; set; } = new List<string>();
        [JsonProperty("synergies")] public List<string> Synergies { get; set; } = new List<string>();
        [JsonProperty("stance")] public string Stance { get; set; } = "none";
        [JsonProperty("abilities")] public List<string> Abilities { get; set; } = new List<string>();
        [JsonProperty("rebirths")] public int Rebirths { get; set; }
        [JsonProperty("prestige_claimed")] public int PrestigeClaimed { get; set; }
        [JsonProperty("prestige_burned")] public int PrestigeBurned { get; set; }
        [JsonProperty("tree")] public List<string> Tree { get; set; } = new List<string>();
        [JsonProperty("prestige_producers")] public Dictionary<string, int> PrestigeProducers { get; set; } = new Dictionary<string, int>();
        [JsonProperty("ascensions")] public int Ascensions { get; set; }
        [JsonProperty("relics_total")] public int RelicsTotal { get; set; }
        [JsonProperty("relic_tree")] public List<string> RelicTree { get; set; } = new List<string>();
        [JsonProperty("all_time_earned")] public double AllTimeEarned { get; set; }
        [JsonProperty("best_era")] public double BestEra { get; set; }
        [JsonProperty("medals")] public List<string> Medals { get; set; } = new List<string>();
        [JsonProperty("encore_maps")] public int EncoreMaps { get; set; }
        [JsonProperty("daily_days")] public int DailyDays { get; set; }
        [JsonProperty("expeditions_done")] public int ExpeditionsDone { get; set; }
        [JsonProperty("sliders_done")] public int SlidersDone { get; set; }
        [JsonProperty("spinners_done")] public int SpinnersDone { get; set; }
        [JsonProperty("expeditions")] public List<ClickerSubmissionExpedition> Expeditions { get; set; } = new List<ClickerSubmissionExpedition>();
        [JsonProperty("modifier_id")] public string? ModifierId { get; set; }

        /// <summary>
        /// The complete save document (cloud save) as raw JSON; the server stores it as-is and hands it back through GET /save.
        /// </summary>
        [JsonProperty("save")] public JRaw? Save { get; set; }
    }

    public class ClickerSubmissionExpedition
    {
        [JsonProperty("producer")] public string Producer { get; set; } = string.Empty;
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("started_at")] public long StartedAt { get; set; }
        [JsonProperty("ends_at")] public long EndsAt { get; set; }
    }

    public class ClickerSubmitResponse
    {
        [JsonProperty("accepted")] public bool Accepted { get; set; }
        [JsonProperty("reason")] public string? Reason { get; set; }
        [JsonProperty("clamped")] public bool Clamped { get; set; }
        [JsonProperty("total_earned")] public double TotalEarned { get; set; }
        [JsonProperty("rank_pp")] public int? RankPp { get; set; }
        [JsonProperty("rank_bpm")] public int? RankBpm { get; set; }
        [JsonProperty("rank_prestige")] public int? RankPrestige { get; set; }
        [JsonProperty("season")] public int Season { get; set; }
        [JsonProperty("bpm_epoch")] public int BpmEpoch { get; set; }
        [JsonProperty("daily")] public ClickerDailyStatus? Daily { get; set; }
        [JsonProperty("modifier")] public ClickerModifierResponse? ModifierRaw { get; set; }
        [JsonProperty("profile_medals")] public List<int> ProfileMedals { get; set; } = new List<int>();

        /// <summary>Relics the server holds for the player after this submission (it verifies ascensions from its own stored lifetime).</summary>
        [JsonProperty("relics_total")] public int? RelicsTotal { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public ClickerModifier? Modifier => ModifierRaw?.ToModifier();
    }

    public class ClickerDailyStatus
    {
        [JsonProperty("day")] public string Day { get; set; } = string.Empty;
        [JsonProperty("played")] public bool Played { get; set; }
        [JsonProperty("streak")] public int Streak { get; set; }
    }

    /// <summary>
    /// The weekly rule as the server sends it (Newtonsoft on the wire, converted to the balance-file class).
    /// </summary>
    public class ClickerModifierResponse
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("production_mult")] public double ProductionMult { get; set; } = 1;
        [JsonProperty("click_mult")] public double ClickMult { get; set; } = 1;
        [JsonProperty("producer_mult")] public Dictionary<string, double> ProducerMult { get; set; } = new Dictionary<string, double>();
        [JsonProperty("name")] public ClickerLocalisedNameResponse Name { get; set; } = new ClickerLocalisedNameResponse();
        [JsonProperty("ends_at")] public long EndsAt { get; set; }

        public ClickerModifier ToModifier() => new ClickerModifier
        {
            Id = Id,
            ProductionMult = ProductionMult,
            ClickMult = ClickMult,
            ProducerMult = ProducerMult,
            Name = new LocalisedName { En = Name.En, De = Name.De },
            EndsAt = EndsAt,
        };
    }

    public class ClickerLocalisedNameResponse
    {
        [JsonProperty("en")] public string En { get; set; } = string.Empty;
        [JsonProperty("de")] public string De { get; set; } = string.Empty;
    }

    public class ClickerLeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank { get; set; }
        [JsonProperty("user_id")] public long UserId { get; set; }
        [JsonProperty("username")] public string Username { get; set; } = string.Empty;
        [JsonProperty("country_code")] public string? CountryCode { get; set; }
        [JsonProperty("avatar_url")] public string? AvatarUrl { get; set; }
        [JsonProperty("total_earned")] public double TotalEarned { get; set; }
        [JsonProperty("best_bpm")] public double BestBpm { get; set; }
        [JsonProperty("best_combo")] public int BestCombo { get; set; }
        [JsonProperty("clicks")] public long Clicks { get; set; }
        [JsonProperty("buildings")] public int Buildings { get; set; }
        [JsonProperty("rebirths")] public int Rebirths { get; set; }
        [JsonProperty("prestige")] public int Prestige { get; set; }
        [JsonProperty("medals")] public int Medals { get; set; }
        [JsonProperty("per_second")] public double PerSecond { get; set; }
        [JsonProperty("relics")] public int Relics { get; set; }
        [JsonProperty("ascensions")] public int Ascensions { get; set; }
    }

    public class ClickerLeaderboardResponse
    {
        [JsonProperty("sort")] public string Sort { get; set; } = "pp";
        [JsonProperty("total")] public int Total { get; set; }
        [JsonProperty("entries")] public List<ClickerLeaderboardEntry> Entries { get; set; } = new List<ClickerLeaderboardEntry>();
        [JsonProperty("own")] public ClickerLeaderboardEntry? Own { get; set; }
    }

    public class ClickerSaveResponse
    {
        [JsonProperty("season")] public int Season { get; set; }
        [JsonProperty("updated_at")] public long UpdatedAt { get; set; }
        [JsonProperty("save")] public JObject? Save { get; set; }
    }

    /// <summary>
    /// Banchosucks: sends the osu! Clicker progress to the lazer server plugin <c>banchosucks_clicker</c>.
    /// </summary>
    public class SubmitClickerScoreRequest : APIRequest<ClickerSubmitResponse>
    {
        public readonly ClickerSubmission Submission;

        public SubmitClickerScoreRequest(ClickerSubmission submission)
        {
            Submission = submission;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_clicker/submit";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.ContentType = "application/json";
            req.AddRaw(JsonConvert.SerializeObject(Submission, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            return req;
        }
    }

    /// <summary>
    /// Banchosucks: osu! Clicker leaderboard, sorted by lifetime PP ("pp"), tapping speed ("bpm"), prestige ("prestige") or medals ("medals").
    /// </summary>
    public class GetClickerLeaderboardRequest : APIRequest<ClickerLeaderboardResponse>
    {
        public readonly string Sort;

        public GetClickerLeaderboardRequest(string sort)
        {
            Sort = sort;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_clicker/leaderboard";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.AddParameter("sort", Sort);
            req.AddParameter("limit", "50");
            return req;
        }
    }

    /// <summary>
    /// Banchosucks: the cloud save of the logged-in player (GET /save).
    /// </summary>
    public class GetClickerSaveRequest : APIRequest<ClickerSaveResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => "banchosucks_clicker/save";
    }
}

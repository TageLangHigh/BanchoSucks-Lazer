// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace osu.Game.Screens.Banchosucks.Clicker
{
    /// <summary>
    /// What the osu! Clicker saves to <c>banchosucks/osu-clicker.json</c> and sends to the server as cloud save.
    /// </summary>
    /// <remarks>
    /// Property names are the JSON names (snake_case) so the server plugin can read the same document.
    /// Times are unix seconds (wall clock), never game time, so cooldowns and expeditions keep running while lazer is closed.
    /// </remarks>
    public class ClickerState
    {
        [JsonPropertyName("season")] public int Season { get; set; }
        [JsonPropertyName("balance_version")] public int BalanceVersion { get; set; }
        [JsonPropertyName("bpm_epoch")] public int BpmEpoch { get; set; }

        /// <summary>Spendable PP of the current run.</summary>
        [JsonPropertyName("points")] public double Points { get; set; }

        /// <summary>PP earned since the last rebirth.</summary>
        [JsonPropertyName("run_earned")] public double RunEarned { get; set; }

        /// <summary>PP earned over all runs: the leaderboard value and the prestige source. Never decreases.</summary>
        [JsonPropertyName("total_earned")] public double TotalEarned { get; set; }

        [JsonPropertyName("clicks")] public long Clicks { get; set; }
        [JsonPropertyName("best_bpm")] public double BestBpm { get; set; }
        [JsonPropertyName("best_combo")] public int BestCombo { get; set; }

        [JsonPropertyName("producers")] public Dictionary<string, int> Producers { get; set; } = new Dictionary<string, int>();

        /// <summary>Bought building stars per producer id (0..5).</summary>
        [JsonPropertyName("stars")] public Dictionary<string, int> Stars { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("upgrades")] public HashSet<string> Upgrades { get; set; } = new HashSet<string>();
        [JsonPropertyName("synergies")] public HashSet<string> Synergies { get; set; } = new HashSet<string>();

        /// <summary>Active mod stance id ("none", "auto", "nightcore", "hidden").</summary>
        [JsonPropertyName("stance")] public string Stance { get; set; } = "none";

        /// <summary>Unlocked abilities (bought with PP in the run, or kept by the tree).</summary>
        [JsonPropertyName("abilities")] public HashSet<string> Abilities { get; set; } = new HashSet<string>();

        /// <summary>Unix second at which the ability can be used again.</summary>
        [JsonPropertyName("ability_ready_at")] public Dictionary<string, long> AbilityReadyAt { get; set; } = new Dictionary<string, long>();

        /// <summary>Unix second until which the ability is active.</summary>
        [JsonPropertyName("ability_active_until")] public Dictionary<string, long> AbilityActiveUntil { get; set; } = new Dictionary<string, long>();

        [JsonPropertyName("rebirths")] public int Rebirths { get; set; }

        /// <summary>Prestige points ever claimed by rebirths; drives the permanent bonus together with <see cref="PrestigeBurned"/>.</summary>
        [JsonPropertyName("prestige_claimed")] public int PrestigeClaimed { get; set; }

        /// <summary>Prestige points lost to respecs.</summary>
        [JsonPropertyName("prestige_burned")] public int PrestigeBurned { get; set; }

        [JsonPropertyName("tree")] public HashSet<string> Tree { get; set; } = new HashSet<string>();

        /// <summary>Prestige buildings owned (bought with prestige points, kept through rebirths).</summary>
        [JsonPropertyName("prestige_producers")] public Dictionary<string, int> PrestigeProducers { get; set; } = new Dictionary<string, int>();
        [JsonPropertyName("medals")] public HashSet<string> Medals { get; set; } = new HashSet<string>();

        [JsonPropertyName("encore_maps")] public int EncoreMaps { get; set; }
        [JsonPropertyName("encore_hour_start")] public long EncoreHourStart { get; set; }
        [JsonPropertyName("encore_hour_count")] public int EncoreHourCount { get; set; }
        [JsonPropertyName("warmed_up_until")] public long WarmedUpUntil { get; set; }

        [JsonPropertyName("daily_days")] public int DailyDays { get; set; }
        [JsonPropertyName("last_daily_day")] public string LastDailyDay { get; set; } = string.Empty;
        [JsonPropertyName("daily_streak")] public int DailyStreak { get; set; }

        [JsonPropertyName("expeditions_done")] public int ExpeditionsDone { get; set; }
        [JsonPropertyName("expeditions")] public List<ClickerExpedition> Expeditions { get; set; } = new List<ClickerExpedition>();

        [JsonPropertyName("sliders_done")] public int SlidersDone { get; set; }
        [JsonPropertyName("slider_best")] public double SliderBest { get; set; }
        [JsonPropertyName("spinners_done")] public int SpinnersDone { get; set; }

        /// <summary>Unix second of the last save; the engine credits the time since then as offline production.</summary>
        [JsonPropertyName("saved_at")] public long SavedAt { get; set; }

        /// <summary>PP earned while the clicker screen was closed, split by where the player was; shown as a receipt on the next visit.</summary>
        [JsonPropertyName("ledger")] public ClickerLedger Ledger { get; set; } = new ClickerLedger();

        [JsonPropertyName("created_at")] public long CreatedAt { get; set; }
    }

    public class ClickerExpedition
    {
        [JsonPropertyName("producer")] public string Producer { get; set; } = string.Empty;
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("started_at")] public long StartedAt { get; set; }
        [JsonPropertyName("ends_at")] public long EndsAt { get; set; }

        /// <summary>Production rate of one building at departure, so the reward does not depend on later purchases.</summary>
        [JsonPropertyName("rate")] public double Rate { get; set; }
    }

    public class ClickerLedger
    {
        [JsonPropertyName("maps")] public double Maps { get; set; }
        [JsonPropertyName("client")] public double Client { get; set; }
        [JsonPropertyName("offline")] public double Offline { get; set; }
        [JsonPropertyName("encore")] public double Encore { get; set; }
        [JsonPropertyName("expeditions")] public double Expeditions { get; set; }
        [JsonPropertyName("daily")] public double Daily { get; set; }
        [JsonPropertyName("since")] public long Since { get; set; }

        [JsonIgnore] public double Total => Maps + Client + Offline + Encore + Expeditions + Daily;

        public void Clear(long now)
        {
            Maps = Client = Offline = Encore = Expeditions = Daily = 0;
            Since = now;
        }
    }
}

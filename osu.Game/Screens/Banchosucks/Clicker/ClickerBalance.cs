// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Clicker
{
    /// <summary>
    /// Banchosucks: all numbers of the osu! Clicker, read from the embedded <c>clicker-balance.json</c>.
    /// </summary>
    /// <remarks>
    /// The lazer server plugin <c>banchosucks_clicker</c> (website repository, server/lazer/plugins) ships a copy of
    /// the same file and recomputes production from it to keep the leaderboard believable. Change the JSON, never the
    /// numbers in code, and copy it to the plugin in the same release; <c>balance_version</c> tells both sides apart.
    /// </remarks>
    public sealed class ClickerBalance
    {
        private static readonly Lazy<ClickerBalance> instance = new Lazy<ClickerBalance>(load);

        public static ClickerBalance Instance => instance.Value;

        /// <summary>
        /// osu!'s default combo colours, used for the "+PP" popups and hit bursts.
        /// </summary>
        public static readonly Color4[] COMBO_COLOURS =
        {
            Color4Extensions.FromHex("ffc000"),
            Color4Extensions.FromHex("00ca00"),
            Color4Extensions.FromHex("127cff"),
            Color4Extensions.FromHex("f21839"),
        };

        [JsonPropertyName("balance_version")] public int BalanceVersion { get; set; }

        /// <summary>
        /// Saves and submissions from an older season are discarded: the one-time player reset of season 2
        /// (October 2026) wiped every leaderboard entry and every local save.
        /// </summary>
        [JsonPropertyName("season")] public int Season { get; set; }

        /// <summary>
        /// BPM records only count on the server when the client sends this epoch or newer. Epoch 2 came with the
        /// input lock (keyboard and mouse can no longer be tapped at the same time), epoch 3 with the 500 BPM cap.
        /// </summary>
        [JsonPropertyName("bpm_epoch")] public int BpmEpoch { get; set; }

        [JsonPropertyName("max_bpm")] public double MaxBpm { get; set; }
        [JsonPropertyName("cost_growth")] public double CostGrowth { get; set; }
        [JsonPropertyName("offline_rate")] public double OfflineRate { get; set; }
        [JsonPropertyName("offline_cap_seconds")] public double OfflineCapSeconds { get; set; }
        [JsonPropertyName("combo_timeout_ms")] public double ComboTimeoutMs { get; set; }
        [JsonPropertyName("prestige_divisor")] public double PrestigeDivisor { get; set; }
        [JsonPropertyName("prestige_rate")] public double PrestigeRate { get; set; }

        /// <summary>
        /// Prestige points up to which the bonus grows linearly; beyond it with the square root (0 = linear forever).
        /// </summary>
        [JsonPropertyName("prestige_softcap")] public double PrestigeSoftcap { get; set; }
        [JsonPropertyName("medal_rate")] public double MedalRate { get; set; }
        [JsonPropertyName("respec_share")] public double RespecShare { get; set; }
        [JsonPropertyName("stance_unlock_buildings")] public int StanceUnlockBuildings { get; set; }
        [JsonPropertyName("star_thresholds")] public int[] StarThresholds { get; set; } = Array.Empty<int>();
        [JsonPropertyName("star_extra_threshold")] public int StarExtraThreshold { get; set; }
        [JsonPropertyName("star_cost_base_mult")] public double StarCostBaseMult { get; set; }
        [JsonPropertyName("star_cost_growth")] public double StarCostGrowth { get; set; }
        [JsonPropertyName("synergy_min_count")] public int SynergyMinCount { get; set; }
        [JsonPropertyName("synergy_per_source")] public double SynergyPerSource { get; set; }
        [JsonPropertyName("synergy_cap")] public double SynergyCap { get; set; }
        [JsonPropertyName("synergy_cost_mult")] public double SynergyCostMult { get; set; }
        [JsonPropertyName("spinner")] public SpinnerRules Spinner { get; set; } = new SpinnerRules();
        [JsonPropertyName("slider")] public SliderRules Slider { get; set; } = new SliderRules();
        [JsonPropertyName("encore")] public EncoreRules Encore { get; set; } = new EncoreRules();
        [JsonPropertyName("daily")] public DailyRules Daily { get; set; } = new DailyRules();
        [JsonPropertyName("expedition")] public ExpeditionRules Expedition { get; set; } = new ExpeditionRules();
        [JsonPropertyName("producers")] public ClickerProducer[] Producers { get; set; } = Array.Empty<ClickerProducer>();

        /// <summary>
        /// Buildings bought with prestige points; they survive rebirths and get neither stars nor synergies.
        /// </summary>
        [JsonPropertyName("prestige_producers")] public ClickerPrestigeProducer[] PrestigeProducers { get; set; } = Array.Empty<ClickerPrestigeProducer>();
        [JsonPropertyName("prestige_producer_growth")] public double PrestigeProducerGrowth { get; set; } = 1.5;
        [JsonPropertyName("prestige_producers_unlock_rebirths")] public int PrestigeProducersUnlockRebirths { get; set; } = 1;

        /// <summary>
        /// Ascension (2026-10-04): a voluntary restart of the economy that turns the era's lifetime PP into relics.
        /// </summary>
        [JsonPropertyName("ascension")] public AscensionRules Ascension { get; set; } = new AscensionRules();

        /// <summary>
        /// The relic tree: bought with relics, survives rebirths and ascensions. Same node shape as the prestige tree.
        /// </summary>
        [JsonPropertyName("relic_tree")] public ClickerTreeNode[] RelicTree { get; set; } = Array.Empty<ClickerTreeNode>();
        [JsonPropertyName("upgrades")] public ClickerUpgrade[] Upgrades { get; set; } = Array.Empty<ClickerUpgrade>();
        [JsonPropertyName("synergies")] public ClickerSynergy[] Synergies { get; set; } = Array.Empty<ClickerSynergy>();
        [JsonPropertyName("abilities")] public ClickerAbility[] Abilities { get; set; } = Array.Empty<ClickerAbility>();
        [JsonPropertyName("stances")] public ClickerStance[] Stances { get; set; } = Array.Empty<ClickerStance>();
        [JsonPropertyName("tree")] public ClickerTreeNode[] Tree { get; set; } = Array.Empty<ClickerTreeNode>();
        [JsonPropertyName("medals")] public ClickerMedal[] Medals { get; set; } = Array.Empty<ClickerMedal>();
        [JsonPropertyName("modifier_presets")] public ClickerModifier[] ModifierPresets { get; set; } = Array.Empty<ClickerModifier>();

        private Dictionary<string, ClickerProducer> producersById = null!;
        private Dictionary<string, ClickerPrestigeProducer> prestigeProducersById = null!;
        private Dictionary<string, ClickerUpgrade> upgradesById = null!;
        private Dictionary<string, ClickerSynergy> synergiesById = null!;
        private Dictionary<string, ClickerAbility> abilitiesById = null!;
        private Dictionary<string, ClickerStance> stancesById = null!;
        private Dictionary<string, ClickerTreeNode> treeById = null!;
        private Dictionary<string, ClickerMedal> medalsById = null!;
        private Dictionary<string, ClickerTreeNode> relicTreeById = null!;

        public ClickerProducer? Producer(string id) => producersById.GetValueOrDefault(id);
        public ClickerPrestigeProducer? PrestigeProducer(string id) => prestigeProducersById.GetValueOrDefault(id);
        public ClickerUpgrade? Upgrade(string id) => upgradesById.GetValueOrDefault(id);
        public ClickerSynergy? Synergy(string id) => synergiesById.GetValueOrDefault(id);
        public ClickerAbility? Ability(string id) => abilitiesById.GetValueOrDefault(id);
        public ClickerStance? Stance(string id) => stancesById.GetValueOrDefault(id);
        public ClickerTreeNode? Node(string id) => treeById.GetValueOrDefault(id);
        public ClickerMedal? Medal(string id) => medalsById.GetValueOrDefault(id);
        public ClickerTreeNode? RelicNode(string id) => relicTreeById.GetValueOrDefault(id);

        public IEnumerable<string> Branches => Tree.Select(n => n.Branch).Distinct();

        private static ClickerBalance load()
        {
            using var stream = typeof(ClickerBalance).Assembly.GetManifestResourceStream("clicker-balance.json")
                               ?? throw new InvalidOperationException("clicker-balance.json is not embedded in osu.Game");
            return Parse(stream);
        }

        /// <summary>
        /// Reads a balance file; tests use this to check the embedded copy against fixtures.
        /// </summary>
        public static ClickerBalance Parse(Stream stream)
        {
            var balance = JsonSerializer.Deserialize<ClickerBalance>(stream) ?? throw new InvalidOperationException("clicker-balance.json is empty");
            balance.index();
            return balance;
        }

        private void index()
        {
            producersById = Producers.ToDictionary(p => p.Id);
            prestigeProducersById = PrestigeProducers.ToDictionary(p => p.Id);
            upgradesById = Upgrades.ToDictionary(u => u.Id);
            synergiesById = Synergies.ToDictionary(s => s.Id);
            abilitiesById = Abilities.ToDictionary(a => a.Id);
            stancesById = Stances.ToDictionary(s => s.Id);
            treeById = Tree.ToDictionary(n => n.Id);
            medalsById = Medals.ToDictionary(m => m.Id);
            relicTreeById = RelicTree.ToDictionary(n => n.Id);
        }

        /// <summary>
        /// Resolves a FontAwesome icon name from the balance file ("MousePointer" = <see cref="FontAwesome.Solid.MousePointer"/>).
        /// </summary>
        public static IconUsage Icon(string? name, IconUsage fallback)
        {
            if (string.IsNullOrEmpty(name))
                return fallback;

            var property = typeof(FontAwesome.Solid).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            return property?.GetValue(null) is IconUsage icon ? icon : fallback;
        }

        public static Color4 Colour(string? hex, Color4 fallback) => string.IsNullOrEmpty(hex) ? fallback : Color4Extensions.FromHex(hex);
    }

    public class LocalisedName
    {
        [JsonPropertyName("en")] public string En { get; set; } = string.Empty;
        [JsonPropertyName("de")] public string De { get; set; } = string.Empty;

        public string Get(bool german) => german && !string.IsNullOrEmpty(De) ? De : En;
    }

    public class ClickerProducer
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("base_cost")] public double BaseCost { get; set; }
        [JsonPropertyName("per_second")] public double PerSecond { get; set; }
        [JsonPropertyName("icon")] public string? IconName { get; set; }
        [JsonPropertyName("colour")] public string? ColourHex { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();

        /// <summary>
        /// Tree node that has to be owned before this building shows up in the shop; null for the eight base buildings.
        /// </summary>
        [JsonPropertyName("requires_node")] public string? RequiresNode { get; set; }

        [JsonIgnore] public IconUsage Icon => ClickerBalance.Icon(IconName, FontAwesome.Solid.Cube);
        [JsonIgnore] public Color4 Colour => ClickerBalance.Colour(ColourHex, Color4.White);
    }

    public class ClickerPrestigeProducer
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        /// <summary>Price in prestige points of the first one; each further one costs <see cref="ClickerBalance.PrestigeProducerGrowth"/> times more.</summary>
        [JsonPropertyName("cost")] public double Cost { get; set; }
        [JsonPropertyName("per_second")] public double PerSecond { get; set; }
        [JsonPropertyName("icon")] public string? IconName { get; set; }
        [JsonPropertyName("colour")] public string? ColourHex { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();

        [JsonIgnore] public IconUsage Icon => ClickerBalance.Icon(IconName, FontAwesome.Solid.Star);
        [JsonIgnore] public Color4 Colour => ClickerBalance.Colour(ColourHex, Color4.Gold);
    }

    public class ClickerUpgrade
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("cost")] public double Cost { get; set; }
        [JsonPropertyName("icon")] public string? IconName { get; set; }
        [JsonPropertyName("mod")] public string? ModAcronym { get; set; }
        [JsonPropertyName("click")] public double Click { get; set; } = 1;
        [JsonPropertyName("production")] public double Production { get; set; } = 1;
        [JsonPropertyName("click_share")] public double ClickShare { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();

        [JsonIgnore] public IconUsage Icon => ClickerBalance.Icon(IconName, FontAwesome.Solid.ArrowUp);
    }

    public class ClickerSynergy
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
        [JsonPropertyName("target")] public string Target { get; set; } = string.Empty;
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();
    }

    public class ClickerAbility
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("unlock_cost")] public double UnlockCost { get; set; }
        [JsonPropertyName("duration_s")] public double DurationSeconds { get; set; }
        [JsonPropertyName("cooldown_s")] public double CooldownSeconds { get; set; }
        [JsonPropertyName("click_mult")] public double ClickMult { get; set; } = 1;
        [JsonPropertyName("production_mult")] public double ProductionMult { get; set; } = 1;
        [JsonPropertyName("icon")] public string? IconName { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();

        [JsonIgnore] public IconUsage Icon => ClickerBalance.Icon(IconName, FontAwesome.Solid.Bolt);
    }

    public class ClickerStance
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("click_mult")] public double ClickMult { get; set; } = 1;
        [JsonPropertyName("production_mult")] public double ProductionMult { get; set; } = 1;
        [JsonPropertyName("hide_numbers")] public bool HideNumbers { get; set; }
        [JsonPropertyName("mod")] public string? ModAcronym { get; set; }
        [JsonPropertyName("icon")] public string? IconName { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();

        [JsonIgnore] public IconUsage Icon => ClickerBalance.Icon(IconName, FontAwesome.Regular.Circle);
    }

    public class ClickerTreeNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("branch")] public string Branch { get; set; } = string.Empty;
        [JsonPropertyName("tier")] public int Tier { get; set; }
        [JsonPropertyName("cost")] public int Cost { get; set; }
        [JsonPropertyName("effect")] public string Effect { get; set; } = string.Empty;
        [JsonPropertyName("value")] public double Value { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();
        [JsonPropertyName("keeps")] public string[]? Keeps { get; set; }
        [JsonPropertyName("head_start")] public Dictionary<string, int>? HeadStart { get; set; }
        [JsonPropertyName("unlocks")] public string? Unlocks { get; set; }

        /// <summary>Relic tree only: ascensions needed before the node can be bought (the tiers open with real ascensions, not with banked relics).</summary>
        [JsonPropertyName("requires_ascensions")] public int RequiresAscensions { get; set; }

        /// <summary>
        /// Explicit prerequisites; without them the node needs the previous tier of its branch.
        /// </summary>
        [JsonPropertyName("requires")] public string[]? Requires { get; set; }
    }

    public class ClickerMedal
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
        [JsonPropertyName("value")] public double Value { get; set; }
        [JsonPropertyName("node")] public string? Node { get; set; }
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("description")] public LocalisedName Description { get; set; } = new LocalisedName();

        /// <summary>
        /// Id of the real lazer profile medal the server awards for this one, if any.
        /// </summary>
        [JsonPropertyName("profile_medal")] public int? ProfileMedal { get; set; }
    }

    /// <summary>
    /// A server-side weekly rule ("Taiko week: drums x2"), either a preset from the balance file or one the team set.
    /// </summary>
    public class ClickerModifier
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("production_mult")] public double ProductionMult { get; set; } = 1;
        [JsonPropertyName("click_mult")] public double ClickMult { get; set; } = 1;
        [JsonPropertyName("producer_mult")] public Dictionary<string, double> ProducerMult { get; set; } = new Dictionary<string, double>();
        [JsonPropertyName("name")] public LocalisedName Name { get; set; } = new LocalisedName();
        [JsonPropertyName("ends_at")] public long EndsAt { get; set; }
    }

    public class SpinnerRules
    {
        [JsonPropertyName("min_interval_s")] public double MinIntervalSeconds { get; set; }
        [JsonPropertyName("max_interval_s")] public double MaxIntervalSeconds { get; set; }
        [JsonPropertyName("lifetime_s")] public double LifetimeSeconds { get; set; }
        [JsonPropertyName("click_mult")] public double ClickMult { get; set; }
        [JsonPropertyName("production_seconds")] public double ProductionSeconds { get; set; }
        [JsonPropertyName("kiai_chance")] public double KiaiChance { get; set; }
        [JsonPropertyName("kiai_mult")] public double KiaiMult { get; set; }
    }

    public class SliderRules
    {
        [JsonPropertyName("min_interval_s")] public double MinIntervalSeconds { get; set; }
        [JsonPropertyName("max_interval_s")] public double MaxIntervalSeconds { get; set; }
        [JsonPropertyName("duration_s")] public double DurationSeconds { get; set; }
        [JsonPropertyName("production_seconds")] public double ProductionSeconds { get; set; }
        [JsonPropertyName("click_mult")] public double ClickMult { get; set; }
    }

    public class EncoreRules
    {
        [JsonPropertyName("production_seconds")] public double ProductionSeconds { get; set; }
        [JsonPropertyName("clicks")] public double Clicks { get; set; }
        [JsonPropertyName("warmup_seconds")] public double WarmupSeconds { get; set; }
        [JsonPropertyName("warmup_mult")] public double WarmupMult { get; set; }
        [JsonPropertyName("max_per_hour")] public int MaxPerHour { get; set; }
    }

    public class DailyRules
    {
        [JsonPropertyName("production_seconds")] public double ProductionSeconds { get; set; }
    }

    public class AscensionRules
    {
        /// <summary>Era lifetime PP needed before ascending is possible (worth the first relic).</summary>
        [JsonPropertyName("min_total_earned")] public double MinTotalEarned { get; set; }

        /// <summary>Permanent production and click bonus per relic ever earned.</summary>
        [JsonPropertyName("relic_bonus")] public double RelicBonus { get; set; }
    }

    public class ExpeditionRules
    {
        [JsonPropertyName("durations_s")] public double[] DurationsSeconds { get; set; } = Array.Empty<double>();
        [JsonPropertyName("success_chance")] public double SuccessChance { get; set; }
        [JsonPropertyName("reward_mult")] public double RewardMult { get; set; }
        [JsonPropertyName("improved_chance")] public double ImprovedChance { get; set; }
    }
}

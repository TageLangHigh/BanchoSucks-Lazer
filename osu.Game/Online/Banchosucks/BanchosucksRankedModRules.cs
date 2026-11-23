// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: whether a mod combination gives pp on our lazer server. Pure, no game state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a hand-written mirror of the server and has to be changed together with it:
    /// <list type="bullet">
    /// <item>plugin <c>banchosucks_rankedmods/rules.py</c>: Double Time at every speed from 1.01x to 2.00x, Difficulty Adjust in
    /// osu!, taiko and catch inside the limits below (circle size up to 7, everything else in the normal slider range), not in mania;</item>
    /// <item>plugin <c>banchosucks_stablescores</c> (<c>_ensure_classic_ranked</c>): Classic gives pp in all four rulesets;</item>
    /// <item>plugin <c>banchosucks_maniapp</c> (<c>NEUTRAL_MODS</c>, the same list as <c>ManiaPerformance.NEUTRAL_MODS</c> in the
    /// mania ruleset): any other mod and every convert gives no pp in osu!mania.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Every mod the plugins do not name follows g0v0's default rule. That file (<c>config/ranked_mods.json</c> on the server) is
    /// not part of the client; the client's own <c>Mod.Ranked</c> stands in for it (<see cref="ModEntry.RankedByClient"/>), which is
    /// the upstream rule g0v0's defaults are modelled on (Easy with two retries, Half Time at 0.75x, ...).
    /// Open point: in osu!mania the neutral list contains Hard Rock and Constant Speed, which <c>Mod.Ranked</c> refuses. The list is
    /// followed here; whether g0v0's file agrees for ruleset 3 has to be checked on the server.
    /// </para>
    /// <para>
    /// The setting rules read like g0v0's <c>check_settings</c>: a setting the client does not send (the default) always passes, a
    /// setting the rule does not name gives no pp, a named one has to be inside its bounds unless it is not checked.
    /// </para>
    /// </remarks>
    public static class BanchosucksRankedModRules
    {
        public const double DOUBLE_TIME_MIN_SPEED = 1.01;
        public const double DOUBLE_TIME_MAX_SPEED = 2.0;

        /// <summary>The rating runs away above this circle size (12.5 stars at CS 8 on a 7.8 star map), so pp end here.</summary>
        public const double MAX_CIRCLE_SIZE = 7;

        public const int RULESET_OSU = 0;
        public const int RULESET_TAIKO = 1;
        public const int RULESET_CATCH = 2;
        public const int RULESET_MANIA = 3;

        /// <summary>
        /// Mods an osu!mania play may carry and still receive pp. Must equal <c>ManiaPerformance.NEUTRAL_MODS</c>
        /// (checked by <c>BanchosucksRankedModRulesTest.TestManiaNeutralModListsAreTheSame</c>).
        /// </summary>
        public static readonly IReadOnlySet<string> MANIA_NEUTRAL_MODS = new HashSet<string>
        {
            "NF", "EZ", "HD", "FI", "FL", "HT", "DC", "DT", "NC", "MR", "CL", "SD", "PF", "HR", "MU", "AC", "CS",
        };

        private static readonly IReadOnlyDictionary<string, SettingRule> double_time = new Dictionary<string, SettingRule>
        {
            ["speed_change"] = SettingRule.Number(DOUBLE_TIME_MIN_SPEED, DOUBLE_TIME_MAX_SPEED),
            ["adjust_pitch"] = SettingRule.NOT_CHECKED,
        };

        // an empty rule accepts every setting
        private static readonly IReadOnlyDictionary<string, SettingRule> classic = new Dictionary<string, SettingRule>();

        private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, SettingRule>> difficulty_adjust = new Dictionary<int, IReadOnlyDictionary<string, SettingRule>>
        {
            [RULESET_OSU] = new Dictionary<string, SettingRule>
            {
                ["circle_size"] = SettingRule.Number(0, MAX_CIRCLE_SIZE),
                ["approach_rate"] = SettingRule.Number(0, 10),
                ["overall_difficulty"] = SettingRule.Number(0, 10),
                ["drain_rate"] = SettingRule.Number(0, 10),
                ["extended_limits"] = SettingRule.NOT_CHECKED,
            },
            [RULESET_TAIKO] = new Dictionary<string, SettingRule>
            {
                ["scroll_speed"] = SettingRule.Number(0.25, 4),
                ["overall_difficulty"] = SettingRule.Number(0, 10),
                ["drain_rate"] = SettingRule.Number(0, 10),
                ["extended_limits"] = SettingRule.NOT_CHECKED,
            },
            [RULESET_CATCH] = new Dictionary<string, SettingRule>
            {
                ["circle_size"] = SettingRule.Number(0, MAX_CIRCLE_SIZE),
                ["approach_rate"] = SettingRule.Number(0, 10),
                ["hard_rock_offsets"] = SettingRule.NOT_CHECKED,
                ["overall_difficulty"] = SettingRule.Number(0, 10),
                ["drain_rate"] = SettingRule.Number(0, 10),
                ["extended_limits"] = SettingRule.NOT_CHECKED,
            },
        };

        /// <summary>
        /// Whether a play in <paramref name="rulesetId"/> with <paramref name="mods"/> gives pp on Banchosucks, and if not, why.
        /// The first mod that fails decides the reason.
        /// </summary>
        /// <param name="rulesetId">The ruleset played (0 osu!, 1 taiko, 2 catch, 3 mania). Other rulesets only follow the client's rule.</param>
        /// <param name="mods">The selected mods.</param>
        /// <param name="convert">Whether the map was made for another ruleset than the one played.</param>
        public static Verdict Check(int rulesetId, IEnumerable<ModEntry> mods, bool convert = false)
        {
            bool official = rulesetId >= RULESET_OSU && rulesetId <= RULESET_MANIA;

            if (rulesetId == RULESET_MANIA && convert)
                return new Verdict(Reason.ManiaConvert);

            foreach (var mod in mods)
            {
                if (rulesetId == RULESET_MANIA && !MANIA_NEUTRAL_MODS.Contains(mod.Acronym))
                    return new Verdict(Reason.ManiaMod, mod.Acronym);

                IReadOnlyDictionary<string, SettingRule>? rule = official ? ruleFor(rulesetId, mod.Acronym) : null;

                if (rule != null)
                {
                    var verdict = checkSettings(mod, rule);

                    if (!verdict.GivesPerformance)
                        return verdict;

                    continue;
                }

                if (mod.RankedByClient)
                    continue;

                // osu!mania: the neutral list is the server team's own decision and names two mods the client never ranks
                // (Hard Rock, Constant Speed). The list wins for the mod itself, as it does in the client's mania pp
                // (ManiaPerformance.ModsGivePerformance); changed settings still follow the client's rule.
                if (rulesetId == RULESET_MANIA && !mod.RankedByClientWithDefaults)
                    continue;

                return new Verdict(mod.RankedByClientWithDefaults ? Reason.ModSettings : Reason.UnrankedMod, mod.Acronym);
            }

            return new Verdict(Reason.None);
        }

        /// <summary>The rule our plugins set for a mod, or null where g0v0's default applies.</summary>
        private static IReadOnlyDictionary<string, SettingRule>? ruleFor(int rulesetId, string acronym)
        {
            switch (acronym)
            {
                case "DT":
                    return double_time;

                case "CL":
                    return classic;

                case "DA":
                    return difficulty_adjust.GetValueOrDefault(rulesetId);

                default:
                    return null;
            }
        }

        private static Verdict checkSettings(ModEntry mod, IReadOnlyDictionary<string, SettingRule> rule)
        {
            if (rule.Count == 0 || mod.Settings == null)
                return new Verdict(Reason.None);

            foreach (string name in mod.Settings.Keys)
            {
                if (!rule.ContainsKey(name))
                    return new Verdict(Reason.SettingNotAllowed, mod.Acronym, name);
            }

            foreach (var (name, validation) in rule)
            {
                if (!validation.Checked || !mod.Settings.TryGetValue(name, out object? value))
                    continue;

                if (!tryGetNumber(value, out double number))
                    return new Verdict(Reason.SettingNotAllowed, mod.Acronym, name);

                if (number < validation.Minimum || number > validation.Maximum)
                    return new Verdict(Reason.SettingOutOfRange, mod.Acronym, name, validation.Minimum, validation.Maximum);
            }

            return new Verdict(Reason.None);
        }

        private static bool tryGetNumber(object? value, out double number)
        {
            switch (value)
            {
                case double d:
                    number = d;
                    return !double.IsNaN(d);

                case float f:
                    // the difficulty sliders work in float; 7.1f must not become 7.099999904 and still compare as intended,
                    // so go through the shortest decimal form the player sees
                    return double.TryParse(f.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

                case int or long or short or byte or decimal:
                    number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;

                default:
                    number = 0;
                    return false;
            }
        }

        public enum Reason
        {
            /// <summary>The play gives pp.</summary>
            None,

            /// <summary>A mod that never gives pp.</summary>
            UnrankedMod,

            /// <summary>A mod that only gives pp with its default settings.</summary>
            ModSettings,

            /// <summary>A setting our rule limits is outside its range.</summary>
            SettingOutOfRange,

            /// <summary>A setting our rule does not allow at all (for example a value of another ruleset).</summary>
            SettingNotAllowed,

            /// <summary>A mod outside the osu!mania neutral list.</summary>
            ManiaMod,

            /// <summary>A map of another ruleset played in osu!mania.</summary>
            ManiaConvert,
        }

        /// <summary>
        /// One selected mod as the rules see it.
        /// </summary>
        /// <param name="Acronym">The mod's acronym ("DT").</param>
        /// <param name="Settings">The settings as the client sends them: only the ones that differ from the default, snake_case names.</param>
        /// <param name="RankedByClient">The client's own <c>Mod.Ranked</c> for the mod as configured.</param>
        /// <param name="RankedByClientWithDefaults"><c>Mod.Ranked</c> of the same mod with default settings.</param>
        public readonly record struct ModEntry(string Acronym, IReadOnlyDictionary<string, object>? Settings = null, bool RankedByClient = true, bool RankedByClientWithDefaults = true);

        /// <summary>
        /// The answer of <see cref="Check"/>.
        /// </summary>
        /// <param name="Reason">Why the play gives no pp, or <see cref="BanchosucksRankedModRules.Reason.None"/>.</param>
        /// <param name="Acronym">The mod that decided it.</param>
        /// <param name="Setting">The setting that decided it (snake_case).</param>
        /// <param name="Minimum">The smallest value of <paramref name="Setting"/> that gives pp.</param>
        /// <param name="Maximum">The largest value of <paramref name="Setting"/> that gives pp.</param>
        public readonly record struct Verdict(Reason Reason, string? Acronym = null, string? Setting = null, double Minimum = 0, double Maximum = 0)
        {
            public bool GivesPerformance => Reason == Reason.None;
        }

        private readonly record struct SettingRule(bool Checked, double Minimum, double Maximum)
        {
            public static readonly SettingRule NOT_CHECKED = new SettingRule(false, 0, 0);

            public static SettingRule Number(double minimum, double maximum) => new SettingRule(true, minimum, maximum);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: asks <see cref="BanchosucksRankedModRules"/> about the mods selected in the game and puts the answer into words.
    /// Only the mods are judged; whether the map itself gives pp (its ranked status on our server) is a separate question.
    /// </summary>
    public static class BanchosucksPerformanceEligibility
    {
        public static BanchosucksRankedModRules.Verdict Check(IRulesetInfo? ruleset, IBeatmapInfo? beatmap, IEnumerable<Mod>? mods)
        {
            // no ruleset: only the client's own rule can be asked
            int rulesetId = ruleset?.OnlineID ?? -1;
            bool convert = beatmap != null && ruleset != null && beatmap.Ruleset.OnlineID >= 0 && beatmap.Ruleset.OnlineID != ruleset.OnlineID;

            return BanchosucksRankedModRules.Check(rulesetId, (mods ?? Enumerable.Empty<Mod>()).Select(toEntry), convert);
        }

        private static BanchosucksRankedModRules.ModEntry toEntry(Mod mod)
        {
            bool ranked = mod.Ranked;
            bool rankedWithDefaults = ranked;

            if (!ranked && !mod.UsesDefaultConfiguration)
            {
                var plain = mod.DeepClone();
                plain.ResetSettingsToDefaults();
                rankedWithDefaults = plain.Ranked;
            }

            // the settings exactly as they are sent with a score: only what differs from the default
            return new BanchosucksRankedModRules.ModEntry(mod.Acronym, new APIMod(mod).Settings, ranked, rankedWithDefaults);
        }

        /// <summary>
        /// The short form for places with a fixed width: "gives pp" or "no pp".
        /// </summary>
        public static LocalisableString Label(BanchosucksRankedModRules.Verdict verdict)
            => verdict.GivesPerformance ? ClickerStrings.Text("gives pp", "gibt pp") : ClickerStrings.Text("no pp", "keine pp");

        /// <summary>
        /// "gives pp", or "no pp on Banchosucks: reason".
        /// </summary>
        public static LocalisableString Describe(BanchosucksRankedModRules.Verdict verdict)
        {
            if (verdict.GivesPerformance)
                return ClickerStrings.Text("gives pp", "gibt pp");

            string acronym = verdict.Acronym ?? string.Empty;
            (string en, string de) = settingName(verdict.Setting);

            switch (verdict.Reason)
            {
                case BanchosucksRankedModRules.Reason.ModSettings:
                    return ClickerStrings.Text("no pp on Banchosucks: {0} only with default settings", "keine pp auf Banchosucks: {0} nur mit Standardwerten", acronym);

                case BanchosucksRankedModRules.Reason.SettingOutOfRange:
                    return ClickerStrings.Text("no pp on Banchosucks: {0} {1} only from {2} to {3}", "keine pp auf Banchosucks: {0} {4} nur von {2} bis {3}",
                        acronym, en, verdict.Minimum, verdict.Maximum, de);

                case BanchosucksRankedModRules.Reason.SettingNotAllowed:
                    return ClickerStrings.Text("no pp on Banchosucks: {0} with {1}", "keine pp auf Banchosucks: {0} mit {2}", acronym, en, de);

                case BanchosucksRankedModRules.Reason.ManiaMod:
                    return ClickerStrings.Text("no pp on Banchosucks: {0} in osu!mania", "keine pp auf Banchosucks: {0} in osu!mania", acronym);

                case BanchosucksRankedModRules.Reason.ManiaConvert:
                    return ClickerStrings.Text("no pp on Banchosucks: converted map in osu!mania", "keine pp auf Banchosucks: konvertierte Map in osu!mania");

                default:
                    return ClickerStrings.Text("no pp on Banchosucks: mod {0}", "keine pp auf Banchosucks: Mod {0}", acronym);
            }
        }

        /// <summary>
        /// The longer sentence for the tooltip.
        /// </summary>
        public static LocalisableString Explain(BanchosucksRankedModRules.Verdict verdict)
        {
            if (verdict.GivesPerformance)
            {
                return ClickerStrings.Text("With these mods a play gives pp on Banchosucks, as long as the map itself does.",
                    "Mit diesen Mods gibt ein Play auf Banchosucks pp, sofern die Map selbst pp gibt.");
            }

            return LocalisableString.Interpolate($"{Describe(verdict)}. {ClickerStrings.Text("Only the mods are judged here, not the map.", "Geprüft werden hier nur die Mods, nicht die Map.")}");
        }

        private static (string en, string de) settingName(string? setting)
        {
            switch (setting)
            {
                case "circle_size":
                    return ("circle size", "Circle Size");

                case "approach_rate":
                    return ("approach rate", "Approach Rate");

                case "overall_difficulty":
                    return ("accuracy (OD)", "Genauigkeit (OD)");

                case "drain_rate":
                    return ("HP drain", "HP-Drain");

                case "scroll_speed":
                    return ("scroll speed", "Scroll-Tempo");

                case "speed_change":
                    return ("speed", "Tempo");

                case null:
                    return (string.Empty, string.Empty);

                default:
                    // a setting of another ruleset or a new one: show its wire name rather than nothing
                    return (setting, setting);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: decides whether two osu!standard replays can be shown on one playfield (replay comparison: the
    /// second replay is drawn as a ghost cursor over the first one).
    /// </summary>
    /// <remarks>
    /// The ghost is only a cursor moving over the map as the watched score plays it. That is only meaningful when
    /// both scores were played on the same playfield at the same speed, so mods that move, resize or re-time the
    /// objects must be the same in both scores. Mods that only change what is seen, how the play is judged or
    /// when it fails (HD, FL, NF, SD, PF, CL, RX, AP, SO, TD, ...) may differ.
    /// Pure: works on acronyms and settings, no ruleset needed.
    /// </remarks>
    public static class BanchosucksReplayComparison
    {
        /// <summary>
        /// The only ruleset replays can be compared in.
        /// </summary>
        public const string SUPPORTED_RULESET = "osu";

        /// <summary>
        /// Mods that change where objects are, how large they are or how they move. Each must be in both scores or
        /// in neither, with the same settings.
        /// </summary>
        private static readonly string[] layout_mods = { "HR", "EZ", "MR", "DA", "RD", "TP", "DP", "WG", "TR" };

        /// <summary>
        /// Mods that place objects from a seed. Without the seed in the score nobody can tell whether two plays saw the same map.
        /// </summary>
        private static readonly string[] seeded_mods = { "RD", "TP" };

        /// <summary>
        /// Mods whose effect depends on how the play went (the rate follows the hits, objects follow the cursor),
        /// so two plays never see the same thing.
        /// </summary>
        private static readonly string[] unpredictable_mods = { "AS", "MG", "RP" };

        /// <summary>
        /// Mods that change the speed of the map. The speed (and for the ramping ones the whole setting) must be the same.
        /// </summary>
        private static readonly Dictionary<string, double> constant_rate_mods = new Dictionary<string, double>
        {
            ["DT"] = 1.5,
            ["NC"] = 1.5,
            ["HT"] = 0.75,
            ["DC"] = 0.75,
        };

        private static readonly string[] ramping_rate_mods = { "WU", "WD" };

        /// <summary>
        /// One mod of a score: its acronym and its settings as text.
        /// </summary>
        public readonly record struct ModSummary(string Acronym, IReadOnlyDictionary<string, string> Settings)
        {
            public ModSummary(string acronym)
                : this(acronym, new Dictionary<string, string>())
            {
            }

            public ModSummary(Mod mod)
                : this(new APIMod(mod))
            {
            }

            public ModSummary(APIMod mod)
                : this(mod.Acronym, mod.Settings.ToDictionary(s => s.Key, s => Convert.ToString(s.Value, CultureInfo.InvariantCulture) ?? string.Empty))
            {
            }
        }

        public enum Verdict
        {
            /// <summary>
            /// The two replays fit on one playfield.
            /// </summary>
            Compatible,

            /// <summary>
            /// One of the scores is not an osu!standard score.
            /// </summary>
            UnsupportedRuleset,

            /// <summary>
            /// The scores were set on different beatmaps (or different versions of one).
            /// </summary>
            DifferentBeatmap,

            /// <summary>
            /// A mod that changes the playfield is in one score only.
            /// </summary>
            ModInOneScoreOnly,

            /// <summary>
            /// A mod that changes the playfield is in both scores, but set up differently.
            /// </summary>
            ModSettingsDiffer,

            /// <summary>
            /// The scores were played at different speeds.
            /// </summary>
            RateDiffers,

            /// <summary>
            /// A mod makes the map depend on the play itself (or on a seed the score does not carry).
            /// </summary>
            UnpredictableMod,
        }

        /// <param name="Verdict">Whether the comparison is possible, and if not, why.</param>
        /// <param name="Acronym">The mod in the way, when a mod is the reason.</param>
        public readonly record struct Result(Verdict Verdict, string? Acronym = null)
        {
            public bool Compatible => Verdict == Verdict.Compatible;
        }

        /// <summary>
        /// Checks two scores.
        /// </summary>
        /// <param name="watched">The score whose replay is played normally.</param>
        /// <param name="ghost">The score shown as a ghost cursor.</param>
        public static Result Check(ScoreInfo watched, ScoreInfo ghost)
        {
            // a score set on an older version of the map does not fit the map as it is now
            if (isFromOtherVersion(watched) || isFromOtherVersion(ghost))
                return new Result(Verdict.DifferentBeatmap);

            return Check(
                watched.Ruleset.ShortName, ghost.Ruleset.ShortName,
                watched.BeatmapInfo?.MD5Hash ?? string.Empty, ghost.BeatmapInfo?.MD5Hash ?? string.Empty,
                watched.BeatmapInfo?.OnlineID ?? -1, ghost.BeatmapInfo?.OnlineID ?? -1,
                watched.Mods.Select(m => new ModSummary(m)).ToArray(),
                ghost.Mods.Select(m => new ModSummary(m)).ToArray());
        }

        /// <summary>
        /// Local scores remember the hash of the beatmap they were set on; when the map was updated since, it differs.
        /// </summary>
        private static bool isFromOtherVersion(ScoreInfo score) =>
            !string.IsNullOrEmpty(score.BeatmapHash) && !string.IsNullOrEmpty(score.BeatmapInfo?.Hash) && score.BeatmapHash != score.BeatmapInfo.Hash;

        public static Result Check(string watchedRuleset, string ghostRuleset, string watchedBeatmapHash, string ghostBeatmapHash, int watchedBeatmapId, int ghostBeatmapId,
                                   IReadOnlyList<ModSummary> watchedMods, IReadOnlyList<ModSummary> ghostMods)
        {
            if (watchedRuleset != SUPPORTED_RULESET || ghostRuleset != SUPPORTED_RULESET)
                return new Result(Verdict.UnsupportedRuleset);

            // hashes when both are known (they also tell versions of a map apart), online ids otherwise
            if (!string.IsNullOrEmpty(watchedBeatmapHash) && !string.IsNullOrEmpty(ghostBeatmapHash))
            {
                if (watchedBeatmapHash != ghostBeatmapHash)
                    return new Result(Verdict.DifferentBeatmap);
            }
            else if (watchedBeatmapId <= 0 || watchedBeatmapId != ghostBeatmapId)
                return new Result(Verdict.DifferentBeatmap);

            return CheckMods(watchedMods, ghostMods);
        }

        /// <summary>
        /// Checks only the mods of two scores on the same beatmap.
        /// </summary>
        public static Result CheckMods(IReadOnlyList<ModSummary> watchedMods, IReadOnlyList<ModSummary> ghostMods)
        {
            foreach (string acronym in unpredictable_mods)
            {
                if (has(watchedMods, acronym) || has(ghostMods, acronym))
                    return new Result(Verdict.UnpredictableMod, acronym);
            }

            foreach (string acronym in layout_mods)
            {
                ModSummary? inWatched = find(watchedMods, acronym);
                ModSummary? inGhost = find(ghostMods, acronym);

                if (inWatched == null && inGhost == null)
                    continue;

                if (inWatched == null || inGhost == null)
                    return new Result(Verdict.ModInOneScoreOnly, acronym);

                if (seeded_mods.Contains(acronym) && (!hasSeed(inWatched.Value) || !hasSeed(inGhost.Value)))
                    return new Result(Verdict.UnpredictableMod, acronym);

                if (!sameSettings(inWatched.Value, inGhost.Value))
                    return new Result(Verdict.ModSettingsDiffer, acronym);
            }

            foreach (string acronym in ramping_rate_mods)
            {
                ModSummary? inWatched = find(watchedMods, acronym);
                ModSummary? inGhost = find(ghostMods, acronym);

                if (inWatched == null && inGhost == null)
                    continue;

                if (inWatched == null || inGhost == null || !sameSettings(inWatched.Value, inGhost.Value))
                    return new Result(Verdict.RateDiffers, acronym);
            }

            if (Math.Abs(Rate(watchedMods) - Rate(ghostMods)) > 0.0001)
                return new Result(Verdict.RateDiffers, rateAcronym(watchedMods) ?? rateAcronym(ghostMods));

            return new Result(Verdict.Compatible);
        }

        /// <summary>
        /// The constant speed a score was played at: 1 without rate mods, 1.5 with DT / NC, 0.75 with HT / DC, or
        /// what their speed setting says.
        /// </summary>
        public static double Rate(IReadOnlyList<ModSummary> mods)
        {
            double rate = 1;

            foreach (var mod in mods)
            {
                if (!constant_rate_mods.TryGetValue(mod.Acronym, out double defaultRate))
                    continue;

                if (mod.Settings.TryGetValue("speed_change", out string? text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double set))
                    rate *= set;
                else
                    rate *= defaultRate;
            }

            return rate;
        }

        private static string? rateAcronym(IReadOnlyList<ModSummary> mods)
        {
            foreach (var mod in mods)
            {
                if (constant_rate_mods.ContainsKey(mod.Acronym))
                    return mod.Acronym;
            }

            return null;
        }

        private static bool has(IReadOnlyList<ModSummary> mods, string acronym) => find(mods, acronym) != null;

        private static ModSummary? find(IReadOnlyList<ModSummary> mods, string acronym)
        {
            foreach (var mod in mods)
            {
                if (mod.Acronym == acronym)
                    return mod;
            }

            return null;
        }

        private static bool hasSeed(ModSummary mod) => mod.Settings.TryGetValue("seed", out string? seed) && !string.IsNullOrEmpty(seed);

        private static bool sameSettings(ModSummary a, ModSummary b)
        {
            if (a.Settings.Count != b.Settings.Count)
                return false;

            foreach (var (key, value) in a.Settings)
            {
                if (!b.Settings.TryGetValue(key, out string? other) || !sameValue(value, other))
                    return false;
            }

            return true;
        }

        private static bool sameValue(string a, string b)
        {
            if (a == b)
                return true;

            // "1.5" and "1.50" are the same setting
            return double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                   && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                   && Math.Abs(x - y) < 0.0001;
        }
    }
}

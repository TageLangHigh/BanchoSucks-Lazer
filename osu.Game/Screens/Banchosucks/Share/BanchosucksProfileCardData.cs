// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Extensions;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Users;
using osu.Game.Utils;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: everything the profile card shows, worked out from what the profile overlay has loaded: the
    /// user with the statistics of the shown ruleset, and the first entry of the "best performance" list.
    /// </summary>
    public class BanchosucksProfileCardData
    {
        /// <summary>
        /// Shown in place of a value the server did not send (an unranked player has no rank).
        /// </summary>
        public const string UNKNOWN = "-";

        public string Username { get; private init; } = string.Empty;
        public CountryCode CountryCode { get; private init; }
        public string CountryName { get; private init; } = string.Empty;
        public string RulesetName { get; private init; } = string.Empty;

        /// <summary>
        /// "#1,234", or <see cref="UNKNOWN"/>.
        /// </summary>
        public string GlobalRankText { get; private init; } = UNKNOWN;

        public string CountryRankText { get; private init; } = UNKNOWN;

        /// <summary>
        /// "5,432" (the label says pp), or <see cref="UNKNOWN"/>.
        /// </summary>
        public string PpText { get; private init; } = UNKNOWN;

        public string AccuracyText { get; private init; } = UNKNOWN;
        public string PlayCountText { get; private init; } = UNKNOWN;
        public string LevelText { get; private init; } = UNKNOWN;

        /// <summary>
        /// "45%" on the way to the next level, empty when unknown.
        /// </summary>
        public string LevelProgressText { get; private init; } = string.Empty;

        /// <summary>
        /// "Artist - Title [Difficulty]" of the best play, null when the list was not loaded or is empty.
        /// </summary>
        public string? BestTitle { get; private init; }

        /// <summary>
        /// "523pp" of the best play, empty when it has no pp.
        /// </summary>
        public string BestPpText { get; private init; } = string.Empty;

        /// <summary>
        /// Where the profile overlay got the avatar from; empty for none (the card then shows the guest avatar).
        /// </summary>
        public string AvatarUrl { get; private init; } = string.Empty;

        public string DateText { get; private init; } = string.Empty;

        public BanchosucksCardLabels Labels { get; private init; } = BanchosucksCardLabels.ENGLISH;

        public string FileName { get; private init; } = string.Empty;

        /// <param name="user">The user as loaded by the profile overlay (statistics of the shown ruleset included).</param>
        /// <param name="ruleset">The ruleset the profile is shown with.</param>
        /// <param name="best">The player's best play in that ruleset, if the profile's score list has delivered it.</param>
        /// <param name="german">Whether labels, numbers and the date are written the German way.</param>
        /// <param name="now">The date printed on the card (now by default).</param>
        public static BanchosucksProfileCardData From(APIUser user, IRulesetInfo ruleset, SoloScoreInfo? best = null, bool german = false, DateTimeOffset? now = null)
        {
            var culture = BanchosucksCardLabels.CultureFor(german);
            var statistics = user.Statistics;

            string rank(int? value) => value > 0 ? $"#{value.Value.ToString("N0", culture)}" : UNKNOWN;

            string? bestTitle = null;
            string bestPp = string.Empty;

            if (best?.Beatmap != null)
            {
                var set = best.Beatmap.BeatmapSet;
                string song = set == null ? string.Empty : $"{set.Artist} - {set.Title}";

                bestTitle = string.IsNullOrEmpty(song) ? $"[{best.Beatmap.DifficultyName}]" : $"{song} [{best.Beatmap.DifficultyName}]";

                if (best.PP is double pp)
                    bestPp = $"{Math.Round(pp, MidpointRounding.AwayFromZero).ToString("N0", culture)}pp";
            }

            return new BanchosucksProfileCardData
            {
                Username = user.Username,
                CountryCode = user.CountryCode,
                CountryName = user.CountryCode == CountryCode.Unknown ? string.Empty : user.CountryCode.GetDescription(),
                RulesetName = ruleset.Name,
                GlobalRankText = rank(statistics?.GlobalRank),
                CountryRankText = rank(statistics?.CountryRank),
                PpText = statistics?.PP is decimal totalPp ? Math.Round(totalPp, MidpointRounding.AwayFromZero).ToString("N0", culture) : UNKNOWN,
                // the statistics carry the accuracy in percent
                AccuracyText = statistics == null
                    ? UNKNOWN
                    : (statistics.Accuracy / 100).FloorToDecimalDigits(4).ToString("0.00%", culture).Replace(" ", string.Empty).Replace(" ", string.Empty),
                PlayCountText = statistics == null ? UNKNOWN : statistics.PlayCount.ToString("N0", culture),
                LevelText = statistics == null || statistics.Level.Current <= 0 ? UNKNOWN : statistics.Level.Current.ToString(culture),
                LevelProgressText = statistics == null || statistics.Level.Current <= 0 ? string.Empty : $"{statistics.Level.Progress}%",
                BestTitle = bestTitle,
                BestPpText = bestPp,
                AvatarUrl = user.AvatarUrl ?? string.Empty,
                DateText = (now ?? DateTimeOffset.Now).ToLocalTime().ToString(german ? "dd.MM.yyyy" : "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Labels = BanchosucksCardLabels.For(german),
                FileName = BanchosucksCardImage.SafeFileName($"{user.Username} - {ruleset.ShortName}"),
            };
        }
    }

    /// <summary>
    /// Banchosucks: remembers the best play the profile overlay's "best performance" list has shown for a player
    /// and ruleset, so the profile card can print it without asking the server again.
    /// </summary>
    public static class BanchosucksProfileBestScores
    {
        private const int max_entries = 64;

        private static readonly Dictionary<(int user, string ruleset), SoloScoreInfo?> known = new Dictionary<(int user, string ruleset), SoloScoreInfo?>();
        private static readonly Queue<(int user, string ruleset)> order = new Queue<(int user, string ruleset)>();

        /// <summary>
        /// Stores the first entry of a player's best plays (null: the list is empty).
        /// </summary>
        public static void Remember(int userId, string rulesetShortName, SoloScoreInfo? best)
        {
            lock (known)
            {
                var key = (userId, rulesetShortName);

                if (!known.ContainsKey(key))
                {
                    order.Enqueue(key);

                    while (order.Count > max_entries)
                        known.Remove(order.Dequeue());
                }

                known[key] = best;
            }
        }

        /// <summary>
        /// The best play last seen for a player and ruleset, if any.
        /// </summary>
        public static SoloScoreInfo? Get(int userId, string rulesetShortName)
        {
            lock (known)
                return known.GetValueOrDefault((userId, rulesetShortName));
        }

        /// <summary>
        /// Forgets everything (for tests).
        /// </summary>
        public static void Clear()
        {
            lock (known)
            {
                known.Clear();
                order.Clear();
            }
        }
    }
}

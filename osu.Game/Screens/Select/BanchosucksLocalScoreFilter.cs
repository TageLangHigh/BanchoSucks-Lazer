// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Banchosucks: the ids of the difficulties the player already has a local score on, for the song select filter
    /// <c>score=no</c> / <c>score=yes</c>.
    /// </summary>
    /// <remarks>
    /// The ids are loaded once, on the first lookup (the carousel filters on a background thread, so that is where the
    /// database is read), and then shared by every filter pass until the scores change. A filter pass over tens of
    /// thousands of difficulties therefore costs one hash lookup per difficulty.
    /// </remarks>
    public sealed class BanchosucksScoredBeatmaps
    {
        private readonly Lazy<HashSet<Guid>> ids;

        /// <param name="load">Returns the ids of all difficulties with a score. Called at most once, on the first lookup.</param>
        public BanchosucksScoredBeatmaps(Func<IEnumerable<Guid>> load)
        {
            ids = new Lazy<HashSet<Guid>>(() => new HashSet<Guid>(load()));
        }

        /// <summary>
        /// The number of difficulties with a score. Loads the ids if that has not happened yet.
        /// </summary>
        public int Count => ids.Value.Count;

        /// <summary>
        /// Whether the ids have been loaded already.
        /// </summary>
        public bool IsLoaded => ids.IsValueCreated;

        public bool Contains(Guid beatmapId) => ids.Value.Contains(beatmapId);

        /// <summary>
        /// Whether a difficulty passes the filter.
        /// </summary>
        /// <param name="beatmapId">The local id of the difficulty.</param>
        /// <param name="wantScore"><c>true</c> to keep only difficulties with a score, <c>false</c> to keep only those without one.</param>
        public bool Matches(Guid beatmapId, bool wantScore) => Contains(beatmapId) == wantScore;
    }

    /// <summary>
    /// Banchosucks: the search keyword of the "no score yet" filter, <c>score=no</c> and <c>score=yes</c>
    /// (also <c>scores</c>, <c>:</c> instead of <c>=</c>, <c>!=</c>, and the German values <c>nein</c> / <c>ja</c>).
    /// </summary>
    public static class BanchosucksLocalScoreQuery
    {
        /// <summary>
        /// What the toggle button writes into the search box.
        /// </summary>
        public const string NO_SCORE_QUERY = "score=no";

        // the same shape as the keyword syntax of FilterQueryParser, limited to this keyword.
        private static readonly Regex keyword_regex = new Regex(@"\b(?<key>\w+)(?<op>!?(:|=))(?<value>\S*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsKey(string key) => key == "score" || key == "scores";

        /// <summary>
        /// Parses the value of the keyword.
        /// </summary>
        /// <param name="op">The operator between keyword and value. Only "is" and "is not" make sense here.</param>
        /// <param name="value">The value as typed.</param>
        /// <param name="hasScore">Whether the player asked for difficulties with (<c>true</c>) or without (<c>false</c>) a score.</param>
        public static bool TryParse(Operator op, string value, out bool hasScore)
        {
            hasScore = false;

            if (op != Operator.Equal && op != Operator.NotEqual)
                return false;

            switch (value.ToLowerInvariant())
            {
                case "yes":
                case "ja":
                case "1":
                case "true":
                    hasScore = true;
                    break;

                case "no":
                case "nein":
                case "none":
                case "0":
                case "false":
                    hasScore = false;
                    break;

                default:
                    return false;
            }

            if (op == Operator.NotEqual)
                hasScore = !hasScore;

            return true;
        }

        /// <summary>
        /// What a search text asks for: <c>true</c> (only with a score), <c>false</c> (only without) or <c>null</c> (no such keyword).
        /// As in the parser, the last valid keyword wins.
        /// </summary>
        public static bool? GetState(string query)
        {
            bool? state = null;

            foreach (Match match in keyword_regex.Matches(query))
            {
                if (tryParseMatch(match, out bool hasScore))
                    state = hasScore;
            }

            return state;
        }

        /// <summary>
        /// Returns the search text with the "no score yet" filter switched on or off, leaving everything else as typed.
        /// </summary>
        public static string WithNoScore(string query, bool enabled)
        {
            string result = keyword_regex.Replace(query, match => tryParseMatch(match, out _) ? string.Empty : match.Value);

            result = Regex.Replace(result, @"\s{2,}", " ").Trim();

            if (!enabled)
                return result;

            return result.Length == 0 ? NO_SCORE_QUERY : $"{result} {NO_SCORE_QUERY}";
        }

        private static bool tryParseMatch(Match match, out bool hasScore)
        {
            hasScore = false;

            if (!IsKey(match.Groups["key"].Value.ToLowerInvariant()))
                return false;

            var op = match.Groups["op"].Value.StartsWith('!') ? Operator.NotEqual : Operator.Equal;
            return TryParse(op, match.Groups["value"].Value, out hasScore);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: answer of <c>GET /api/plugins/banchosucks_medals/catalogue</c>: every medal the server knows
    /// (osu!, g0v0 and Banchosucks), Banchosucks medals first, then by id.
    /// </summary>
    public class BanchosucksMedalCatalogueResponse
    {
        [JsonProperty("medals")]
        public List<BanchosucksCatalogueMedal> Medals { get; set; } = new List<BanchosucksCatalogueMedal>();
    }

    /// <summary>
    /// One medal of the catalogue. The plugin sends no condition numbers and no progress, only what the medal is.
    /// </summary>
    public class BanchosucksCatalogueMedal
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>The asset name; the client builds the image address from it (<see cref="Users.Medal.ImageUrl"/>).</summary>
        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        /// <summary>Whether the medal is one of ours (its slug starts with <c>banchosucks_</c>).</summary>
        [JsonProperty("banchosucks")]
        public bool Banchosucks { get; set; }
    }

    public class GetBanchosucksMedalCatalogueRequest : APIRequest<BanchosucksMedalCatalogueResponse>
    {
        protected override string Route => "plugins";

        protected override string Target => @"banchosucks_medals/catalogue";
    }

    /// <summary>
    /// Where a medal comes from. The catalogue has no grouping of its own beyond the <c>banchosucks</c> flag; the rest follows
    /// the slug prefixes the client already uses for the image address.
    /// </summary>
    public enum BanchosucksMedalGroup
    {
        Banchosucks,
        G0v0,
        Osu,
    }

    /// <summary>
    /// Banchosucks: the medal overview of one player: the catalogue grouped by origin with the player's medals marked. Pure.
    /// </summary>
    public class BanchosucksMedalOverview
    {
        public IReadOnlyList<Group> Groups { get; }

        /// <summary>Medals in the catalogue.</summary>
        public int Total { get; }

        /// <summary>Medals of the catalogue the player has.</summary>
        public int Achieved { get; }

        /// <summary>Medals the player has that the catalogue does not list (not shown, only counted).</summary>
        public int AchievedOutsideCatalogue { get; }

        /// <summary>False when the server sent no medal list for the player: nothing can be marked then, and no count is claimed.</summary>
        public bool AchievementsKnown { get; }

        private BanchosucksMedalOverview(IReadOnlyList<Group> groups, int total, int achieved, int outside, bool achievementsKnown)
        {
            Groups = groups;
            Total = total;
            Achieved = achieved;
            AchievedOutsideCatalogue = outside;
            AchievementsKnown = achievementsKnown;
        }

        public static BanchosucksMedalGroup GroupOf(BanchosucksCatalogueMedal medal)
        {
            if (medal.Banchosucks || medal.Slug.StartsWith(@"banchosucks_", StringComparison.Ordinal))
                return BanchosucksMedalGroup.Banchosucks;

            return medal.Slug.StartsWith(@"g0v0_", StringComparison.Ordinal) ? BanchosucksMedalGroup.G0v0 : BanchosucksMedalGroup.Osu;
        }

        /// <param name="catalogue">The server's catalogue.</param>
        /// <param name="achievements">The player's medals (id and time), or null if the server did not send them.</param>
        public static BanchosucksMedalOverview Build(IEnumerable<BanchosucksCatalogueMedal>? catalogue, IEnumerable<(int id, DateTimeOffset at)>? achievements)
        {
            var achievedAt = new Dictionary<int, DateTimeOffset>();

            if (achievements != null)
            {
                foreach (var (id, at) in achievements)
                    achievedAt[id] = at;
            }

            var seen = new HashSet<int>();
            var byGroup = new Dictionary<BanchosucksMedalGroup, List<Entry>>();
            int achieved = 0;

            foreach (var medal in catalogue ?? Enumerable.Empty<BanchosucksCatalogueMedal>())
            {
                if (!seen.Add(medal.Id))
                    continue;

                DateTimeOffset? at = achievedAt.TryGetValue(medal.Id, out var time) ? time : null;

                if (at != null)
                    achieved++;

                var group = GroupOf(medal);

                if (!byGroup.TryGetValue(group, out var entries))
                    byGroup[group] = entries = new List<Entry>();

                entries.Add(new Entry(medal, at));
            }

            // the player's medals first, the newest in front; the rest in the catalogue's order (OrderBy is stable)
            var groups = byGroup.OrderBy(g => g.Key)
                                .Select(g => new Group(g.Key, g.Value.OrderBy(e => e.Achieved ? 0 : 1).ThenByDescending(e => e.AchievedAt ?? DateTimeOffset.MinValue).ToList()))
                                .ToList();

            return new BanchosucksMedalOverview(groups, seen.Count, achieved, achievedAt.Keys.Count(id => !seen.Contains(id)), achievements != null);
        }

        public class Group
        {
            public BanchosucksMedalGroup Kind { get; }

            public IReadOnlyList<Entry> Medals { get; }

            public int Achieved { get; }

            public Group(BanchosucksMedalGroup kind, IReadOnlyList<Entry> medals)
            {
                Kind = kind;
                Medals = medals;
                Achieved = medals.Count(m => m.Achieved);
            }
        }

        /// <param name="Medal">The medal.</param>
        /// <param name="AchievedAt">When the player got it, or null.</param>
        public readonly record struct Entry(BanchosucksCatalogueMedal Medal, DateTimeOffset? AchievedAt)
        {
            public bool Achieved => AchievedAt != null;
        }
    }

    /// <summary>
    /// Banchosucks: keeps the medal catalogue for the session. Every profile that is opened would otherwise ask for the same
    /// list again; it only changes when the server gets new medals.
    /// </summary>
    public class BanchosucksMedalCatalogueCache
    {
        /// <summary>Milliseconds until the catalogue is asked for again.</summary>
        public const double REFRESH_INTERVAL = 30 * 60_000;

        /// <summary>The cache the game uses.</summary>
        public static readonly BanchosucksMedalCatalogueCache SHARED = new BanchosucksMedalCatalogueCache();

        private BanchosucksRefreshGate gate = new BanchosucksRefreshGate(REFRESH_INTERVAL);

        /// <summary>The catalogue, or null as long as the server has not answered.</summary>
        public IReadOnlyList<BanchosucksCatalogueMedal>? Medals { get; private set; }

        public bool LastFailed => gate.LastFailed;

        public bool InFlight => gate.InFlight;

        public bool ShouldRefresh(double now) => gate.ShouldRefresh(now);

        public void Started(double now) => gate.Started(now);

        /// <summary>
        /// Raised when a request for the catalogue ended, with or without an answer. Several lists can wait for the same request.
        /// </summary>
        public event Action? Changed;

        public void Succeeded(double now, IReadOnlyList<BanchosucksCatalogueMedal> medals)
        {
            gate.Succeeded(now);
            Medals = medals;
            Changed?.Invoke();
        }

        public void Failed()
        {
            gate.Failed();
            Changed?.Invoke();
        }

        /// <summary>Forgets everything (tests).</summary>
        public void Clear()
        {
            gate = new BanchosucksRefreshGate(REFRESH_INTERVAL);
            Medals = null;
        }
    }
}

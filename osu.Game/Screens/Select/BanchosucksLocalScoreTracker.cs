// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Banchosucks: keeps the ids of the difficulties with a local score for the song select filter <c>score=no</c> / <c>score=yes</c>.
    /// </summary>
    /// <remarks>
    /// "Score" means what the rank badge on a carousel panel and the "rank achieved" grouping show: a local score of the
    /// logged-in user (or of a guest, as everywhere in the client) in the selected ruleset that belongs to the current
    /// version of the difficulty and is not deleted. Nothing is asked from the server.
    /// The scores are only watched once the filter is used for the first time, so nobody pays for it otherwise.
    /// </remarks>
    public sealed class BanchosucksLocalScoreTracker : IDisposable
    {
        /// <summary>
        /// Fired on the update thread when a score was added or removed, after the cached ids were thrown away.
        /// </summary>
        public event Action? ScoresChanged;

        private readonly RealmAccess realm;

        private IDisposable? subscription;

        private BanchosucksScoredBeatmaps? cached;
        private int? cachedUserId;
        private string? cachedRuleset;

        public BanchosucksLocalScoreTracker(RealmAccess realm)
        {
            this.realm = realm;
        }

        /// <summary>
        /// The difficulties the given user has a score on in the given ruleset. Must be called from the update thread;
        /// the returned object may be used from any thread and reads the database on its first lookup.
        /// </summary>
        public BanchosucksScoredBeatmaps Get(int? userId, string rulesetShortName)
        {
            subscription ??= realm.RegisterForNotifications(r => r.All<ScoreInfo>().Filter(
                $@"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.Hash)} == {nameof(ScoreInfo.BeatmapHash)}"
                + $@" && {nameof(ScoreInfo.DeletePending)} == false"), scoresChanged);

            if (cached == null || cachedUserId != userId || cachedRuleset != rulesetShortName)
            {
                cachedUserId = userId;
                cachedRuleset = rulesetShortName;
                cached = new BanchosucksScoredBeatmaps(() => Query(realm, userId, rulesetShortName));
            }

            return cached;
        }

        private void scoresChanged(IRealmCollection<ScoreInfo> sender, ChangeSet? changes)
        {
            // the first callback only announces the subscription. Modifications alone come from changes to the linked
            // beatmaps (last played, star rating); a new, deleted or restored score always changes the collection.
            if (changes == null || !changes.HasCollectionChanges())
                return;

            cached = null;
            ScoresChanged?.Invoke();
        }

        /// <summary>
        /// Reads the ids of all difficulties the user has a score on. One pass over the user's scores, usable from any thread.
        /// </summary>
        public static HashSet<Guid> Query(RealmAccess realm, int? userId, string rulesetShortName)
        {
            try
            {
                return realm.Run(r =>
                {
                    var ids = new HashSet<Guid>();

                    var scores = r.GetAllLocalScoresForUser(userId)
                                  .Filter($@"{nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0", rulesetShortName);

                    foreach (var score in scores)
                    {
                        if (score.BeatmapInfo != null)
                            ids.Add(score.BeatmapInfo.ID);
                    }

                    return ids;
                });
            }
            catch (Exception e)
            {
                // without the ids the filter treats every difficulty as unplayed, which is better than a carousel that never finishes loading.
                Logger.Error(e, "Banchosucks: could not read the local scores for the song select filter");
                return new HashSet<Guid>();
            }
        }

        public void Dispose()
        {
            subscription?.Dispose();
            subscription = null;
        }
    }
}

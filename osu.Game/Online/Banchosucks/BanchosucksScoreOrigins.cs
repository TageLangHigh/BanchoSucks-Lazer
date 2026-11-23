// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: answer of <c>GET /api/plugins/banchosucks_stablescores/origin/{lazer_score_id}</c>.
    /// </summary>
    public class BanchosucksScoreOriginResponse
    {
        /// <summary>The stable score the lazer score was imported from, or null for a play made in lazer.</summary>
        [JsonProperty("stable_score_id")]
        public long? StableScoreId { get; set; }
    }

    /// <summary>
    /// Banchosucks: asks the plugin <c>banchosucks_stablescores</c> whether one lazer score was imported from the stable server.
    /// Public on the server, one database query, nothing about the player is sent beyond the score id.
    /// </summary>
    public class GetBanchosucksScoreOriginRequest : APIRequest<BanchosucksScoreOriginResponse>
    {
        public readonly long ScoreId;

        public GetBanchosucksScoreOriginRequest(long scoreId)
        {
            ScoreId = scoreId;
        }

        protected override string Route => "plugins";

        protected override string Target => $@"banchosucks_stablescores/origin/{ScoreId}";
    }

    /// <summary>
    /// Banchosucks: tells score displays whether a score came from the stable server. A component of <see cref="OsuGame"/>;
    /// the rules (cache, pace, retries) are in <see cref="BanchosucksScoreOriginCache"/>, this class only sends what it hands out.
    /// </summary>
    /// <remarks>
    /// The lookup is one request per score because the plugin has no batch endpoint. Everything that asks goes through
    /// <see cref="Request"/>, so a batch endpoint (<c>GET /origin?ids=1,2,3</c>) would only change <see cref="Update"/>.
    /// </remarks>
    public partial class BanchosucksScoreOrigins : Component
    {
        private readonly BanchosucksScoreOriginCache cache = new BanchosucksScoreOriginCache();

        // how many displays wait for each score that was asked for
        private readonly Dictionary<long, int> watchers = new Dictionary<long, int>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? playInfo { get; set; }

        private IBindable<LocalUserPlayingState>? playingState;

        /// <summary>
        /// Raised on the update thread when a score's origin became known: (lazer score id, imported from stable).
        /// </summary>
        public event Action<long, bool>? Resolved;

        /// <summary>The cache (tests).</summary>
        public BanchosucksScoreOriginCache Cache => cache;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            playingState = playInfo?.PlayingState.GetBoundCopy();
        }

        /// <summary>
        /// What is known about a score right now, without asking the server: true = from stable, false = made in lazer, null = unknown.
        /// Scores that cannot be imports (no online id, no Classic mod, changed mod settings) are answered on the spot.
        /// </summary>
        public bool? Lookup(ScoreInfo score)
        {
            long id = score.OnlineID;

            if (cache.Lookup(id) is bool known)
                return known;

            if (!canBeStableImport(score))
                return false;

            return null;
        }

        /// <summary>The stable score id behind an imported score, if known.</summary>
        public long? StableScoreId(ScoreInfo score) => cache.StableScoreId(score.OnlineID);

        /// <summary>
        /// Asks for a score's origin. The answer arrives through <see cref="Resolved"/>; nothing happens if it is already known or cannot be an import.
        /// Call only for scores that are on screen, and <see cref="Withdraw"/> them when they leave.
        /// </summary>
        public void Request(ScoreInfo score)
        {
            if (Lookup(score) != null)
                return;

            long id = score.OnlineID;

            // the same score can be on screen twice (the list and the "personal best" row)
            watchers[id] = watchers.GetValueOrDefault(id) + 1;
            cache.Request(id, Time.Current);
        }

        /// <summary>
        /// <see cref="Withdraw"/> from any thread (drawables are disposed off the update thread).
        /// </summary>
        public void WithdrawLater(ScoreInfo score) => Schedule(() => Withdraw(score));

        /// <summary>
        /// The score is no longer on screen: a lookup that has not been sent yet is dropped.
        /// </summary>
        public void Withdraw(ScoreInfo score)
        {
            long id = score.OnlineID;

            if (!watchers.TryGetValue(id, out int count))
                return;

            if (count > 1)
            {
                watchers[id] = count - 1;
                return;
            }

            watchers.Remove(id);
            cache.Withdraw(id);
        }

        private static bool canBeStableImport(ScoreInfo score)
        {
            Mod[] mods;

            try
            {
                mods = score.Mods;
            }
            catch (Exception)
            {
                // mods of a ruleset this client does not have: cannot tell, and such a score is not shown with a badge anyway
                return false;
            }

            return BanchosucksScoreOriginCache.CanBeStableImport(score.OnlineID, mods.Any(m => m is ModClassic), mods.All(m => m.UsesDefaultConfiguration));
        }

        protected override void Update()
        {
            base.Update();

            // nothing during gameplay, nothing while logged out (the client sends no api requests then)
            bool allowed = api.State.Value == APIState.Online
                           && (playingState == null || playingState.Value == LocalUserPlayingState.NotPlaying);

            if (cache.Next(Time.Current, allowed) is not long scoreId)
                return;

            var request = new GetBanchosucksScoreOriginRequest(scoreId);

            request.Success += response => Schedule(() =>
            {
                cache.Complete(scoreId, response.StableScoreId);
                watchers.Remove(scoreId);
                Resolved?.Invoke(scoreId, response.StableScoreId != null);
            });
            request.Failure += _ => Schedule(() =>
            {
                cache.Fail(scoreId, Time.Current);
                watchers.Remove(scoreId);
            });

            api.Queue(request);
        }
    }
}

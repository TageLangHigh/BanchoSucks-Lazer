// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.Chat
{
    /// <summary>
    /// Banchosucks: looks up the beatmap sets behind beatmap links in chat, for the preview cards
    /// (<c>BanchosucksChatBeatmapPreview</c>).
    /// </summary>
    /// <remarks>
    /// Asks our own API only, with the request the client already uses everywhere (<see cref="GetBeatmapSetRequest"/>).
    /// Every set is asked once and kept for the session; <see cref="BanchosucksLookupThrottle{TKey}"/> limits how
    /// many requests go out. Who is asked for what is decided by the caller: the preview only asks for messages
    /// that are on screen.
    /// </remarks>
    public partial class BanchosucksChatBeatmapLookup : Component
    {
        /// <summary>
        /// Above this many sets everything is dropped and looked up again when needed.
        /// </summary>
        private const int max_cached_sets = 300;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly BanchosucksLookupThrottle<BanchosucksChatBeatmapLink> throttle = new BanchosucksLookupThrottle<BanchosucksChatBeatmapLink>();

        private readonly Dictionary<int, APIBeatmapSet> sets = new Dictionary<int, APIBeatmapSet>();
        private readonly Dictionary<int, int> setOfBeatmap = new Dictionary<int, int>();

        private readonly Dictionary<BanchosucksChatBeatmapLink, List<Action<APIBeatmapSet>>> waiting = new Dictionary<BanchosucksChatBeatmapLink, List<Action<APIBeatmapSet>>>();

        /// <summary>
        /// How many requests were sent to the server so far (for tests).
        /// </summary>
        public int RequestsSent { get; private set; }

        /// <summary>
        /// The set a link points at, if it was looked up before.
        /// </summary>
        public APIBeatmapSet? GetCached(BanchosucksChatBeatmapLink link)
        {
            if (link.IsSet)
                return sets.GetValueOrDefault(link.Id);

            return setOfBeatmap.TryGetValue(link.Id, out int setId) ? sets.GetValueOrDefault(setId) : null;
        }

        /// <summary>
        /// Asks for the set behind a link. Must be called from the update thread.
        /// </summary>
        /// <param name="link">The link.</param>
        /// <param name="callback">
        /// Called on the update thread once the set is known: right away when it is cached, later otherwise.
        /// Not called at all when the lookup fails (the set does not exist, or the server cannot be reached).
        /// </param>
        public void Lookup(BanchosucksChatBeatmapLink link, Action<APIBeatmapSet> callback)
        {
            var cached = GetCached(link);

            if (cached != null)
            {
                callback(cached);
                return;
            }

            if (!throttle.Request(link, Time.Current))
                return;

            if (!waiting.TryGetValue(link, out var callbacks))
                waiting[link] = callbacks = new List<Action<APIBeatmapSet>>();

            callbacks.Add(callback);
        }

        /// <summary>
        /// Withdraws a callback (the message left the screen or is gone). A lookup nobody waits for is not sent.
        /// </summary>
        public void Cancel(BanchosucksChatBeatmapLink link, Action<APIBeatmapSet> callback)
        {
            if (!waiting.TryGetValue(link, out var callbacks))
                return;

            callbacks.Remove(callback);

            if (callbacks.Count == 0)
            {
                waiting.Remove(link);
                throttle.Cancel(link);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (api.State.Value != APIState.Online)
                return;

            if (!throttle.TryTake(Time.Current, out var link))
                return;

            var request = new GetBeatmapSetRequest(link.Id, link.IsSet ? BeatmapSetLookupType.SetId : BeatmapSetLookupType.BeatmapId);

            request.Success += set => Schedule(() => succeeded(link, set));
            request.Failure += _ => Schedule(() => failed(link));

            RequestsSent++;
            api.Queue(request);
        }

        private void succeeded(BanchosucksChatBeatmapLink link, APIBeatmapSet set)
        {
            throttle.Succeed(link);

            if (set.OnlineID <= 0)
            {
                waiting.Remove(link);
                return;
            }

            if (sets.Count >= max_cached_sets)
                dropCache();

            sets[set.OnlineID] = set;

            // everything this answer also answers: the set itself and each of its difficulties
            var answered = new List<BanchosucksChatBeatmapLink> { link, new BanchosucksChatBeatmapLink(true, set.OnlineID) };

            foreach (var beatmap in set.Beatmaps.Where(b => b.OnlineID > 0))
            {
                setOfBeatmap[beatmap.OnlineID] = set.OnlineID;
                answered.Add(new BanchosucksChatBeatmapLink(false, beatmap.OnlineID));
            }

            // a difficulty the set does not list (anymore) still got its answer
            if (!link.IsSet)
                setOfBeatmap[link.Id] = set.OnlineID;

            foreach (var key in answered.Distinct())
            {
                throttle.MarkKnown(key);

                if (!waiting.Remove(key, out var callbacks))
                    continue;

                foreach (var callback in callbacks)
                    callback(set);
            }
        }

        private void failed(BanchosucksChatBeatmapLink link)
        {
            throttle.Fail(link, Time.Current);
            waiting.Remove(link);
        }

        private void dropCache()
        {
            foreach (int setId in sets.Keys)
                throttle.Forget(new BanchosucksChatBeatmapLink(true, setId));

            foreach (int beatmapId in setOfBeatmap.Keys)
                throttle.Forget(new BanchosucksChatBeatmapLink(false, beatmapId));

            sets.Clear();
            setOfBeatmap.Clear();
        }
    }
}

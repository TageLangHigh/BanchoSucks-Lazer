// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: downloads many beatmap sets through the game's normal download path (<see cref="BeatmapModelDownloader"/>,
    /// with its notifications and import), at most two at a time. A component of <see cref="OsuGame"/>, so a queue keeps
    /// running after the overlay that filled it was closed. Nothing is started during gameplay or while logged out.
    /// </summary>
    public partial class BanchosucksBeatmapDownloadQueue : Component
    {
        /// <summary>A download that vanished from the downloader is given this long for its failure event to arrive.</summary>
        private const double settle_time = 250;

        private readonly BanchosucksDownloadSlots slots = new BanchosucksDownloadSlots();

        // sets waiting for a slot
        private readonly Dictionary<int, IBeatmapSetInfo> waiting = new Dictionary<int, IBeatmapSetInfo>();

        // sets occupying a slot, with the time the downloader stopped listing them (null while it still does)
        private readonly Dictionary<int, (IBeatmapSetInfo model, double? goneSince)> active = new Dictionary<int, (IBeatmapSetInfo, double?)>();

        // filled from the downloader's event, which is raised on other threads
        private readonly ConcurrentDictionary<int, byte> failed = new ConcurrentDictionary<int, byte>();

        [Resolved]
        private BeatmapModelDownloader downloader { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? playInfo { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private IBindable<LocalUserPlayingState>? playingState;
        private Bindable<bool> preferNoVideo = null!;

        private bool reportPending;

        /// <summary>The slots (tests).</summary>
        public BanchosucksDownloadSlots Slots => slots;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            playingState = playInfo?.PlayingState.GetBoundCopy();
            preferNoVideo = config.GetBindable<bool>(OsuSetting.PreferNoVideo);
            downloader.DownloadFailed += onDownloadFailed;
        }

        /// <summary>
        /// Whether a set waits here, is being downloaded by this queue, or is being downloaded by anything else in the game.
        /// </summary>
        public bool IsBusy(int setId) => slots.Contains(setId) || downloader.GetExistingDownload(new APIBeatmapSet { OnlineID = setId }) != null;

        /// <summary>
        /// Queues sets for download and returns how many were new. Sets already downloading or waiting are skipped.
        /// </summary>
        public int Enqueue(IEnumerable<IBeatmapSetInfo> sets)
        {
            var wanted = sets.Where(s => s.OnlineID > 0 && !IsBusy(s.OnlineID)).ToList();

            foreach (var set in wanted)
                waiting[set.OnlineID] = set;

            int added = slots.Enqueue(wanted.Select(s => s.OnlineID));

            if (added > 0)
                reportPending = true;

            return added;
        }

        private void onDownloadFailed(ArchiveDownloadRequest<IBeatmapSetInfo> request) => failed[request.Model.OnlineID] = 0;

        protected override void Update()
        {
            base.Update();

            checkActive();

            bool allowed = api.State.Value == APIState.Online
                           && (playingState == null || playingState.Value == LocalUserPlayingState.NotPlaying);

            while (allowed && slots.Next() is int setId)
                start(setId);

            if (reportPending && slots.Active == 0 && slots.Waiting == 0)
            {
                reportPending = false;
                waiting.Clear();
                report();
            }
        }

        private void start(int setId)
        {
            var model = waiting[setId];

            waiting.Remove(setId);
            failed.TryRemove(setId, out _);
            active[setId] = (model, null);

            // started by hand in the meantime: let that download run, it frees the slot when it is gone
            if (downloader.GetExistingDownload(model) != null)
                return;

            if (!downloader.Download(model, preferNoVideo.Value))
                finish(setId, false);
        }

        /// <summary>
        /// A download is over when the downloader no longer lists it: it removes a request when it failed or once the import is done.
        /// </summary>
        private void checkActive()
        {
            if (active.Count == 0)
                return;

            foreach (int setId in active.Keys.ToArray())
            {
                var (model, goneSince) = active[setId];

                if (downloader.GetExistingDownload(model) != null)
                    continue;

                if (goneSince == null)
                    active[setId] = (model, Time.Current);
                else if (Time.Current - goneSince.Value >= settle_time || Time.Current < goneSince.Value)
                    finish(setId, !failed.ContainsKey(setId));
            }
        }

        private void finish(int setId, bool success)
        {
            active.Remove(setId);
            failed.TryRemove(setId, out _);
            slots.Finished(setId, success);
        }

        private void report()
        {
            // every finished download has its own notification already; only trouble needs a summary
            if (slots.Failed == 0 && slots.Dropped == 0)
                return;

            notifications?.Post(new SimpleNotification
            {
                Text = slots.Dropped > 0
                    ? ClickerStrings.Text("Map downloads stopped after repeated failures: {0} downloaded, {1} failed, {2} not tried. Try again later.",
                        "Map-Downloads nach wiederholten Fehlern gestoppt: {0} geladen, {1} fehlgeschlagen, {2} nicht versucht. Später noch einmal probieren.",
                        slots.Succeeded, slots.Failed, slots.Dropped)
                    : ClickerStrings.Text("Map downloads finished: {0} downloaded, {1} failed.", "Map-Downloads fertig: {0} geladen, {1} fehlgeschlagen.", slots.Succeeded, slots.Failed),
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (downloader.IsNotNull())
                downloader.DownloadFailed -= onDownloadFailed;

            base.Dispose(isDisposing);
        }
    }
}

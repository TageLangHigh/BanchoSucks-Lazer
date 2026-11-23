// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;
using osu.Game.Utils;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: the local activity log. Lives in <see cref="OsuGame"/>, records every finished play and is the one
    /// source of the daily goal (main menu) and the personal statistics screen.
    /// </summary>
    /// <remarks>
    /// Nothing here is sent anywhere: the log is a JSON file in the game's own storage
    /// (<c>banchosucks/activity-log.json</c>, previous version in <c>activity-log.bak.json</c>).
    /// <para>
    /// What counts as a play: a <see cref="SubmittingPlayer"/> (solo, multiplayer, playlists) without an autoplay mod.
    /// Replays, spectating, autoplay / cinema and the editor's test play are not that kind of screen and are ignored.
    /// A play is finished when its screen is left: to the results (passed), by failing or by quitting / retrying
    /// (not passed). Unfinished plays shorter than <see cref="BanchosucksActivityFile.MIN_SECONDS"/> are dropped.
    /// </para>
    /// <para>
    /// The duration is the time the player spent in <see cref="LocalUserPlayingState.Playing"/>: active gameplay in
    /// real time, without breaks, the lead-in, pauses and whatever follows a fail.
    /// </para>
    /// </remarks>
    public partial class BanchosucksActivityLog : Component
    {
        public const string LOG_FILE = "activity-log.json";
        public const string BACKUP_FILE = "activity-log.bak.json";

        // a frame longer than this is a stall or the PC waking up, not play time
        private const double max_frame_ms = 1000;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private RealmAccess? realm { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        /// <summary>
        /// The PC's local time. Replaced in tests to cross midnight.
        /// </summary>
        public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.Now;

        /// <summary>
        /// Raised on the update thread whenever the entries, the goal or the met days changed.
        /// </summary>
        public event Action? Changed;

        public IReadOnlyList<BanchosucksActivityEntry> Entries => file.Entries;

        public IReadOnlyList<DateOnly> GoalDays => file.GoalDays;

        /// <summary>
        /// When the log was started; days before it only hold estimated entries.
        /// </summary>
        public DateTimeOffset Created => file.Created;

        public DateOnly Today => DateOnly.FromDateTime(Now().DateTime);

        /// <summary>
        /// Today's progress. Without a goal (also while this component is still loading) it is simply not <see cref="BanchosucksDailyGoalProgress.Enabled"/>.
        /// </summary>
        public BanchosucksDailyGoalProgress TodayProgress => BanchosucksDailyGoal.ProgressOn(file.Entries, Today, goalMinutes?.Value ?? 0, goalMaps?.Value ?? 0);

        public int Streak => BanchosucksDailyGoal.Streak(file.GoalDays, Today);

        private BanchosucksActivityFile file = new BanchosucksActivityFile();
        private Storage logStorage = null!;

        // null until loaded; the main menu can be up before this component is
        private Bindable<int>? goalMinutes;
        private Bindable<int>? goalMaps;
        private IBindable<APIState> apiState = null!;

        private Player? tracked;
        private double trackedMs;
        private WeakReference<Player>? finished;

        private readonly object saveLock = new object();
        private Task saveTask = Task.CompletedTask;

        [BackgroundDependencyLoader]
        private void load()
        {
            logStorage = storage.GetStorageForDirectory("banchosucks");

            goalMinutes = config.GetBindable<int>(OsuSetting.BanchosucksDailyGoalMinutes);
            goalMaps = config.GetBindable<int>(OsuSetting.BanchosucksDailyGoalMaps);

            var loaded = BanchosucksActivityFile.Read(logStorage.GetFullPath(LOG_FILE)) ?? BanchosucksActivityFile.Read(logStorage.GetFullPath(BACKUP_FILE));

            if (loaded != null)
            {
                file = loaded;

                if (file.ReadOnly)
                    Logger.Log($"Activity log was written by a newer client (version {file.Version}); it is shown but not changed by this one.");
            }
            else
                file = new BanchosucksActivityFile { Created = Now() };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            goalMinutes!.BindValueChanged(_ => Changed?.Invoke());
            goalMaps!.BindValueChanged(_ => Changed?.Invoke());

            // whoever resolved this component before it was loaded saw an empty log without a goal
            Changed?.Invoke();

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(state =>
            {
                if (state.NewValue == APIState.Online)
                    Schedule(backfill);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (tracked != null && tracked.PlayingState.Value == LocalUserPlayingState.Playing)
                trackedMs += Math.Clamp(Time.Elapsed, 0, max_frame_ms);
        }

        // ------------------------------------------------------------------ recording

        /// <summary>
        /// Called by <see cref="OsuGame"/> on every screen change; starts and ends the timing of a play.
        /// </summary>
        public void ScreenChanged(IScreen? last, IScreen? next)
        {
            if (tracked != null && !ReferenceEquals(next, tracked))
                finishTracked();

            if (next is Player player && tracked == null && IsRealPlay(player) && !alreadyFinished(player))
            {
                tracked = player;
                trackedMs = 0;
            }
        }

        /// <summary>
        /// Whether a player screen is the local user actually playing (see the rules on the class).
        /// </summary>
        public static bool IsRealPlay(Player player)
        {
            if (player is not SubmittingPlayer)
                return false;

            // a play whose beatmap failed to load never started
            if (player.GameplayState == null)
                return false;

            return !player.GameplayState.Mods.Any(m => m is ModAutoplay);
        }

        private bool alreadyFinished(Player player) => finished != null && finished.TryGetTarget(out var target) && ReferenceEquals(target, player);

        private void finishTracked()
        {
            var player = tracked!;
            tracked = null;
            // the results screen hands control back to the player screen for a moment when it closes; that is not a second play
            finished = new WeakReference<Player>(player);

            try
            {
                var state = player.GameplayState;

                if (state == null)
                    return;

                RecordPlay(trackedMs / 1000, state.Ruleset.RulesetInfo.ShortName, state.HasPassed, state.ScoreProcessor.Accuracy.Value);
            }
            catch (Exception e)
            {
                // the log must never get in the way of leaving a play
                Logger.Error(e, "Activity log: a finished play could not be recorded");
            }
        }

        /// <summary>
        /// Adds a play that ended now. Returns false when it was too short to be logged.
        /// </summary>
        public bool RecordPlay(double seconds, string ruleset, bool passed, double accuracy)
        {
            if (double.IsNaN(seconds) || seconds < 0)
                seconds = 0;

            if (!BanchosucksActivityFile.ShouldLog(seconds, passed))
                return false;

            DateTimeOffset now = Now();

            var entry = new BanchosucksActivityEntry
            {
                // whole seconds are plenty and keep the file small
                At = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Offset),
                Seconds = Math.Round(seconds, 1),
                Ruleset = ruleset,
                Passed = passed,
                Accuracy = double.IsNaN(accuracy) ? 0 : Math.Round(Math.Clamp(accuracy, 0, 1), 4),
            };

            file.Add(entry);
            file.Prune(entry.Day);

            checkGoal(entry.Day);

            save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Marks the day as met the first time its progress reaches the goal and tells the player, once.
        /// Only a finished play triggers this, so moving the sliders in the settings never fires a notification.
        /// </summary>
        private void checkGoal(DateOnly day)
        {
            var progress = BanchosucksDailyGoal.ProgressOn(file.Entries, day, goalMinutes?.Value ?? 0, goalMaps?.Value ?? 0);

            if (!progress.Met || !file.MarkGoalDay(day))
                return;

            int streak = BanchosucksDailyGoal.Streak(file.GoalDays, day);

            notifications?.Post(new SimpleNotification
            {
                Icon = FontAwesome.Solid.CalendarCheck,
                Text = streak == 1
                    ? ClickerStrings.Text("Daily goal reached! First day of a new streak.", "Tagesziel erreicht! Erster Tag einer neuen Serie.")
                    : ClickerStrings.Text("Daily goal reached! Streak: {0} days in a row.", "Tagesziel erreicht! Serie: {0} Tage in Folge.", streak),
            });
        }

        // ------------------------------------------------------------------ saved scores from before the log

        /// <summary>
        /// Once, as soon as the player is logged in: takes the player's own saved scores from before the log existed
        /// into it as estimated entries, so the statistics do not start empty. The local database only keeps passed
        /// plays (and failed ones the player saved by hand, which are skipped), without a duration.
        /// </summary>
        private void backfill()
        {
            if (file.Backfilled || file.ReadOnly || realm == null || !api.IsLoggedIn)
                return;

            var user = api.LocalUser.Value;
            int userId = user.Id;
            string username = user.Username;

            if (userId <= 1 || string.IsNullOrEmpty(username))
                return;

            // set before the query so a second "online" during it does not start another one
            file.Backfilled = true;

            DateTimeOffset before = file.Created;
            DateTimeOffset after = before.AddDays(-BanchosucksActivityFile.KEEP_DAYS);
            var database = realm;

            Task.Run(() =>
            {
                try
                {
                    return database.Run(r =>
                    {
                        var found = new List<BanchosucksActivityEntry>();

                        // the date is compared here rather than in the query: the list is read once and is small enough
                        foreach (var score in r.All<ScoreInfo>().Where(s => !s.DeletePending))
                        {
                            if (score.Date < after || score.Date >= before)
                                continue;

                            if (score.RealmUser.OnlineID != userId && !string.Equals(score.RealmUser.Username, username, StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (score.Rank == ScoreRank.F || score.BeatmapInfo == null)
                                continue;

                            double rate = 1;

                            try
                            {
                                rate = ModUtils.CalculateRateWithMods(score.Mods);
                            }
                            catch
                            {
                                // a ruleset or mod that is no longer installed: assume normal speed
                            }

                            found.Add(BanchosucksActivityBackfill.FromSavedScore(score.Date.ToLocalTime(), score.BeatmapInfo.Length, rate, score.Accuracy, score.Ruleset.ShortName));
                        }

                        return found;
                    });
                }
                catch (Exception e)
                {
                    // the game is closing or the database is busy being moved: try again on the next start
                    Logger.Log($"Activity log: saved scores could not be read ({e.Message})");
                    return null;
                }
            }).ContinueWith(task => Schedule(() =>
            {
                var found = task.GetResultSafely();

                if (found == null)
                {
                    file.Backfilled = false;
                    return;
                }

                foreach (var entry in found)
                    file.Add(entry);

                file.Prune(Today);
                save();

                if (found.Count > 0)
                {
                    Logger.Log($"Activity log: took over {found.Count} saved scores from before the log existed.");
                    Changed?.Invoke();
                }
            }));
        }

        // ------------------------------------------------------------------ saving

        /// <summary>
        /// Queues an atomic write of the current state. Serialising and writing happen off the update thread, one write at a time and in order.
        /// </summary>
        private void save()
        {
            if (file.ReadOnly)
                return;

            var snapshot = file.Snapshot();
            string path = logStorage.GetFullPath(LOG_FILE, true);
            string backup = logStorage.GetFullPath(BACKUP_FILE, true);

            lock (saveLock)
            {
                saveTask = saveTask.ContinueWith(_ =>
                {
                    try
                    {
                        snapshot.Write(path, backup);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Activity log could not be saved");
                    }
                }, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Blocks until every queued write is on disk (tests, and closing the game).
        /// </summary>
        public void Flush()
        {
            Task pending;

            lock (saveLock)
                pending = saveTask;

            pending.Wait(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Starts from an empty log (tests only).
        /// </summary>
        public void ResetForTests()
        {
            tracked = null;
            finished = null;
            file = new BanchosucksActivityFile { Created = Now(), Backfilled = true };
            save();
            Changed?.Invoke();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                Flush();

            base.Dispose(isDisposing);
        }
    }

    /// <summary>
    /// Banchosucks: builds an estimated log entry from a score saved before the log existed.
    /// </summary>
    public static class BanchosucksActivityBackfill
    {
        // no beatmap is longer than this; guards against a broken length in the database
        private const double max_seconds = 3600;

        /// <param name="localEnd">The score's date in local time.</param>
        /// <param name="beatmapLengthMs">The beatmap's playable length in milliseconds.</param>
        /// <param name="rate">The speed of the mods (1.5 for Double Time).</param>
        /// <param name="accuracy">0 to 1.</param>
        /// <param name="ruleset">Short name of the ruleset.</param>
        public static BanchosucksActivityEntry FromSavedScore(DateTimeOffset localEnd, double beatmapLengthMs, double rate, double accuracy, string ruleset)
        {
            if (double.IsNaN(rate) || rate < 0.1)
                rate = 1;

            double seconds = double.IsNaN(beatmapLengthMs) ? 0 : Math.Clamp(beatmapLengthMs / 1000 / rate, 0, max_seconds);

            return new BanchosucksActivityEntry
            {
                At = new DateTimeOffset(localEnd.Year, localEnd.Month, localEnd.Day, localEnd.Hour, localEnd.Minute, localEnd.Second, localEnd.Offset),
                Seconds = Math.Round(seconds, 1),
                Ruleset = ruleset,
                Passed = true,
                Accuracy = double.IsNaN(accuracy) ? 0 : Math.Round(Math.Clamp(accuracy, 0, 1), 4),
                Estimated = true,
            };
        }
    }
}

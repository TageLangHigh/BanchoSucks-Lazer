// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    /// <summary>
    /// Banchosucks: "Clean up library" in Settings > Maintenance > Beatmaps.
    /// </summary>
    /// <remarks>
    /// The button only starts a scan (in the background, with a progress notification that can be cancelled). The scan
    /// changes nothing. Its result is shown in <see cref="BanchosucksLibraryCleanupResultDialog"/>; removing needs a second,
    /// held confirmation in <see cref="BanchosucksLibraryCleanupConfirmationDialog"/> that names the number of sets, and
    /// goes through the game's normal soft delete, so "restore all recently deleted beatmaps" brings everything back
    /// until the game is closed. What counts as duplicate or broken is decided by <see cref="BanchosucksLibraryCleanup"/>.
    /// </remarks>
    public partial class BanchosucksLibraryCleanupButton : SettingsButtonV2
    {
        /// <summary>
        /// How long after the end of a scan its result may still open by itself. A scan of a large library takes a while; when
        /// the player has left the settings in the meantime, the result waits behind the completion notification instead of
        /// popping up at some later moment.
        /// </summary>
        private static readonly TimeSpan auto_show_window = TimeSpan.FromSeconds(5);

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        /// <summary>
        /// Whether a scan or a removal is running.
        /// </summary>
        public bool Busy { get; private set; }

        /// <summary>
        /// The result of the last finished scan.
        /// </summary>
        public BanchosucksLibraryCleanup.Result? LastResult { get; private set; }

        public BanchosucksLibraryCleanupButton()
        {
            Text = ClickerStrings.Text("Clean up library", "Bibliothek aufräumen");
            TooltipText = ClickerStrings.Text(
                "Looks for beatmap sets that were imported more than once and for sets that cannot be played (no difficulty, missing .osu or audio file). Shows the result first; nothing is removed without your confirmation.",
                "Sucht Beatmap-Sets, die mehrfach importiert wurden, und Sets, die sich nicht spielen lassen (keine Schwierigkeit, fehlende .osu- oder Audiodatei). Zeigt zuerst das Ergebnis; ohne deine Bestätigung wird nichts entfernt.");
            Keywords = new[] { "cleanup", "clean up", "aufräumen", "duplicate", "duplikat", "broken", "defekt" };
            Action = startScan;
        }

        private void startScan()
        {
            if (Busy)
                return;

            setBusy(true);
            LastResult = null;

            var notification = new ProgressNotification
            {
                Text = ClickerStrings.Text("Checking the beatmap library...", "Beatmap-Bibliothek wird geprüft ..."),
                CompletionText = ClickerStrings.Text("Library checked.", "Bibliothek geprüft."),
                State = ProgressNotificationState.Active,
            };

            notifications?.Post(notification);

            Task.Run(() =>
            {
                var sets = BanchosucksLibraryScanner.Scan(realm, storage, (done, total) =>
                {
                    // the token is only cancelled on the next update frame; the state is set as soon as the player clicks.
                    if (notification.State == ProgressNotificationState.Cancelled)
                        throw new OperationCanceledException();

                    if (done % 25 == 0 || done == total)
                    {
                        notification.Text = ClickerStrings.Text("Checking the beatmap library ({0} of {1})", "Beatmap-Bibliothek wird geprüft ({0} von {1})", done, total);
                        notification.Progress = (float)done / total;
                    }
                }, notification.CancellationToken);

                return BanchosucksLibraryCleanup.Analyse(sets);
            }).ContinueWith(task =>
            {
                if (task.IsCanceled || task.Exception?.InnerException is OperationCanceledException || notification.State == ProgressNotificationState.Cancelled)
                {
                    Schedule(() => setBusy(false));
                    return;
                }

                if (task.IsFaulted)
                {
                    Logger.Error(task.Exception, "Banchosucks: the library scan failed");
                    notification.State = ProgressNotificationState.Cancelled;
                    Schedule(() => setBusy(false));
                    return;
                }

                var result = task.GetResultSafely();
                DateTimeOffset finished = DateTimeOffset.Now;
                bool shown = false;

                notification.CompletionText = createCompletionText(result);
                notification.CompletionClickAction = () =>
                {
                    shown = true;
                    showResult(result);
                    return true;
                };
                notification.State = ProgressNotificationState.Completed;

                // this only runs while the settings are on screen. If the player has closed them, the notification above leads to the result.
                Schedule(() =>
                {
                    setBusy(false);
                    LastResult = result;

                    if (!shown && DateTimeOffset.Now - finished <= auto_show_window)
                    {
                        shown = true;
                        showResult(result);
                    }
                });
            });
        }

        private static LocalisableString createCompletionText(BanchosucksLibraryCleanup.Result result)
        {
            if (!result.AnythingToRemove && result.PartlyDamagedSets.Count == 0)
                return ClickerStrings.Text("Library checked: {0} beatmap sets, nothing to clean up.", "Bibliothek geprüft: {0} Beatmap-Sets, nichts aufzuräumen.", result.ScannedSets);

            return ClickerStrings.Text(
                "Library checked: {0} duplicate(s), {1} broken set(s). Click to see the result.",
                "Bibliothek geprüft: {0} Duplikat(e), {1} defekte(s) Set(s). Klicken für das Ergebnis.",
                result.DuplicatesToRemove.Count, result.BrokenSets.Count);
        }

        private void showResult(BanchosucksLibraryCleanup.Result result)
        {
            dialogOverlay?.Push(new BanchosucksLibraryCleanupResultDialog(result, (ids, duplicates, broken) =>
                dialogOverlay?.Push(new BanchosucksLibraryCleanupConfirmationDialog(duplicates, broken, ids.Count, () => remove(result, ids)))));
        }

        /// <summary>
        /// Removes the given sets. Only ever called from the confirmation dialog.
        /// </summary>
        private void remove(BanchosucksLibraryCleanup.Result result, IReadOnlyList<Guid> ids)
        {
            setBusy(true);

            var duplicateIds = result.DuplicatesToRemove.Select(s => s.ID).ToHashSet();

            var notification = new ProgressNotification
            {
                Text = ClickerStrings.Text("Removing beatmap sets...", "Beatmap-Sets werden entfernt ..."),
                State = ProgressNotificationState.Active,
            };

            notifications?.Post(notification);

            Task.Run(() =>
            {
                int removed = 0;
                int skipped = 0;

                for (int i = 0; i < ids.Count; i++)
                {
                    if (notification.State == ProgressNotificationState.Cancelled)
                        break;

                    Guid id = ids[i];

                    // the library may have changed since the scan. A duplicate that has received a score in the meantime
                    // stays, like every copy with scores; a set that is gone or protected is not touched.
                    bool stillRemovable = realm.Run(r =>
                    {
                        var set = r.Find<BeatmapSetInfo>(id);

                        if (set == null || set.DeletePending || set.Protected)
                            return false;

                        return !duplicateIds.Contains(id) || !set.Beatmaps.Any(b => b.Scores.Any(s => !s.DeletePending));
                    });

                    // the game's own soft delete: the set is only marked and can be restored until the game is closed.
                    if (stillRemovable && beatmaps.Delete(new BeatmapSetInfo { ID = id }))
                        removed++;
                    else
                        skipped++;

                    notification.Text = ClickerStrings.Text("Removing beatmap sets ({0} of {1})", "Beatmap-Sets werden entfernt ({0} von {1})", i + 1, ids.Count);
                    notification.Progress = (float)(i + 1) / ids.Count;
                }

                notification.CompletionText = skipped == 0
                    ? ClickerStrings.Text(
                        "Removed {0} beatmap set(s). Until the game is closed they can be restored in Maintenance > Beatmaps.",
                        "{0} Beatmap-Set(s) entfernt. Bis zum Beenden des Spiels lassen sie sich unter Wartung > Beatmaps wiederherstellen.", removed)
                    : ClickerStrings.Text(
                        "Removed {0} beatmap set(s), left {1} alone because they changed since the check. Until the game is closed they can be restored in Maintenance > Beatmaps.",
                        "{0} Beatmap-Set(s) entfernt, {1} unverändert gelassen, weil sie sich seit der Prüfung geändert haben. Bis zum Beenden des Spiels lassen sie sich unter Wartung > Beatmaps wiederherstellen.",
                        removed, skipped);

                if (notification.State != ProgressNotificationState.Cancelled)
                    notification.State = ProgressNotificationState.Completed;
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Logger.Error(task.Exception, "Banchosucks: removing beatmap sets failed");
                    notification.State = ProgressNotificationState.Cancelled;
                }

                Schedule(() => setBusy(false));
            });
        }

        private void setBusy(bool busy)
        {
            Busy = busy;
            Enabled.Value = !busy;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Banchosucks: "Skin-Abgleich" (F43). While <see cref="OsuSetting.BanchosucksSkinSync"/> is on, the Skins folder
    /// of the osu!stable installation is looked at shortly after startup, then every <see cref="CHECK_INTERVAL"/>,
    /// and whenever the player presses "check now". Folders that were never imported, or whose files changed since
    /// the last import, go through the normal skin importer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stable is only read: the check looks at names, sizes and times, and the importer copies the files into the
    /// game's own store (no hard links, so that later edits in stable cannot reach into it). Skins are never removed
    /// here; a folder that disappears from stable leaves its skin in the game. What the importer itself does with a
    /// changed folder is the same as for a manual import: same skin.ini but other files replaces the previous import,
    /// a changed skin.ini adds a second skin. If the replaced skin was the selected one, the new one is selected.
    /// </para>
    /// <para>
    /// Nothing happens during gameplay: a due check waits until the player is back in a menu, and a running one stops
    /// before the next folder (the importer additionally holds its own work while a play is on).
    /// </para>
    /// </remarks>
    public partial class BanchosucksSkinSync : Component
    {
        /// <summary>
        /// Time between two automatic checks, in milliseconds.
        /// </summary>
        public const double CHECK_INTERVAL = 10 * 60 * 1000;

        /// <summary>
        /// How long after startup (or after switching the setting on) the first check waits.
        /// </summary>
        public const double FIRST_CHECK_DELAY = 20 * 1000;

        private const string stable_skins_directory = "Skins";

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private LegacyImportManager? legacyImportManager { get; set; }

        [Resolved(CanBeNull = true)]
        private ILocalUserPlayInfo? playInfo { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private Bindable<bool> enabled = null!;
        private IBindable<LocalUserPlayingState>? playingState;

        private double? nextCheck;

        /// <summary>
        /// Read by the import thread before every folder.
        /// </summary>
        private volatile bool playing;

        private int running;

        /// <summary>
        /// The check that is running or ran last (for tests).
        /// </summary>
        public Task LastCheck { get; private set; } = Task.CompletedTask;

        public bool IsChecking => Volatile.Read(ref running) == 1;

        /// <summary>
        /// Where the stable Skins folder is, when it should not be looked up through the stable installation (tests).
        /// </summary>
        public string? SkinsDirectoryOverride { get; init; }

        /// <summary>
        /// The wait before the first automatic check; shortened in tests.
        /// </summary>
        public double FirstCheckDelay { get; init; } = FIRST_CHECK_DELAY;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            playingState = playInfo?.PlayingState.GetBoundCopy();
            playingState?.BindValueChanged(state => playing = state.NewValue != LocalUserPlayingState.NotPlaying, true);

            enabled = config.GetBindable<bool>(OsuSetting.BanchosucksSkinSync);
            enabled.BindValueChanged(e => nextCheck = e.NewValue ? Time.Current + FirstCheckDelay : null, true);
        }

        protected override void Update()
        {
            base.Update();

            // a due check simply stays due while the player is in a play
            if (nextCheck == null || Time.Current < nextCheck || playing || IsChecking)
                return;

            nextCheck = Time.Current + CHECK_INTERVAL;
            CheckNow(false);
        }

        /// <summary>
        /// Looks at the stable Skins folder now and imports what is new or changed.
        /// </summary>
        /// <param name="manual">Whether the player asked for it: then every outcome is reported, not only imports.</param>
        public void CheckNow(bool manual)
        {
            if (playing)
            {
                if (manual)
                    notify(ClickerStrings.Text("Skins are not synced during gameplay.", "Während des Spielens werden keine Skins abgeglichen."));
                return;
            }

            string? skinsDirectory = findSkinsDirectory();

            if (skinsDirectory == null)
            {
                if (manual)
                {
                    notifyError(ClickerStrings.Text("No osu!stable installation with a Skins folder was found on this PC.",
                        "Auf diesem PC wurde keine osu!stable-Installation mit einem Skins-Ordner gefunden."));
                }

                return;
            }

            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            {
                if (manual)
                    notify(ClickerStrings.Text("The skin sync is already running.", "Der Skin-Abgleich läuft bereits."));
                return;
            }

            Guid selectedBefore = skins.CurrentSkinInfo.Value.ID;

            LastCheck = Task.Factory.StartNew(() =>
            {
                try
                {
                    run(skinsDirectory, selectedBefore, manual);
                }
                catch (Exception e)
                {
                    // the message of an IO error carries the stable path; the type says enough
                    Logger.Log($"Banchosucks skin sync failed ({e.GetType().Name}).", LoggingTarget.Runtime, LogLevel.Important);

                    if (manual)
                        Schedule(() => notifyError(ClickerStrings.Text("The skin sync failed (see the log).", "Der Skin-Abgleich ist fehlgeschlagen (siehe Log).")));
                }
                finally
                {
                    Volatile.Write(ref running, 0);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private string? findSkinsDirectory()
        {
            if (SkinsDirectoryOverride != null)
                return Directory.Exists(SkinsDirectoryOverride) ? SkinsDirectoryOverride : null;

            try
            {
                var stable = legacyImportManager?.GetCurrentStableStorage();

                // ExistsDirectory and GetFullPath do not create anything in the stable folder
                if (stable == null || !stable.ExistsDirectory(stable_skins_directory))
                    return null;

                return stable.GetFullPath(stable_skins_directory);
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks skin sync could not look for osu!stable ({e.GetType().Name}).", LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }
        }

        private void run(string skinsDirectory, Guid selectedBefore, bool manual)
        {
            var record = BanchosucksSkinSyncRecord.Load(storage);
            var found = BanchosucksSkinSyncRecord.MeasureAll(skinsDirectory);
            var pending = record.FoldersToImport(found);

            int imported = 0;
            int failed = 0;
            bool interrupted = false;

            foreach (string folder in pending)
            {
                if (playing)
                {
                    interrupted = true;
                    break;
                }

                try
                {
                    // copies into the game's file store; a skin the game already has in exactly this form is reused
                    var skin = skins.Import(new ImportTask(Path.Combine(skinsDirectory, folder))).GetResultSafely();

                    if (skin == null)
                    {
                        failed++;
                        continue;
                    }

                    // written after every folder, so an interrupted run continues where it stopped
                    record.MarkImported(folder, found[folder], skin.ID);
                    record.Store(storage);
                    imported++;

                    keepSelection(selectedBefore, skin);
                }
                catch (Exception e)
                {
                    failed++;
                    Logger.Log($"Banchosucks skin sync: a skin folder could not be imported ({e.GetType().Name}).", LoggingTarget.Runtime, LogLevel.Important);
                }
            }

            Logger.Log($"Banchosucks skin sync: {found.Count} folders, {imported} imported, {failed} failed{(interrupted ? ", stopped for gameplay" : string.Empty)}.");

            int importedCount = imported;
            int failedCount = failed;

            Schedule(() =>
            {
                // the rest follows soon after the play instead of ten minutes later
                if (interrupted && nextCheck != null)
                    nextCheck = Time.Current + FirstCheckDelay;

                if (failedCount > 0)
                {
                    notifyError(ClickerStrings.Text("Skin sync: {0} skins imported from osu!stable, {1} could not be imported.",
                        "Skin-Abgleich: {0} Skins aus osu!stable importiert, {1} ließen sich nicht importieren.", importedCount, failedCount));
                }
                else if (importedCount > 0)
                {
                    notify(ClickerStrings.Text("Skin sync: {0} skins imported from osu!stable.", "Skin-Abgleich: {0} Skins aus osu!stable importiert.", importedCount));
                }
                else if (manual)
                {
                    notify(ClickerStrings.Text("Skin sync: everything is up to date.", "Skin-Abgleich: Alles ist auf dem neuesten Stand."));
                }
            });
        }

        /// <summary>
        /// When the importer replaced the skin the player has selected (same skin.ini, changed files), the selection
        /// would point at a skin that is on its way out: select its replacement instead.
        /// </summary>
        private void keepSelection(Guid selectedBefore, Live<SkinInfo> imported)
        {
            if (imported.ID == selectedBefore)
                return;

            Schedule(() =>
            {
                if (skins.CurrentSkinInfo.Value.ID != selectedBefore || skins.CurrentSkinInfo.Disabled)
                    return;

                bool replaced = realm.Run(r => r.Find<SkinInfo>(selectedBefore)?.DeletePending == true);

                if (replaced)
                    skins.CurrentSkinInfo.Value = imported;
            });
        }

        private void notify(LocalisableString text) => notifications?.Post(new SimpleNotification { Text = text, Icon = FontAwesome.Solid.PaintBrush });

        private void notifyError(LocalisableString text) => notifications?.Post(new SimpleErrorNotification { Text = text });
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Play;
using osu.Game.Updater;
using Velopack;
using Velopack.Sources;
using UpdateManager = osu.Game.Updater.UpdateManager;

namespace osu.Desktop.Updater
{
    /// <summary>
    /// Banchosucks: keeps the client current from our own GitHub releases (the download mirror as fallback). Two paths:
    /// the startup step (<see cref="StartupUpdate"/>) asks the feed while the game loads and installs a newer version before
    /// the intro, with the loader showing the progress; the background check every 30 minutes downloads during a session and
    /// offers the restart in a notification.
    /// </summary>
    public partial class VelopackUpdateManager : UpdateManager
    {
        [Resolved]
        private INotificationOverlay notificationOverlay { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? localUserInfo { get; set; }

        private bool isInGameplay => localUserInfo?.PlayingState.Value != LocalUserPlayingState.NotPlaying;

        /// <summary>
        /// How long the startup step waits for the feed in total. Offline the sockets fail much faster; this bounds a silent firewall.
        /// </summary>
        private const double startup_check_timeout_seconds = 10;

        private readonly StartupUpdate? startup;
        private readonly Task startupTask;
        private bool startupInstalling;
        private bool startupCheckConsumed;

        private ScheduledDelegate? scheduledBackgroundCheck;
        private bool notInstalledNoticeShown;

        /// <param name="startupUpdate">Whether to check, download and install a newer version before the game starts
        /// (deployed builds started without arguments). The check begins right here, in parallel to the game's own loading.</param>
        public VelopackUpdateManager(bool startupUpdate)
        {
            if (startupUpdate)
            {
                startup = new StartupUpdate();
                startupTask = Task.Run(() => runStartupUpdateAsync(startup));
            }
            else
                startupTask = Task.CompletedTask;
        }

        public override StartupUpdate? StartupUpdate => startup;

        private async Task runStartupUpdateAsync(StartupUpdate state)
        {
            try
            {
                var result = await checkSourcesAsync(TimeSpan.FromSeconds(startup_check_timeout_seconds)).ConfigureAwait(false);

                if (result == null)
                {
                    state.Finish("no update source answered in time");
                    return;
                }

                var (manager, update) = result.Value;

                if (update == null)
                {
                    state.Finish("up to date");
                    return;
                }

                if (update.IsDowngrade)
                {
                    state.Finish($"feed offers {update.TargetFullRelease.Version}, not newer than this build");
                    return;
                }

                string version = update.TargetFullRelease.Version.ToString();
                log($"Startup update: downloading {version}");
                state.BeginDownload(version);

                await manager.DownloadUpdatesAsync(update, p => state.ReportProgress(p / 100f)).ConfigureAwait(false);

                log($"Startup update: {version} downloaded, the loader restarts into it");
                startupInstalling = true;
                state.ReadyToRestart(() => manager.WaitExitThenApplyUpdates(update.TargetFullRelease));
            }
            catch (Velopack.Exceptions.NotInstalledException)
            {
                // unpacked from a plain zip; the background check tells the player once the game is up
                state.Finish("not installed via the setup, cannot self-update");
            }
            catch (Exception e)
            {
                log($"Startup update failed ({e.Message}), starting the game without it");
                state.Finish($"failed: {e.Message}");
            }
        }

        /// <summary>
        /// Asks the update sources in order (GitHub first, then the mirror) until one answers.
        /// Null when none did, or none within <paramref name="timeout"/>.
        /// </summary>
        private async Task<(Velopack.UpdateManager manager, UpdateInfo? update)?> checkSourcesAsync(TimeSpan? timeout)
        {
            var stopwatch = Stopwatch.StartNew();

            foreach (var (name, source) in updateSources())
            {
                try
                {
                    // never a downgrade: a stale mirror must not pull players back to an older version
                    var candidate = new Velopack.UpdateManager(source, new UpdateOptions { AllowVersionDowngrade = false });
                    Task<UpdateInfo?> check = candidate.CheckForUpdatesAsync();

                    if (timeout != null)
                    {
                        TimeSpan remaining = timeout.Value - stopwatch.Elapsed;

                        if (remaining <= TimeSpan.Zero || await Task.WhenAny(check, Task.Delay(remaining)).ConfigureAwait(false) != check)
                        {
                            log($"Update check against {name} did not answer within {timeout.Value.TotalSeconds:0} s");
                            return null;
                        }
                    }

                    UpdateInfo? update = await check.ConfigureAwait(false);
                    log($"Update check against {name}: {(update == null ? "up to date" : update.TargetFullRelease.Version.ToString())}");
                    return (candidate, update);
                }
                catch (Velopack.Exceptions.NotInstalledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    log($"Update check against {name} failed ({e.Message}), trying the next source");
                }
            }

            return null;
        }

        private void scheduleNextUpdateCheck()
        {
            scheduledBackgroundCheck?.Cancel();
            scheduledBackgroundCheck = Scheduler.AddDelayed(() =>
            {
                log("Running scheduled background update check...");
                CheckForUpdate();
            }, 60000 * 30);
        }

        protected override async Task<bool> PerformUpdateCheck(CancellationToken cancellationToken)
        {
            scheduledBackgroundCheck?.Cancel();

            // Banchosucks: the startup step goes first. While it installs a version the game is about to restart, and right
            // after it the feed was just read, so the first background check only sets the half-hour rhythm.
            await startupTask.ConfigureAwait(false);

            if (startupInstalling)
                return true;

            if (startup != null && !startupCheckConsumed)
            {
                startupCheckConsumed = true;
                scheduleNextUpdateCheck();
                return false;
            }

            if (isInGameplay)
            {
                log("Update check cancelled - user is in gameplay");
                scheduleNextUpdateCheck();
                return false;
            }

            try
            {
                // Banchosucks: updates come from our own releases, never from upstream (which would replace the
                // client with a g0v0 build). GitHub serves the packages from its CDN at full speed from the first
                // byte; the mirror dl.banchosucks.cc (Julian's home server, uplink-bound and not cached by Cloudflare
                // for .nupkg) carries the same feed and is the fallback when GitHub is down or rate-limited (2026-10-04).
                var result = await checkSourcesAsync(null).ConfigureAwait(false);

                if (result == null)
                    throw new InvalidOperationException("No update source could be reached");

                var (updateManager, update) = result.Value;

                if (cancellationToken.IsCancellationRequested)
                {
                    log("Update check cancelled");
                    scheduleNextUpdateCheck();
                    return true;
                }

                if (update == null)
                {
                    // No update is available.
                    log("No update found");
                    scheduleNextUpdateCheck();
                    return false;
                }

                // Download update in the background while notifying awaiters of the update being available.
                log($"New update available: {update.TargetFullRelease.Version}");
                downloadUpdate(updateManager, update, cancellationToken);
                return true;
            }
            catch (Exception e)
            {
                log($"Update check failed with error ({e.Message})");

                // Banchosucks: a copy unpacked from a plain zip (releases before 2026.913.10) has no Velopack
                // Update.exe next to it, so it can never update itself; say so instead of failing silently.
                if (e is Velopack.Exceptions.NotInstalledException && !notInstalledNoticeShown)
                {
                    notInstalledNoticeShown = true;
                    runOutsideOfGameplay(() => notificationOverlay.Post(new SimpleNotification
                    {
                        Text = "Diese Installation kann sich nicht selbst aktualisieren (kein Setup). Bitte einmal BanchoSucksLazer-win-Setup.exe von github.com/TageLangHigh/BanchoSucks-Lazer/releases installieren, danach kommen Updates im Spiel.",
                        Icon = FontAwesome.Solid.Download,
                    }), cancellationToken);
                }

                // we shouldn't crash on a web failure. or any failure for the matter.
                scheduleNextUpdateCheck();
                return true;
            }
        }

        private void downloadUpdate(Velopack.UpdateManager updateManager, UpdateInfo update, CancellationToken cancellationToken) => Task.Run(async () =>
        {
            log($"Beginning download of update {update.TargetFullRelease.Version}...");

            UpdateDownloadProgressNotification progressNotification = new UpdateDownloadProgressNotification(cancellationToken)
            {
                CompletionClickAction = () =>
                {
                    restartToApplyUpdate(updateManager, update);
                    return true;
                }
            };

            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(progressNotification.CancellationToken, cancellationToken))
                {
                    progressNotification.StartDownload();
                    runOutsideOfGameplay(() => notificationOverlay.Post(progressNotification), cts.Token);

                    await updateManager.DownloadUpdatesAsync(update, p => progressNotification.Progress = p / 100f, cts.Token).ConfigureAwait(false);
                    runOutsideOfGameplay(() => progressNotification.State = ProgressNotificationState.Completed, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                progressNotification.FailDownload();
                log(@"Update cancelled");
            }
            catch (Exception e)
            {
                // In the case of an error, a separate notification will be displayed.
                progressNotification.FailDownload();
                Logger.Error(e, @"Update failed!");
            }

            return true;
        }, cancellationToken);

        private void runOutsideOfGameplay(Action action, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (isInGameplay)
            {
                Scheduler.AddDelayed(() => runOutsideOfGameplay(action, cancellationToken), 1000);
                return;
            }

            action();
        }

        private void restartToApplyUpdate(Velopack.UpdateManager updateManager, UpdateInfo update)
        {
            game.RestartOnExitAction = () => updateManager.WaitExitThenApplyUpdates(update.TargetFullRelease);
            game.AttemptExit();
        }

        private static IEnumerable<(string name, IUpdateSource source)> updateSources()
        {
            yield return ("GitHub", new GithubSource(@"https://github.com/TageLangHigh/BanchoSucks-Lazer", null, false));
            yield return ("dl.banchosucks.cc", new SimpleWebSource(@"https://dl.banchosucks.cc/releases/lazer/"));
        }

        private static void log(string text) => Logger.Log($"VelopackUpdateManager: {text}");
    }
}

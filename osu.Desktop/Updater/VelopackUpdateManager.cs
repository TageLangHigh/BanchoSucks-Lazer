// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Play;
using Velopack;
using Velopack.Sources;
using UpdateManager = osu.Game.Updater.UpdateManager;

namespace osu.Desktop.Updater
{
    public partial class VelopackUpdateManager : UpdateManager
    {
        [Resolved]
        private INotificationOverlay notificationOverlay { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? localUserInfo { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private bool isInGameplay => localUserInfo?.PlayingState.Value != LocalUserPlayingState.NotPlaying;

        private ScheduledDelegate? scheduledBackgroundCheck;
        private bool notInstalledNoticeShown;

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
                Velopack.UpdateManager? updateManager = null;
                UpdateInfo? update = null;

                foreach (var (name, source) in updateSources())
                {
                    try
                    {
                        var candidate = new Velopack.UpdateManager(source, new UpdateOptions { AllowVersionDowngrade = true });
                        update = await candidate.CheckForUpdatesAsync().ConfigureAwait(false);
                        updateManager = candidate;
                        log($"Update check against {name}: {(update == null ? "up to date" : update.TargetFullRelease.Version.ToString())}");
                        break;
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

                if (updateManager == null)
                    throw new InvalidOperationException("No update source could be reached");

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

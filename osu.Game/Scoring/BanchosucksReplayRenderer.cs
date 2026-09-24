// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: renders a replay to a video file on the player's own PC with danser-go.
    /// </summary>
    /// <remarks>
    /// danser-go (https://github.com/Wieku/danser-go, GPL-3.0) is a separate program; it is downloaded
    /// once, after the player agreed, into the game's storage and started as its own process, so no
    /// danser code is part of this client. The Windows and Linux packages bring their own FFmpeg.
    /// For every render the replay (.osr, as stored by the client or downloaded from the server) and
    /// the unmodified beatmap files are copied into danser's own folders, so the beatmap hash in the
    /// replay matches. Only osu!standard replays can be rendered.
    /// </remarks>
    public partial class BanchosucksReplayRenderer : Component
    {
        public const string DANSER_VERSION = "0.11.0";

        private const string windows_url = "https://github.com/Wieku/danser-go/releases/download/0.11.0/danser-0.11.0-win.zip";
        private const string windows_sha256 = "749b2e66e36c3e2217910923802f08de9bc1c0858fcb6ffae861a6787fb21eee";
        private const string linux_url = "https://github.com/Wieku/danser-go/releases/download/0.11.0/danser-0.11.0-linux.zip";
        private const string linux_sha256 = "c3184ceb84b20e8e9c9a2709113efc29b8bdf2f866949834d7fb8e799618a67e";

        private static readonly HttpClient http = createClient();

        private static readonly Regex progress_line = new Regex(@"Progress:\s*(\d+)%", RegexOptions.Compiled);
        private static readonly Regex output_line = new Regex(@"Video is available at:\s*(.+)$", RegexOptions.Compiled);

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private Storage root = null!;
        private int busy;

        private static bool platformSupported => RuntimeInfo.OS == RuntimeInfo.Platform.Windows || RuntimeInfo.OS == RuntimeInfo.Platform.Linux;

        private string danserDirectory => root.GetFullPath("danser");
        private string danserExecutable => Path.Combine(danserDirectory, RuntimeInfo.OS == RuntimeInfo.Platform.Windows ? "danser-cli.exe" : "danser-cli");
        private string ffmpegExecutable => Path.Combine(danserDirectory, "ffmpeg", RuntimeInfo.OS == RuntimeInfo.Platform.Windows ? "ffmpeg.exe" : "ffmpeg");

        [BackgroundDependencyLoader]
        private void load()
        {
            root = storage.GetStorageForDirectory("banchosucks-renderer");
        }

        /// <summary>
        /// Whether the score could be rendered: osu!standard, and a replay either stored locally or on the server.
        /// </summary>
        public static bool CanRender(ScoreInfo score) =>
            platformSupported && score.Ruleset.ShortName == "osu" && (score.Files.Count > 0 || (score.OnlineID > 0 && score.HasOnlineReplay));

        public void Render(ScoreInfo score)
        {
            if (Volatile.Read(ref busy) == 1)
            {
                notifications?.Post(new SimpleNotification { Text = "Es wird gerade schon ein Replay gerendert, bitte warte bis es fertig ist." });
                return;
            }

            RenderJob? job = prepare(score);

            if (job == null)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "Zu diesem Score fehlt die Map auf deinem PC. Lade die Map herunter und versuche es nochmal.",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
                return;
            }

            if (File.Exists(danserExecutable))
            {
                start(job);
                return;
            }

            if (dialogOverlay == null)
                return;

            dialogOverlay.Push(new BanchosucksRendererDownloadDialog(danserDirectory, () => start(job)));
        }

        /// <summary>
        /// Collects everything the background task needs while on the update thread (realm objects are thread bound).
        /// </summary>
        private RenderJob? prepare(ScoreInfo score)
        {
            return realm.Run(r =>
            {
                ScoreInfo? local = score.Files.Count > 0 ? r.Find<ScoreInfo>(score.ID) : null;
                string? replayStoragePath = local?.Files.FirstOrDefault()?.File.GetStoragePath();

                string md5 = score.BeatmapInfo?.MD5Hash ?? string.Empty;
                BeatmapInfo? beatmap = local?.BeatmapInfo
                                       ?? (string.IsNullOrEmpty(md5) ? null : r.All<BeatmapInfo>().FirstOrDefault(b => b.MD5Hash == md5));

                if (beatmap?.BeatmapSet == null || beatmap.BeatmapSet.Files.Count == 0)
                    return null;

                var set = beatmap.BeatmapSet;
                string folder = $"{set.OnlineID} {set.Metadata.Artist} - {set.Metadata.Title}".GetValidFilename();
                if (folder.Length > 80)
                    folder = folder.Remove(80);

                string title = $"{score.User.Username} - {set.Metadata.Artist} - {set.Metadata.Title} [{beatmap.DifficultyName}]";

                return new RenderJob(
                    replayStoragePath,
                    replayStoragePath == null ? score.OnlineID : 0,
                    set.Files.Select(f => new RenderFile(f.Filename, f.File.GetStoragePath())).ToList(),
                    folder,
                    $"{title} {DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}".GetValidFilename().Replace(' ', '_'),
                    title);
            });
        }

        private void start(RenderJob job)
        {
            if (Interlocked.Exchange(ref busy, 1) == 1)
                return;

            var notification = new ProgressNotification
            {
                Text = $"Replay-Video wird vorbereitet: {job.Title}",
                State = ProgressNotificationState.Active,
            };
            notifications?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    await ensureInstalled(notification, notification.CancellationToken).ConfigureAwait(false);
                    string video = await render(job, notification, notification.CancellationToken).ConfigureAwait(false);

                    string relative = Path.GetRelativePath(root.GetFullPath(string.Empty), video);
                    notification.CompletionText = $"Video fertig: {Path.GetFileName(video)}. Zum Öffnen klicken.";
                    notification.CompletionClickAction = () =>
                    {
                        root.PresentFileExternally(relative);
                        return true;
                    };
                    notification.State = ProgressNotificationState.Completed;
                }
                catch (OperationCanceledException)
                {
                    notification.State = ProgressNotificationState.Cancelled;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Banchosucks replay rendering failed");
                    notification.Text = $"Video konnte nicht gerendert werden: {e.Message}";
                    notification.State = ProgressNotificationState.Cancelled;
                }
                finally
                {
                    Volatile.Write(ref busy, 0);
                }
            });
        }

        private async Task ensureInstalled(ProgressNotification notification, CancellationToken cancellationToken)
        {
            if (File.Exists(danserExecutable) && File.Exists(ffmpegExecutable))
                return;

            bool windows = RuntimeInfo.OS == RuntimeInfo.Platform.Windows;
            string url = windows ? windows_url : linux_url;
            string expectedHash = windows ? windows_sha256 : linux_sha256;
            string archive = root.GetFullPath($"danser-{DANSER_VERSION}.zip");

            notification.Text = $"danser-go {DANSER_VERSION} wird heruntergeladen ...";

            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? 31_000_000;

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = File.Create(archive);
                byte[] buffer = new byte[81920];
                long done = 0;
                int read;

                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    done += read;
                    notification.Progress = Math.Min(1, (float)done / total) * 0.3f;
                    notification.Text = $"danser-go wird heruntergeladen: {done / 1048576} von {total / 1048576} MB";
                }
            }

            string actualHash;
            await using (var check = File.OpenRead(archive))
                actualHash = Convert.ToHexString(await SHA256.HashDataAsync(check, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();

            if (actualHash != expectedHash)
            {
                File.Delete(archive);
                throw new InvalidOperationException("Der danser-Download ist beschädigt oder wurde verändert (Prüfsumme stimmt nicht).");
            }

            notification.Text = "danser-go wird entpackt ...";

            try
            {
                if (Directory.Exists(danserDirectory))
                    Directory.Delete(danserDirectory, true);
                ZipFile.ExtractToDirectory(archive, danserDirectory, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // an antivirus holding or blocking ffmpeg shows up exactly here
                Logger.Error(e, "Extracting danser-go failed");
                throw new InvalidOperationException(missingFfmpegMessage());
            }
            finally
            {
                File.Delete(archive);
            }

            if (!windows)
            {
                const UnixFileMode executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

                foreach (string file in new[] { "danser-cli", "danser", "ffmpeg/ffmpeg", "ffmpeg/ffprobe" })
                {
                    string path = Path.Combine(danserDirectory, file);
                    if (File.Exists(path))
                        File.SetUnixFileMode(path, executable);
                }
            }

            if (!File.Exists(ffmpegExecutable))
                throw new InvalidOperationException(missingFfmpegMessage());

            Logger.Log($"danser-go {DANSER_VERSION} installed to {danserDirectory}");
        }

        private const long stall_timeout_ms = 120_000;

        private string missingFfmpegMessage() =>
            "FFmpeg wurde aus dem danser-Ordner entfernt, vermutlich vom Virenscanner (Avast und andere halten es fälschlich für verdächtig). "
            + $"Nimm den Ordner {danserDirectory} in die Ausnahmen auf und versuche es erneut, danser wird dann neu geladen.";

        private static void kill(Process process)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // already gone
            }
        }

        private async Task<string> render(RenderJob job, ProgressNotification notification, CancellationToken cancellationToken)
        {
            if (!File.Exists(ffmpegExecutable))
            {
                // most likely removed by an antivirus; the next attempt downloads danser again
                Directory.Delete(danserDirectory, true);
                throw new InvalidOperationException(missingFfmpegMessage());
            }

            string songs = root.GetFullPath("songs");
            string skins = root.GetFullPath("skins");
            string replays = root.GetFullPath("replays");
            string videos = root.GetFullPath("videos");
            foreach (string directory in new[] { songs, skins, replays, videos })
                Directory.CreateDirectory(directory);

            notification.Text = $"Map und Replay werden vorbereitet: {job.Title}";
            notification.Progress = 0.3f;

            var files = storage.GetStorageForDirectory("files");
            string mapDirectory = Path.Combine(songs, job.SetFolder);

            foreach (var file in job.BeatmapFiles)
            {
                string target = Path.GetFullPath(Path.Combine(mapDirectory, file.Filename));
                if (!target.StartsWith(mapDirectory, StringComparison.Ordinal) || File.Exists(target))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var source = files.GetStream(file.StoragePath);
                if (source == null)
                    continue;

                await using var destination = File.Create(target);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            string replay = Path.Combine(replays, $"{job.OutputName}.osr");

            if (job.ReplayStoragePath != null)
            {
                await using var source = files.GetStream(job.ReplayStoragePath) ?? throw new InvalidOperationException("Die Replay-Datei fehlt.");
                await using var destination = File.Create(replay);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                string url = $"{api.Endpoints.APIUrl}/api/v2/scores/{job.OnlineScoreId}/download";
                using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Das Replay konnte nicht vom Server geladen werden (HTTP {(int)response.StatusCode}).");

                await using var destination = File.Create(replay);
                await response.Content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            // forward slashes work on Windows too and keep the JSON free of escaped backslashes
            string settingsPatch = JsonSerializer.Serialize(new
            {
                General = new { OsuSongsDir = slashes(songs), OsuSkinsDir = slashes(skins), OsuReplaysDir = slashes(replays) },
                Recording = new { OutputDir = slashes(videos), FrameWidth = 1920, FrameHeight = 1080, FPS = 60 },
            });

            var startInfo = new ProcessStartInfo(danserExecutable)
            {
                WorkingDirectory = danserDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[]
                     {
                         "-noupdatecheck", "-record", "-quickstart", "-preciseprogress",
                         $"-replay={slashes(replay)}", $"-out={job.OutputName}", $"-sPatch={settingsPatch}",
                     })
                startInfo.ArgumentList.Add(argument);

            notification.Text = $"Replay wird gerendert: {job.Title}";

            using var process = new Process { StartInfo = startInfo };
            var lastLines = new Queue<string>();
            string? video = null;

            long lastActivity = Environment.TickCount64;

            void handleLine(string? line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                Interlocked.Exchange(ref lastActivity, Environment.TickCount64);

                lock (lastLines)
                {
                    lastLines.Enqueue(line);
                    while (lastLines.Count > 15)
                        lastLines.Dequeue();
                }

                var progress = progress_line.Match(line);
                if (progress.Success)
                {
                    int percent = int.Parse(progress.Groups[1].Value);
                    notification.Progress = 0.35f + percent / 100f * 0.65f;
                    notification.Text = $"Replay wird gerendert: {percent} % · {job.Title}";
                }

                var output = output_line.Match(line);
                if (output.Success)
                    video = output.Groups[1].Value.Trim();
            }

            process.OutputDataReceived += (_, e) => handleLine(e.Data);
            process.ErrorDataReceived += (_, e) => handleLine(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // danser hangs forever when its ffmpeg is removed mid-render (antivirus); watch for silence
            while (!process.HasExited)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    kill(process);
                    throw;
                }

                if (Environment.TickCount64 - Interlocked.Read(ref lastActivity) > stall_timeout_ms)
                {
                    kill(process);
                    Logger.Log("danser-go stopped reporting progress and was killed", level: LogLevel.Error);

                    if (!File.Exists(ffmpegExecutable))
                        throw new InvalidOperationException(missingFfmpegMessage());

                    throw new InvalidOperationException("danser-go hat zwei Minuten lang keinen Fortschritt gemeldet und wurde beendet.");
                }
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            if (process.ExitCode != 0 || video == null || !File.Exists(video))
            {
                string log;
                lock (lastLines)
                    log = string.Join('\n', lastLines);

                Logger.Log($"danser-go exited with {process.ExitCode}:\n{log}", level: LogLevel.Error);

                if (!File.Exists(ffmpegExecutable))
                    throw new InvalidOperationException(missingFfmpegMessage());

                throw new InvalidOperationException($"danser-go wurde mit Fehler {process.ExitCode} beendet, Details im Log.");
            }

            Logger.Log($"Replay video rendered: {video}");
            return video;
        }

        private static string slashes(string path) => path.Replace('\\', '/');

        private static HttpClient createClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BanchoSucks-Lazer");
            return client;
        }

        private record RenderFile(string Filename, string StoragePath);

        private record RenderJob(
            string? ReplayStoragePath,
            long OnlineScoreId,
            List<RenderFile> BeatmapFiles,
            string SetFolder,
            string OutputName,
            string Title);
    }

    /// <summary>
    /// Asks the player before danser-go is downloaded for the first time.
    /// </summary>
    public partial class BanchosucksRendererDownloadDialog : PopupDialog
    {
        public BanchosucksRendererDownloadDialog(string installPath, Action onConfirm)
        {
            HeaderText = "Replay als Video rendern";
            BodyText = $"Dafür lädt der Client einmalig danser-go {BanchosucksReplayRenderer.DANSER_VERSION} herunter "
                       + "(github.com/Wieku/danser-go, GPL-3.0, etwa 30 MB inklusive FFmpeg) und speichert es in "
                       + $"{installPath}. Gerendert wird danach auf deinem eigenen PC. Einverstanden?";
            Icon = FontAwesome.Solid.Video;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = "Herunterladen und rendern",
                    Action = onConfirm,
                },
                new PopupDialogCancelButton
                {
                    Text = "Abbrechen",
                },
            };
        }
    }
}

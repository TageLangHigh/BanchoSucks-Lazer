// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Edit;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: the "overlay data source". While <see cref="OsuSetting.BanchosucksOverlayDataSource"/> is on, the game
    /// answers on <c>127.0.0.1:24050</c> the way the tool tosu does, so stream overlays written for tosu (pp counters,
    /// now-playing widgets) work with this client, whose memory tosu cannot read.
    /// </summary>
    /// <remarks>
    /// Division of labour: this component collects a <see cref="BanchosucksOverlayState"/> on the update thread at most
    /// every <see cref="PUBLISH_INTERVAL"/> ms; <see cref="BanchosucksOverlayJson"/> turns it into tosu's JSON (on a
    /// worker thread, and only while an overlay is connected); <see cref="BanchosucksOverlayServer"/> sends it to the
    /// overlays whose last document differs. Off by default, loopback only, nothing leaves the PC.
    /// </remarks>
    public partial class BanchosucksOverlayDataSource : Component
    {
        /// <summary>
        /// Minimum time between two documents in milliseconds (at most ten per second).
        /// </summary>
        public const double PUBLISH_INTERVAL = 100;

        /// <summary>
        /// Folder in the game's data directory whose content is served to overlays (<c>http://127.0.0.1:24050/&lt;name&gt;/</c>).
        /// </summary>
        public const string OVERLAY_FOLDER = "banchosucks-overlays";

        /// <summary>
        /// How long a beatmap has to stay selected before its details (objects, star rating) are worked out.
        /// </summary>
        private const double details_debounce = 150;

        /// <summary>
        /// The port to listen on. Tests pick a free one.
        /// </summary>
        public int Port { get; init; } = BanchosucksOverlayHttp.DEFAULT_PORT;

        private readonly Func<IScreen?> getCurrentScreen;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private MusicController? music { get; set; }

        [Resolved]
        private SkinManager? skins { get; set; }

        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        private Bindable<bool> enabled = null!;
        private Bindable<double> audioOffset = null!;
        private Bindable<double> dimLevel = null!;
        private Bindable<bool> showStoryboard = null!;
        private Bindable<float> cursorSize = null!;
        private Bindable<HUDVisibilityMode> hudVisibility = null!;
        private Bindable<WindowMode> windowMode = null!;
        private IBindable<Live<SkinInfo>>? currentSkin;
        private string skinName = string.Empty;

        private readonly Stopwatch sessionTime = Stopwatch.StartNew();
        private int sessionPlayCount;

        private BanchosucksOverlayServer? server;
        private bool serverRunning;
        private Task lastStop = Task.CompletedTask;

        private volatile BanchosucksOverlayState latestState = new BanchosucksOverlayState();
        private double lastGatherTime = double.MinValue;
        private int publishing;

        private Ruleset? rulesetInstance;
        private RulesetInfo? rulesetInstanceInfo;

        private volatile BeatmapDetails? details;
        private (WorkingBeatmap working, RulesetInfo ruleset, IReadOnlyList<Mod> mods)? detailsKey;
        private double detailsKeyTime;
        private bool detailsRequested;
        private int detailsGeneration;

        private PlayTracker? playTracker;

        /// <summary>
        /// The state collected last.
        /// </summary>
        public BanchosucksOverlayState LatestState => latestState;

        /// <summary>
        /// Whether the server is listening.
        /// </summary>
        public bool IsRunning => serverRunning;

        /// <summary>
        /// Number of overlays connected through the websocket.
        /// </summary>
        public int ClientCount => server?.ClientCount ?? 0;

        /// <summary>
        /// The folder overlays are served from.
        /// </summary>
        public string OverlayFolder => storage.GetStorageForDirectory(OVERLAY_FOLDER).GetFullPath(string.Empty);

        public BanchosucksOverlayDataSource(Func<IScreen?> getCurrentScreen)
        {
            this.getCurrentScreen = getCurrentScreen;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            audioOffset = config.GetBindable<double>(OsuSetting.AudioOffset);
            dimLevel = config.GetBindable<double>(OsuSetting.DimLevel);
            showStoryboard = config.GetBindable<bool>(OsuSetting.ShowStoryboard);
            cursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize);
            hudVisibility = config.GetBindable<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode);
            windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);

            if (skins != null)
            {
                currentSkin = skins.CurrentSkinInfo.GetBoundCopy();
                // reading a live skin goes to the database, so only when it changes
                currentSkin.BindValueChanged(skin => skinName = skin.NewValue.PerformRead(s => s.Name), true);
            }

            enabled = config.GetBindable<bool>(OsuSetting.BanchosucksOverlayDataSource);
            enabled.BindValueChanged(e =>
            {
                if (e.NewValue)
                    start();
                else
                    stop();
            }, true);
        }

        #region server

        private void start()
        {
            if (server != null)
                return;

            var created = new BanchosucksOverlayServer(Port, () => BanchosucksOverlayJson.Build(latestState), OverlayFolder);
            server = created;

            // a server that was switched off a moment ago may still hold the port
            lastStop.ContinueWith(_ => created.StartAsync()).Unwrap().ContinueWith(startTask => Schedule(() =>
            {
                if (server != created)
                {
                    // switched off again in the meantime
                    shutDown(created);
                    return;
                }

                if (startTask.IsFaulted)
                {
                    server = null;
                    created.Dispose();
                    onStartFailed(startTask.Exception?.GetBaseException());
                    return;
                }

                serverRunning = true;
                Logger.Log($"Overlay data source: listening on http://127.0.0.1:{Port}/ (websocket {BanchosucksOverlayHttp.WEBSOCKET_PATH}), overlays from {OVERLAY_FOLDER}.");
            }));
        }

        private void onStartFailed(Exception? exception)
        {
            Logger.Log($"Overlay data source: could not listen on 127.0.0.1:{Port} ({exception?.Message}). Is tosu or another program using the port?", level: LogLevel.Important);

            notifications?.Post(new SimpleNotification
            {
                Icon = FontAwesome.Solid.ExclamationTriangle,
                Text = ClickerStrings.Text(
                    "Overlay data source: port {0} is in use (is tosu running?). Close the other program, then switch the setting off and on again.",
                    "Overlay-Datenquelle: Port {0} ist belegt (läuft tosu?). Das andere Programm schließen, dann die Einstellung aus- und wieder einschalten.",
                    Port),
            });
        }

        private void stop()
        {
            var running = server;

            if (running == null)
                return;

            server = null;

            if (!serverRunning)
            {
                // still starting: the start continuation sees that it was replaced and shuts it down
                return;
            }

            serverRunning = false;
            shutDown(running);
            Logger.Log("Overlay data source: stopped.");
        }

        private void shutDown(BanchosucksOverlayServer target)
        {
            lastStop = Task.Run(async () =>
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        await target.StopAsync(cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Overlay data source: stopping failed ({ex.Message}).");
                }
                finally
                {
                    target.Dispose();
                }
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            playTracker?.Dispose();

            var running = server;
            server = null;

            if (running != null)
            {
                // also when it was still starting: the continuation that would clean up no longer runs on a disposed component
                serverRunning = false;
                shutDown(running);
                // give the port back before the process (or the next test) goes on, but never hang the exit
                lastStop.Wait(TimeSpan.FromSeconds(6));
            }
        }

        #endregion

        protected override void Update()
        {
            base.Update();

            if (!serverRunning || server == null)
                return;

            double now = Clock.CurrentTime;

            if (now - lastGatherTime < PUBLISH_INTERVAL && now >= lastGatherTime)
                return;

            lastGatherTime = now;

            BanchosucksOverlayState state;

            try
            {
                state = gather(now);
            }
            catch (Exception ex)
            {
                // never let an overlay take the game down
                Logger.Log($"Overlay data source: collecting the state failed ({ex.Message}).");
                return;
            }

            latestState = state;

            var target = server;

            if (target.ClientCount == 0 || Interlocked.CompareExchange(ref publishing, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    target.Publish(BanchosucksOverlayJson.Build(state));
                }
                catch (Exception ex)
                {
                    Logger.Log($"Overlay data source: sending failed ({ex.Message}).");
                }
                finally
                {
                    Interlocked.Exchange(ref publishing, 0);
                }
            });
        }

        #region collecting the state

        private BanchosucksOverlayState gather(double now)
        {
            var screen = getCurrentScreen();
            var player = screen as Player;
            var gameplay = player?.GameplayState;

            var working = beatmap.Value;
            var info = working.BeatmapInfo;
            var selectedRuleset = ruleset.Value;
            var selectedMods = mods.Value;

            var state = new BanchosucksOverlayState
            {
                Game = gameStateFor(screen),
                ClientVersion = game.Version,
                // in whole seconds, so that an idle game does not produce a new document every tick
                SessionPlayTime = sessionTime.ElapsedMilliseconds / 1000 * 1000,
                RulesetId = selectedRuleset.OnlineID,
            };

            fillSettings(state.Settings);
            fillProfile(state, screen);

            // ---- beatmap
            var map = state.Beatmap;
            var metadata = info.Metadata;

            map.Artist = metadata.Artist;
            map.ArtistUnicode = string.IsNullOrEmpty(metadata.ArtistUnicode) ? metadata.Artist : metadata.ArtistUnicode;
            map.Title = metadata.Title;
            map.TitleUnicode = string.IsNullOrEmpty(metadata.TitleUnicode) ? metadata.Title : metadata.TitleUnicode;
            map.Mapper = metadata.Author.Username;
            map.Version = info.DifficultyName;
            map.Id = Math.Max(0, info.OnlineID);
            map.SetId = Math.Max(0, info.BeatmapSet?.OnlineID ?? 0);
            map.Checksum = info.MD5Hash;
            map.StatusNumber = BanchosucksOverlayMapping.RankedStatus(info.Status);
            map.RulesetId = info.Ruleset.OnlineID;
            map.IsConvert = info.Ruleset.OnlineID != selectedRuleset.OnlineID;

            var track = music?.CurrentTrack;
            double time = track?.CurrentTime ?? 0;
            map.TimeLive = time;
            map.TimeMp3Length = track?.Length ?? 0;

            map.ApproachRate = info.Difficulty.ApproachRate;
            map.CircleSize = info.Difficulty.CircleSize;
            map.OverallDifficulty = info.Difficulty.OverallDifficulty;
            map.DrainRate = info.Difficulty.DrainRate;

            var activeMods = gameplay?.Mods ?? selectedMods;
            var activeRuleset = gameplay?.Ruleset.RulesetInfo ?? selectedRuleset;
            double rate = ModUtils.CalculateRateWithMods(activeMods);

            // until the details are in, what the database knows about the beatmap
            map.ApproachRateConverted = map.ApproachRate;
            map.CircleSizeConverted = map.CircleSize;
            map.OverallDifficultyConverted = map.OverallDifficulty;
            map.DrainRateConverted = map.DrainRate;
            map.Stars = info.StarRating;
            map.StarsLive = info.StarRating;
            map.BpmCommon = map.BpmMin = map.BpmMax = map.BpmRealtime = info.BPM * rate;
            map.Objects = info.TotalObjectCount;

            updateDetailsRequest(working, selectedRuleset, selectedMods, now);

            var known = details;

            if (known != null && ReferenceEquals(known.Working, working))
            {
                map.TimeFirstObject = known.FirstObject;
                map.TimeLastObject = known.LastObject;
                map.Circles = known.Circles;
                map.Sliders = known.Sliders;
                map.Spinners = known.Spinners;
                map.Holds = known.Holds;
                map.Objects = known.Objects;
                map.BpmMin = known.BpmMin * rate;
                map.BpmMax = known.BpmMax * rate;

                if (known.Beatmap != null)
                {
                    map.BpmRealtime = known.Beatmap.ControlPointInfo.TimingPointAt(time).BPM * rate;
                    map.IsKiai = known.Beatmap.ControlPointInfo.EffectPointAt(time).KiaiMode;
                }

                // these depend on the ruleset and mods the details were worked out for
                if (ReferenceEquals(known.Mods, selectedMods) && known.Ruleset.Equals(selectedRuleset))
                {
                    map.Stars = map.StarsLive = known.Stars;
                    map.MaxPP = known.MaxPP;
                    map.MaxCombo = known.MaxCombo;

                    if (known.Adjusted != null)
                    {
                        map.ApproachRateConverted = known.Adjusted.ApproachRate;
                        map.CircleSizeConverted = known.Adjusted.CircleSize;
                        map.OverallDifficultyConverted = known.Adjusted.OverallDifficulty;
                        map.DrainRateConverted = known.Adjusted.DrainRate;
                    }
                }
            }

            // ---- play
            var play = state.Play;

            play.PlayerName = api.IsLoggedIn ? api.LocalUser.Value.Username : string.Empty;
            play.RulesetId = activeRuleset.OnlineID;
            play.Mods = modsStateFor(activeMods, activeRuleset, rate);

            if (player != null && gameplay != null)
            {
                if (playTracker?.Player != player)
                {
                    playTracker?.Dispose();
                    playTracker = new PlayTracker(player, gameplay, difficultyCache, action => Schedule(action));
                    sessionPlayCount++;
                }

                playTracker.Update();

                var scoreProcessor = gameplay.ScoreProcessor;

                play.Active = true;
                play.PlayerName = gameplay.Score.ScoreInfo.User.Username;
                play.Score = scoreProcessor.TotalScore.Value;
                play.Accuracy = scoreProcessor.Accuracy.Value * 100;
                play.Combo = scoreProcessor.Combo.Value;
                play.MaxCombo = scoreProcessor.HighestCombo.Value;
                play.Health = gameplay.HealthProcessor.Health.Value;
                play.Rank = scoreProcessor.JudgedHits > 0 ? scoreProcessor.Rank.Value.ToString() : string.Empty;
                BanchosucksOverlayMapping.FillHits(play.Hits, scoreProcessor.Statistics, activeRuleset.OnlineID);

                play.PP = playTracker.PP;
                play.PPIfFullCombo = playTracker.PPIfFullCombo;
                play.PPMaxThisPlay = playTracker.PPMax;
                play.UnstableRate = playTracker.UnstableRate;

                if (playTracker.StarsLive != null)
                    map.StarsLive = playTracker.StarsLive.Value;

                map.IsBreak = player.IsBreakTime.Value;
            }
            else if (playTracker != null)
            {
                playTracker.Dispose();
                playTracker = null;
            }

            state.SessionPlayCount = sessionPlayCount;

            // ---- results
            if (screen is ResultsScreen resultsScreen && (resultsScreen.SelectedScore.Value ?? resultsScreen.Score) is ScoreInfo score)
            {
                var results = state.Results;

                results.Active = true;
                results.ScoreId = Math.Max(0, score.OnlineID);
                results.PlayerName = score.User.Username;
                results.RulesetId = score.Ruleset.OnlineID;
                results.Score = score.TotalScore;
                results.Accuracy = score.Accuracy * 100;
                results.MaxCombo = score.MaxCombo;
                results.Rank = score.Rank.ToString();
                results.PP = score.PP ?? 0;
                results.CreatedAt = score.Date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                results.Mods = modsStateFor(score.Mods, score.Ruleset, ModUtils.CalculateRateWithMods(score.Mods));
                BanchosucksOverlayMapping.FillHits(results.Hits, score.Statistics, score.Ruleset.OnlineID);
            }

            return state;
        }

        private static BanchosucksOverlayGameState gameStateFor(IScreen? screen)
        {
            switch (screen)
            {
                case Player:
                case PlayerLoader:
                    return BanchosucksOverlayGameState.Play;

                case ResultsScreen:
                    return BanchosucksOverlayGameState.ResultScreen;

                case SongSelect:
                    return BanchosucksOverlayGameState.SelectPlay;

                case Editor:
                    return BanchosucksOverlayGameState.Edit;

                case OnlinePlayScreen:
                    return BanchosucksOverlayGameState.Lobby;

                default:
                    return BanchosucksOverlayGameState.Menu;
            }
        }

        private void fillSettings(BanchosucksOverlayState.SettingsState settings)
        {
            settings.InterfaceVisible = hudVisibility.Value != HUDVisibilityMode.Never;
            settings.Fullscreen = windowMode.Value == WindowMode.Fullscreen;

            if (host.Window != null)
            {
                settings.WindowWidth = host.Window.ClientSize.Width;
                settings.WindowHeight = host.Window.ClientSize.Height;
            }

            settings.SkinName = skinName;
            settings.VolumeMaster = audio.Volume.Value * 100;
            settings.VolumeMusic = audio.VolumeTrack.Value * 100;
            settings.VolumeEffect = audio.VolumeSample.Value * 100;
            settings.AudioOffset = audioOffset.Value;
            settings.BackgroundDim = dimLevel.Value * 100;
            settings.Storyboard = showStoryboard.Value;
            settings.CursorSize = cursorSize.Value;
        }

        private void fillProfile(BanchosucksOverlayState state, IScreen? screen)
        {
            var profile = state.Profile;

            profile.Online = api.State.Value == APIState.Online;

            switch (screen)
            {
                case ReplayPlayer:
                case SpectatorPlayer:
                    profile.ActivityNumber = 6;
                    profile.ActivityName = "watching";
                    break;

                case Player:
                    profile.ActivityNumber = 2;
                    profile.ActivityName = "playing";
                    break;

                case Editor:
                    profile.ActivityNumber = 3;
                    profile.ActivityName = "editing";
                    break;

                case OnlinePlayScreen:
                    profile.ActivityNumber = 11;
                    profile.ActivityName = "lobby";
                    break;
            }

            if (!api.IsLoggedIn)
                return;

            var user = api.LocalUser.Value;

            profile.Id = user.OnlineID;
            profile.Name = user.Username;

            var statistics = statisticsProvider?.GetStatisticsFor(ruleset.Value);

            if (statistics == null)
                return;

            profile.RankedScore = statistics.RankedScore;
            profile.Level = statistics.Level.Current + statistics.Level.Progress / 100.0;
            profile.Accuracy = statistics.Accuracy;
            profile.PP = (double)(statistics.PP ?? 0);
            profile.PlayCount = statistics.PlayCount;
            profile.GlobalRank = statistics.GlobalRank ?? 0;
        }

        private BanchosucksOverlayState.ModsState modsStateFor(IReadOnlyList<Mod> list, RulesetInfo rulesetInfo, double rate)
        {
            var result = new BanchosucksOverlayState.ModsState { Rate = rate };

            if (list.Count == 0)
                return result;

            string[] acronyms = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                acronyms[i] = list[i].Acronym;

            result.Acronyms = acronyms;

            try
            {
                if (rulesetInstance == null || rulesetInstanceInfo?.Equals(rulesetInfo) != true)
                {
                    rulesetInstance = rulesetInfo.CreateInstance();
                    rulesetInstanceInfo = rulesetInfo;
                }

                result.Number = (int)rulesetInstance.ConvertToLegacyMods(list.ToArray());
            }
            catch (Exception)
            {
                // a ruleset that is not installed (any more): the acronyms have to do
            }

            return result;
        }

        #endregion

        #region beatmap details

        private void updateDetailsRequest(WorkingBeatmap working, RulesetInfo selectedRuleset, IReadOnlyList<Mod> selectedMods, double now)
        {
            if (detailsKey == null
                || !ReferenceEquals(detailsKey.Value.working, working)
                || !ReferenceEquals(detailsKey.Value.mods, selectedMods)
                || !detailsKey.Value.ruleset.Equals(selectedRuleset))
            {
                detailsKey = (working, selectedRuleset, selectedMods);
                detailsKeyTime = now;
                detailsRequested = false;
                detailsGeneration++;
                return;
            }

            if (detailsRequested || now - detailsKeyTime < details_debounce)
                return;

            detailsRequested = true;

            int generation = detailsGeneration;
            Mod[] modsCopy = selectedMods.ToArray();

            Task.Run(async () =>
            {
                var computed = await computeDetails(working, selectedRuleset, selectedMods, modsCopy).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (generation == detailsGeneration)
                        details = computed;
                });
            });
        }

        private async Task<BeatmapDetails> computeDetails(WorkingBeatmap working, RulesetInfo rulesetInfo, IReadOnlyList<Mod> modsReference, Mod[] modsCopy)
        {
            var result = new BeatmapDetails(working, rulesetInfo, modsReference);

            try
            {
                var info = working.BeatmapInfo;

                result.Adjusted = rulesetInfo.CreateInstance().GetAdjustedDisplayDifficulty(info, modsCopy);

                // the beatmap as it is in the file, not converted to the selected ruleset
                IBeatmap content = working.Beatmap;

                result.Beatmap = content;
                result.Objects = content.HitObjects.Count;
                result.BpmMin = content.ControlPointInfo.BPMMinimum;
                result.BpmMax = content.ControlPointInfo.BPMMaximum;

                if (content.HitObjects.Count > 0)
                {
                    result.FirstObject = content.HitObjects[0].StartTime;

                    foreach (var hitObject in content.HitObjects)
                    {
                        result.LastObject = Math.Max(result.LastObject, hitObject.GetEndTime());

                        switch (hitObject)
                        {
                            case IHasPath:
                                result.Sliders++;
                                break;

                            case IHasDuration:
                                if (info.Ruleset.OnlineID == 3)
                                    result.Holds++;
                                else
                                    result.Spinners++;
                                break;

                            default:
                                result.Circles++;
                                break;
                        }
                    }
                }

                var difficulty = await difficultyCache.GetDifficultyAsync(info, rulesetInfo, modsCopy).ConfigureAwait(false);

                result.Stars = difficulty?.Stars ?? info.StarRating;
                result.MaxCombo = difficulty?.MaxCombo ?? 0;
                result.MaxPP = difficulty?.PerformanceAttributes?.Total ?? 0;
            }
            catch (Exception ex)
            {
                Logger.Log($"Overlay data source: beatmap details failed ({ex.Message}).");
            }

            return result;
        }

        private sealed class BeatmapDetails
        {
            public readonly WorkingBeatmap Working;
            public readonly RulesetInfo Ruleset;
            public readonly IReadOnlyList<Mod> Mods;

            public IBeatmap? Beatmap;
            public BeatmapDifficulty? Adjusted;
            public double FirstObject;
            public double LastObject;
            public int Circles;
            public int Sliders;
            public int Spinners;
            public int Holds;
            public int Objects;
            public double BpmMin;
            public double BpmMax;
            public double Stars;
            public double MaxPP;
            public int MaxCombo;

            public BeatmapDetails(WorkingBeatmap working, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
            {
                Working = working;
                Ruleset = ruleset;
                Mods = mods;
            }
        }

        #endregion

        #region live pp

        /// <summary>
        /// Follows one play: pp so far, pp if it ended as a full combo, unstable rate. Works like the pp counter of
        /// the HUD (<see cref="Screens.Play.HUD.PerformancePointsCounter"/>): the difficulty of the map up to every
        /// object is worked out once in the background, pp is recalculated when a judgement came in.
        /// </summary>
        private sealed class PlayTracker : IDisposable
        {
            public readonly Player Player;

            public BanchosucksOverlayState.PerformanceState PP { get; private set; } = new BanchosucksOverlayState.PerformanceState();
            public BanchosucksOverlayState.PerformanceState PPIfFullCombo { get; private set; } = new BanchosucksOverlayState.PerformanceState();
            public double PPMax { get; private set; }
            public double UnstableRate { get; private set; }
            public double? StarsLive { get; private set; }

            private readonly GameplayState gameplay;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly PerformanceCalculator? calculator;
            private readonly ScoreInfo score;
            private readonly ScoreInfo fullComboScore;

            private List<TimedDifficultyAttributes>? timedAttributes;
            private HitEventExtensions.UnstableRateCalculationResult? unstableRate;
            private int lastJudgedHits = -1;
            private int lastCombo = -1;
            private bool failed;

            public PlayTracker(Player player, GameplayState gameplay, BeatmapDifficultyCache difficultyCache, Action<Action> schedule)
            {
                Player = player;
                this.gameplay = gameplay;

                Mod[] clonedMods = gameplay.Mods.Select(m => m.DeepClone()).ToArray();

                score = new ScoreInfo(gameplay.Score.ScoreInfo.BeatmapInfo, gameplay.Score.ScoreInfo.Ruleset) { Mods = clonedMods };
                fullComboScore = new ScoreInfo(gameplay.Score.ScoreInfo.BeatmapInfo, gameplay.Score.ScoreInfo.Ruleset) { Mods = clonedMods };

                try
                {
                    calculator = gameplay.Ruleset.CreatePerformanceCalculator();
                }
                catch (Exception)
                {
                    calculator = null;
                }

                if (calculator == null)
                    return;

                difficultyCache.GetTimedDifficultyAttributesAsync(new GameplayWorkingBeatmap(gameplay.Beatmap), gameplay.Ruleset, clonedMods, cancellation.Token)
                               .ContinueWith(task => schedule(() =>
                               {
                                   timedAttributes = task.GetResultSafely();
                                   // recalculate with the next update
                                   lastJudgedHits = -1;
                               }), TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            public void Update()
            {
                var processor = gameplay.ScoreProcessor;

                if (processor.JudgedHits == lastJudgedHits && processor.Combo.Value == lastCombo)
                    return;

                lastJudgedHits = processor.JudgedHits;
                lastCombo = processor.Combo.Value;

                if (failed)
                    return;

                try
                {
                    // rewinding a replay makes the incremental result useless
                    if (unstableRate != null && processor.HitEvents.Count <= unstableRate.LastProcessedIndex)
                        unstableRate = null;

                    unstableRate = processor.HitEvents.CalculateUnstableRate(unstableRate);
                    UnstableRate = unstableRate?.Result ?? 0;

                    recalculatePerformance(processor);
                }
                catch (Exception ex)
                {
                    // a ruleset whose calculator cannot cope: no pp for this play, everything else goes on
                    failed = true;
                    Logger.Log($"Overlay data source: live pp failed ({ex.Message}).");
                }
            }

            private void recalculatePerformance(ScoreProcessor processor)
            {
                var last = gameplay.LastJudgementResult.Value;

                if (calculator == null || timedAttributes == null || timedAttributes.Count == 0 || last == null)
                    return;

                int index = timedAttributes.BinarySearch(new TimedDifficultyAttributes(last.HitObject.GetEndTime(), null!));
                if (index < 0)
                    index = ~index - 1;

                var soFar = timedAttributes[Math.Clamp(index, 0, timedAttributes.Count - 1)].Attributes;
                var whole = timedAttributes[^1].Attributes;

                processor.PopulateScore(score);

                PP = toState(calculator.Calculate(score, soFar));
                PPMax = Math.Max(PPMax, PP.Total);
                StarsLive = soFar.StarRating;

                var fullComboStatistics = BanchosucksOverlayFullCombo.Statistics(score.Statistics, score.MaximumStatistics);

                fullComboScore.Statistics = fullComboStatistics;
                fullComboScore.MaximumStatistics = new Dictionary<HitResult, int>(score.MaximumStatistics);
                fullComboScore.MaxCombo = whole.MaxCombo;
                fullComboScore.Combo = whole.MaxCombo;
                fullComboScore.Accuracy = StandardisedScoreMigrationTools.ComputeAccuracy(fullComboStatistics, score.MaximumStatistics, processor);
                fullComboScore.TotalScore = score.TotalScore;
                fullComboScore.Rank = score.Rank;

                PPIfFullCombo = toState(calculator.Calculate(fullComboScore, whole));
            }

            private static BanchosucksOverlayState.PerformanceState toState(PerformanceAttributes attributes)
            {
                var state = new BanchosucksOverlayState.PerformanceState { Total = attributes.Total };

                foreach (var part in attributes.GetAttributesForDisplay())
                {
                    switch (part.PropertyName)
                    {
                        case "Aim":
                            state.Aim = part.Value;
                            break;

                        case "Speed":
                            state.Speed = part.Value;
                            break;

                        case "Accuracy":
                            state.Accuracy = part.Value;
                            break;

                        case "Difficulty":
                            state.Difficulty = part.Value;
                            break;

                        case "Flashlight":
                            state.Flashlight = part.Value;
                            break;
                    }
                }

                return state;
            }

            public void Dispose()
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            /// <summary>
            /// Hands the beatmap that is being played to the difficulty calculation (same trick as the pp counter's).
            /// </summary>
            private class GameplayWorkingBeatmap : WorkingBeatmap
            {
                private readonly IBeatmap gameplayBeatmap;

                public GameplayWorkingBeatmap(IBeatmap gameplayBeatmap)
                    : base(gameplayBeatmap.BeatmapInfo, null)
                {
                    this.gameplayBeatmap = gameplayBeatmap;
                }

                public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                    => gameplayBeatmap;

                protected override IBeatmap GetBeatmap() => gameplayBeatmap;

                public override Texture GetBackground() => throw new NotImplementedException();

                protected override Track GetBeatmapTrack() => throw new NotImplementedException();

                protected internal override ISkin GetSkin() => throw new NotImplementedException();

                public override Stream GetStream(string storagePath) => throw new NotImplementedException();
            }
        }

        #endregion
    }
}

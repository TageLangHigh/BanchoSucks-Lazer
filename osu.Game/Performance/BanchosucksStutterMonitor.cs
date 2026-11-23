// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings.Sections.Graphics;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using GameplayBeatmapSettings = osu.Game.Overlays.Settings.Sections.Gameplay.BeatmapSettings;

namespace osu.Game.Performance
{
    /// <summary>
    /// Banchosucks: the stutter report. Records the frame times of the update and the draw thread while the player is
    /// actively playing and, if there were noticeable stutters, posts one notification when the results screen opens.
    /// </summary>
    /// <remarks>
    /// Only frames in <see cref="LocalUserPlayingState.Playing"/> count: no breaks, no pause menu, no replays, no loading.
    /// The update thread is measured between two <see cref="Update"/> calls of this component, the draw thread between
    /// two runs of a task scheduled on it for every frame (only while a play is being recorded). Each sample costs one
    /// timestamp and one array increment, see <see cref="BanchosucksFrameTimeHistogram"/>. What counts as a stutter and
    /// which hint is given is decided by <see cref="BanchosucksStutterAnalysis"/>.
    /// </remarks>
    public partial class BanchosucksStutterMonitor : Component
    {
        /// <summary>
        /// How long after the play the results screen may take to appear before the report is dropped.
        /// </summary>
        private const double results_timeout = 15000;

        private readonly Func<IScreen?> getCurrentScreen;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? playInfo { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private SettingsOverlay? settings { get; set; }

        /// <summary>
        /// Update thread frames of the current or last play.
        /// </summary>
        public readonly BanchosucksFrameTimeHistogram UpdateFrames = new BanchosucksFrameTimeHistogram();

        /// <summary>
        /// Draw thread frames of the current or last play. Written on the draw thread while <see cref="drawRecording"/>.
        /// </summary>
        public readonly BanchosucksFrameTimeHistogram DrawFrames = new BanchosucksFrameTimeHistogram();

        private Bindable<bool> enabled = null!;
        private IBindable<LocalUserPlayingState>? playingState;

        /// <summary>
        /// A play is being recorded (from its first active frame until the player leaves it).
        /// </summary>
        private bool sessionActive;

        private bool windowWasInactive;
        private bool storyboardShown;
        private long lastUpdateTimestamp;

        private ScheduledDelegate? drawProbe;
        private volatile bool drawRecording;
        private long lastDrawTimestamp;

        private double? awaitingResultsUntil;

        public BanchosucksStutterMonitor(Func<IScreen?> getCurrentScreen)
        {
            this.getCurrentScreen = getCurrentScreen;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled = config.GetBindable<bool>(OsuSetting.BanchosucksStutterReport);

            if (playInfo != null)
            {
                playingState = playInfo.PlayingState.GetBoundCopy();
                playingState.BindValueChanged(onPlayingStateChanged);
            }
        }

        private void onPlayingStateChanged(ValueChangedEvent<LocalUserPlayingState> state)
        {
            if (state.NewValue == LocalUserPlayingState.NotPlaying)
            {
                if (!sessionActive)
                    return;

                endSession();
                awaitingResultsUntil = Time.Current + results_timeout;
                return;
            }

            if (state.OldValue == LocalUserPlayingState.NotPlaying)
                beginSession();
        }

        private void beginSession()
        {
            endSession();
            awaitingResultsUntil = null;

            if (!enabled.Value)
                return;

            UpdateFrames.Reset();
            DrawFrames.Reset();

            windowWasInactive = false;
            storyboardShown = config.Get<bool>(OsuSetting.ShowStoryboard) && (getCurrentScreen() as Player)?.GameplayState?.Storyboard.HasDrawable == true;
            lastUpdateTimestamp = 0;
            lastDrawTimestamp = 0;

            sessionActive = true;
            drawProbe = host.DrawThread.Scheduler.AddDelayed(onDrawFrame, 0, true);
        }

        private void endSession()
        {
            sessionActive = false;
            drawRecording = false;

            drawProbe?.Cancel();
            drawProbe = null;
        }

        /// <summary>
        /// Runs on the draw thread once per draw frame while a play is recorded.
        /// </summary>
        private void onDrawFrame()
        {
            if (!drawRecording)
            {
                lastDrawTimestamp = 0;
                return;
            }

            long now = Stopwatch.GetTimestamp();

            if (lastDrawTimestamp != 0)
                DrawFrames.Add(Stopwatch.GetElapsedTime(lastDrawTimestamp, now).TotalMilliseconds);

            lastDrawTimestamp = now;
        }

        protected override void Update()
        {
            base.Update();

            bool recording = sessionActive && playingState?.Value == LocalUserPlayingState.Playing;

            drawRecording = recording;

            if (recording)
            {
                long now = Stopwatch.GetTimestamp();

                if (lastUpdateTimestamp != 0)
                    UpdateFrames.Add(Stopwatch.GetElapsedTime(lastUpdateTimestamp, now).TotalMilliseconds);

                lastUpdateTimestamp = now;

                if (!host.IsActive.Value)
                    windowWasInactive = true;
            }
            else
                lastUpdateTimestamp = 0;

            if (awaitingResultsUntil != null)
                checkForResults();
        }

        private void checkForResults()
        {
            var screen = getCurrentScreen();

            if (screen is ResultsScreen)
            {
                awaitingResultsUntil = null;
                report();
                return;
            }

            // the player stays the current screen until the results are pushed; anything else means the play was left
            if (screen is not Player || Time.Current > awaitingResultsUntil)
                awaitingResultsUntil = null;
        }

        private void report()
        {
            if (!enabled.Value)
                return;

            var result = BanchosucksStutterAnalysis.CreateReport(UpdateFrames, DrawFrames, new BanchosucksStutterEnvironment
            {
                WindowWasInactive = windowWasInactive,
                SingleThreaded = frameworkConfig.Get<ExecutionMode>(FrameworkSetting.ExecutionMode) == ExecutionMode.SingleThread,
                UnlimitedFrameLimiter = frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync) == FrameSync.Unlimited,
                InputRateHz = (int)config.Get<BanchosucksThreadRate>(OsuSetting.BanchosucksInputRate),
                AudioRateHz = (int)config.Get<BanchosucksThreadRate>(OsuSetting.BanchosucksAudioRate),
                StoryboardShown = storyboardShown,
            });

            Logger.Log($"Banchosucks stutter report: update {UpdateFrames.Count} frames (longest {UpdateFrames.Longest:0.0} ms), "
                       + $"draw {DrawFrames.Count} frames (longest {DrawFrames.Longest:0.0} ms), {result?.Stutters ?? 0} stutters, hint {result?.Hint ?? BanchosucksStutterHint.None}");

            if (result == null)
                return;

            notifications?.Post(new BanchosucksStutterNotification(result)
            {
                Activated = () =>
                {
                    openSettingsFor(result.Hint);
                    return true;
                }
            });
        }

        private void openSettingsFor(BanchosucksStutterHint hint)
        {
            switch (hint)
            {
                case BanchosucksStutterHint.SingleThreaded:
                case BanchosucksStutterHint.UnlimitedFrameLimiter:
                case BanchosucksStutterHint.HighThreadRate:
                    settings?.ShowAtControl<RendererSettings>();
                    break;

                case BanchosucksStutterHint.Storyboard:
                    settings?.ShowAtControl<GameplayBeatmapSettings>();
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            drawRecording = false;
            drawProbe?.Cancel();
        }

        /// <summary>
        /// The notification of the stutter report. Closing it is all it takes to dismiss; clicking opens the setting the hint is about.
        /// </summary>
        public partial class BanchosucksStutterNotification : SimpleNotification
        {
            public readonly BanchosucksStutterReport Report;

            public BanchosucksStutterNotification(BanchosucksStutterReport report)
            {
                Report = report;

                Icon = FontAwesome.Solid.TachometerAlt;
                Text = ClickerStrings.Text(BanchosucksStutterAnalysis.Describe(report, false), BanchosucksStutterAnalysis.Describe(report, true));
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Threading;

namespace osu.Game.Updater
{
    /// <summary>
    /// Banchosucks: the update step that runs while the game starts. The platform's <see cref="UpdateManager"/> drives it from a
    /// background thread (ask the feed, download, hand over the restart); the <see cref="Screens.Loader"/> polls it every frame,
    /// waits before the intro and shows the progress. Plain fields with volatile access instead of bindables, because the two
    /// sides live on different threads.
    /// </summary>
    public class StartupUpdate
    {
        public enum Phase
        {
            /// <summary>The feed is being asked; the loader shows its usual spinner.</summary>
            Checking,

            /// <summary>A newer version is being downloaded; the loader shows the popup with <see cref="Progress"/>.</summary>
            Downloading,

            /// <summary>Downloaded; the loader exits the game with <see cref="ApplyOnExit"/> so the updater can install and relaunch.</summary>
            ReadyToRestart,

            /// <summary>Nothing (more) to do: up to date, offline, not installed via the setup, or failed. The game starts normally.</summary>
            Finished,
        }

        private int phase;
        private int progressPermille;

        public Phase State => (Phase)Volatile.Read(ref phase);

        /// <summary>
        /// Download progress from 0 to 1.
        /// </summary>
        public float Progress => Volatile.Read(ref progressPermille) / 1000f;

        /// <summary>
        /// The version being installed, known from <see cref="Phase.Downloading"/> on.
        /// </summary>
        public string? Version { get; private set; }

        /// <summary>
        /// Why the step ended, for the log.
        /// </summary>
        public string? Outcome { get; private set; }

        /// <summary>
        /// What the game runs after it exited: the updater applies the downloaded package and starts the game again.
        /// Set before <see cref="Phase.ReadyToRestart"/>.
        /// </summary>
        public Action? ApplyOnExit { get; private set; }

        public void BeginDownload(string version)
        {
            Version = version;
            Volatile.Write(ref phase, (int)Phase.Downloading);
        }

        public void ReportProgress(float progress) => Volatile.Write(ref progressPermille, (int)(Math.Clamp(progress, 0, 1) * 1000));

        public void ReadyToRestart(Action applyOnExit)
        {
            ApplyOnExit = applyOnExit;
            Volatile.Write(ref progressPermille, 1000);
            Volatile.Write(ref phase, (int)Phase.ReadyToRestart);
        }

        public void Finish(string outcome)
        {
            Outcome = outcome;
            Volatile.Write(ref phase, (int)Phase.Finished);
        }
    }
}

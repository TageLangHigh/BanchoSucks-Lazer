// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Logging;
using osu.Game.Screens.Menu;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Banchosucks;
using osu.Game.Updater;

namespace osu.Game.Screens
{
    public partial class Loader : StartupScreen
    {
        public Loader()
        {
            ValidForResume = false;
        }

        private OsuScreen loadableScreen;
        private ShaderPrecompiler precompiler;

        private LoadingSpinner spinner;
        private ScheduledDelegate spinnerShow;

        [Resolved(canBeNull: true)]
        private UpdateManager updateManager { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGameBase game { get; set; }

        private StartupUpdate startupUpdate;
        private StartupUpdatePopup updatePopup;
        private double startupUpdateDeadline;
        private bool restartRequested;

        /// <summary>
        /// Banchosucks: how long the loader waits for the startup update check at most. The check gives up on its own earlier;
        /// this only guards against a hanging updater, and shaders and the intro screen usually take longer than the check anyway.
        /// </summary>
        protected virtual double StartupUpdateCheckTimeout => 15000;

        protected virtual OsuScreen CreateLoadableScreen() => new Disclaimer(new IntroSimple(() => new MainMenu()));

        protected virtual ShaderPrecompiler CreateShaderPrecompiler() => new ShaderPrecompiler();

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            // Banchosucks: the update manager began asking our releases when the game was constructed. The intro waits until
            // that answer is in, and a newer version is downloaded and installed right here instead of starting the old one.
            startupUpdate = updateManager?.StartupUpdate;
            startupUpdateDeadline = Clock.CurrentTime + StartupUpdateCheckTimeout;

            LoadComponentAsync(precompiler = CreateShaderPrecompiler(), AddInternal);

            LoadComponentAsync(loadableScreen = CreateLoadableScreen());

            LoadComponentAsync(spinner = new LoadingSpinner(true, true)
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Margin = new MarginPadding(40),
            }, _ =>
            {
                AddInternal(spinner);

                // the update popup has its own progress, no spinner next to it
                if (updatePopup == null)
                    spinnerShow = Scheduler.AddDelayed(spinner.Show, 200);
            });

            checkIfLoaded();
        }

        private void checkIfLoaded()
        {
            if (loadableScreen?.LoadState != LoadState.Ready || !precompiler.FinishedCompiling || !startupUpdateDone())
            {
                Schedule(checkIfLoaded);
                return;
            }

            spinnerShow?.Cancel();

            if (spinner.State.Value == Visibility.Visible)
            {
                spinner.Hide();
                Scheduler.AddDelayed(() => this.Push(loadableScreen), LoadingSpinner.TRANSITION_DURATION);
            }
            else
                this.Push(loadableScreen);
        }

        /// <summary>
        /// Banchosucks: whether the game may go on to the intro. False while the update check is pending (bounded by
        /// <see cref="StartupUpdateCheckTimeout"/>) and for good once a newer version is being installed: the game exits
        /// into the updater then and comes back as the new version.
        /// </summary>
        private bool startupUpdateDone()
        {
            if (startupUpdate == null)
                return true;

            switch (startupUpdate.State)
            {
                case StartupUpdate.Phase.Finished:
                    Logger.Log($"Loader: startup update step finished ({startupUpdate.Outcome})");
                    startupUpdate = null;
                    return true;

                case StartupUpdate.Phase.Checking:
                    if (Clock.CurrentTime < startupUpdateDeadline)
                        return false;

                    Logger.Log("Loader: startup update check took too long, starting the game without it");
                    startupUpdate = null;
                    return true;

                case StartupUpdate.Phase.Downloading:
                    showUpdatePopup();
                    updatePopup.Progress = startupUpdate.Progress;
                    return false;

                case StartupUpdate.Phase.ReadyToRestart:
                    showUpdatePopup();

                    if (!restartRequested)
                    {
                        restartRequested = true;
                        updatePopup.ShowRestarting();
                        // a moment for the full bar to be seen, then hand over to the updater
                        Scheduler.AddDelayed(() => ExitToApplyUpdate(startupUpdate.ApplyOnExit), 600);
                    }

                    return false;

                default:
                    return true;
            }
        }

        private void showUpdatePopup()
        {
            if (updatePopup != null)
                return;

            spinnerShow?.Cancel();
            spinner?.Hide();
            AddInternal(updatePopup = new StartupUpdatePopup(startupUpdate.Version));
        }

        /// <summary>
        /// Banchosucks: leaves the game so the Velopack updater can apply the downloaded version and start the game again.
        /// Exiting from the loader is never blocked (<see cref="OsuGame.OnExiting"/>). Tests override this.
        /// </summary>
        protected virtual void ExitToApplyUpdate(Action applyOnExit)
        {
            if (game == null)
                return;

            game.RestartOnExitAction = applyOnExit;
            game.Exit();
        }

        /// <summary>
        /// Compiles a set of shaders before continuing. Attempts to draw some frames between compilation by limiting to one compile per draw frame.
        /// </summary>
        public partial class ShaderPrecompiler : Drawable
        {
            private readonly List<IShader> loadTargets = new List<IShader>();

            public bool FinishedCompiling { get; private set; }

            [BackgroundDependencyLoader]
            private void load(ShaderManager manager)
            {
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.BLUR));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_3, FragmentShaderDescriptor.TEXTURE));

                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"TriangleBorder"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"FastCircle"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"CircularProgress"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"ArgonBarPath"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"ArgonBarPathBackground"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"SaturationSelectorBackground"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"HueSelectorBackground"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, "Path"));
                loadTargets.Add(manager.Load("PathPrepass", "PathPrepass"));
                loadTargets.Add(manager.Load(@"LogoAnimation", @"LogoAnimation"));

                // Ruleset local shader usage (should probably move somewhere else).
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"SpinnerGlow"));
                loadTargets.Add(manager.Load(@"CursorTrail", FragmentShaderDescriptor.TEXTURE));
            }

            protected virtual bool AllLoaded => loadTargets.All(s => s.IsLoaded);

            protected override void Update()
            {
                base.Update();

                // if our target is null we are done.
                if (AllLoaded)
                {
                    FinishedCompiling = true;
                    Expire();
                }
            }
        }
    }
}

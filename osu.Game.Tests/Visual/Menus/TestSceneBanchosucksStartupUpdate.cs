// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens;
using osu.Game.Screens.Banchosucks;
using osu.Game.Screens.Menu;
using osu.Game.Updater;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// Banchosucks: the loader waits for the startup update step, shows the popup while a version downloads and hands the
    /// restart to the updater instead of starting the intro; a hanging check does not keep the game from starting.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBanchosucksStartupUpdate : ScreenTestScene
    {
        [Cached]
        private readonly OsuLogo logo;

        [Cached(typeof(UpdateManager))]
        private readonly TestUpdateManager updateManager = new TestUpdateManager();

        private TestLoader loader = null!;

        public TestSceneBanchosucksStartupUpdate()
        {
            Child = logo = new OsuLogo
            {
                Alpha = 0,
                Depth = float.MinValue
            };
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("fresh startup step", () => updateManager.Startup = new StartupUpdate());
        }

        [Test]
        public void TestUpToDateStartsTheGame()
        {
            AddStep("load", () => LoadScreen(loader = new TestLoader()));
            AddWaitStep("wait while checking", 5);
            AddAssert("intro not started yet", () => loader.IsCurrentScreen() && !loader.ScreenLoaded);
            AddStep("check says up to date", () => updateManager.Startup.Finish("up to date"));
            AddUntilStep("intro started", () => loader.ScreenLoaded);
            AddAssert("no popup", () => loader.Popup == null);
        }

        [Test]
        public void TestDownloadShowsPopupAndRestarts()
        {
            AddStep("load", () => LoadScreen(loader = new TestLoader()));
            AddStep("update found", () => updateManager.Startup.BeginDownload("2026.913.99"));
            AddUntilStep("popup names the version", () => loader.Popup != null && hasText(loader.Popup, t => t.Contains("2026.913.99", StringComparison.Ordinal)));
            AddStep("half downloaded", () => updateManager.Startup.ReportProgress(0.5f));
            AddUntilStep("50 % shown", () => loader.Popup != null && hasText(loader.Popup, t => t == "50 %"));
            AddStep("downloaded", () => updateManager.Startup.ReadyToRestart(() => { }));
            AddUntilStep("restart handed to the updater", () => loader.RestartRequested);
            AddAssert("intro never started", () => loader.IsCurrentScreen() && !loader.ScreenLoaded);
        }

        [Test]
        public void TestHangingCheckDoesNotBlockTheGame()
        {
            AddStep("load", () => LoadScreen(loader = new TestLoader()));
            AddUntilStep("intro started after the timeout", () => loader.ScreenLoaded);
            AddAssert("no popup", () => loader.Popup == null);
        }

        private static bool hasText(Drawable root, Func<string, bool> predicate) =>
            root.ChildrenOfType<OsuSpriteText>().Any(t => predicate(t.Text.ToString()));

        private partial class TestUpdateManager : UpdateManager
        {
            public StartupUpdate Startup { get; set; } = new StartupUpdate();

            public override StartupUpdate StartupUpdate => Startup;
        }

        private partial class TestLoader : Loader
        {
            public StartupUpdatePopup? Popup => this.ChildrenOfType<StartupUpdatePopup>().FirstOrDefault();

            public bool RestartRequested { get; private set; }

            private TestScreen? screen;

            public bool ScreenLoaded => screen?.IsCurrentScreen() == true;

            protected override double StartupUpdateCheckTimeout => 2000;

            protected override OsuScreen CreateLoadableScreen() => screen = new TestScreen();

            protected override ShaderPrecompiler CreateShaderPrecompiler() => new InstantShaderPrecompiler();

            protected override void ExitToApplyUpdate(Action applyOnExit)
            {
                RestartRequested = true;
                applyOnExit?.Invoke();
            }

            private partial class InstantShaderPrecompiler : ShaderPrecompiler
            {
                protected override bool AllLoaded => true;
            }

            private partial class TestScreen : OsuScreen
            {
                public TestScreen()
                {
                    InternalChild = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.DarkSlateGray,
                        Alpha = 0,
                    };
                }

                protected override void LogoArriving(OsuLogo logo, bool resuming)
                {
                    base.LogoArriving(logo, resuming);
                    InternalChild.FadeInFromZero(200);
                }
            }
        }
    }
}

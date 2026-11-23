// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Updater;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// The "what's new" dialog in the running game, with a configuration left behind by an older version.
    /// </summary>
    public partial class TestSceneBanchosucksWhatsNew : OsuGameTestScene
    {
        private const string version_with_notes = "2026.913.17-banchosucks";

        private string runningVersion = version_with_notes;
        private string previousVersion = string.Empty;
        private string shownVersion = string.Empty;

        // every test starts its own game, with its own "previous" configuration
        public override void SetUpSteps()
        {
        }

        public override void RecycleLocalStorage(bool isDisposing)
        {
            base.RecycleLocalStorage(isDisposing);

            if (isDisposing)
                return;

            using (var config = new OsuConfigManager(LocalStorage))
                config.SetValue(OsuSetting.Version, previousVersion);
        }

        protected override TestOsuGame CreateTestGame() => new VersionedTestGame(LocalStorage, API, runningVersion, shownVersion);

        private void startGame(string running, string previous, string shown)
        {
            AddStep($"previous '{previous}', shown '{shown}', running '{running}'", () =>
            {
                runningVersion = running;
                previousVersion = previous;
                shownVersion = shown;
            });

            CreateNewGame();
            ConfirmAtMainMenu();
        }

        private BanchosucksWhatsNewOverlay? overlay => Game.ChildrenOfType<BanchosucksWhatsNewOverlay>().SingleOrDefault();

        [Test]
        public void TestShownOnceAfterUpdate()
        {
            startGame(version_with_notes, "2026.913.16-banchosucks", string.Empty);

            AddUntilStep("dialog visible", () => overlay?.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddAssert("for the running version", () => overlay!.Version, () => Is.EqualTo("2026.913.17"));
            AddAssert("remembered", () => Game.LocalConfig.Get<string>(OsuSetting.BanchosucksWhatsNewShownVersion), () => Is.EqualTo("2026.913.17"));

            AddStep("english", () => Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "en"));
            AddUntilStep("english notes", () => overlay!.ShowingGerman, () => Is.False);
            AddUntilStep("english text drawn", () => overlay.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("sorting")));
            AddAssert("no german text", () => overlay.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("Sortieren")), () => Is.False);
            AddAssert("header names the version", () => overlay.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("2026.913.17")));

            AddStep("german", () => Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "de"));
            AddUntilStep("german notes", () => overlay!.ShowingGerman);
            AddUntilStep("german text drawn", () => overlay.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("Sortieren")));
            AddAssert("no english text", () => overlay.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("sorting")), () => Is.False);
            AddStep("back to english", () => Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "en"));

            AddStep("click close", () =>
            {
                InputManager.MoveMouseTo(overlay.ChildrenOfType<RoundedButton>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("dialog closed", () => overlay!.State.Value, () => Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void TestEscapeCloses()
        {
            startGame(version_with_notes, "2026.913.16-banchosucks", string.Empty);

            AddUntilStep("dialog visible", () => overlay?.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("press escape", () => InputManager.Key(Key.Escape));
            AddUntilStep("dialog closed", () => overlay!.State.Value, () => Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void TestNothingOnFreshInstall()
        {
            startGame(version_with_notes, string.Empty, string.Empty);

            AddWaitStep("wait", 10);
            AddAssert("no dialog", () => overlay, () => Is.Null);
            AddAssert("marked as seen", () => Game.LocalConfig.Get<string>(OsuSetting.BanchosucksWhatsNewShownVersion), () => Is.EqualTo("2026.913.17"));
        }

        [Test]
        public void TestNothingWhenAlreadyShown()
        {
            startGame(version_with_notes, version_with_notes, "2026.913.17");

            AddWaitStep("wait", 10);
            AddAssert("no dialog", () => overlay, () => Is.Null);
        }

        [Test]
        public void TestNothingWithoutNotesFile()
        {
            startGame("2031.101.5-banchosucks", "2026.913.17-banchosucks", "2026.913.17");

            AddWaitStep("wait", 10);
            AddAssert("no dialog", () => overlay, () => Is.Null);
            AddAssert("version dealt with", () => Game.LocalConfig.Get<string>(OsuSetting.BanchosucksWhatsNewShownVersion), () => Is.EqualTo("2031.101.5"));
        }

        private partial class VersionedTestGame : TestOsuGame
        {
            private readonly string version;
            private readonly string shownVersion;

            public override string Version => version ?? "test game";

            public VersionedTestGame(Storage storage, IAPIProvider api, string version, string shownVersion)
                : base(storage, api)
            {
                this.version = version;
                this.shownVersion = shownVersion;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // the test game silences the dialog; put back what the "previous version" left in the configuration
                LocalConfig.SetValue(OsuSetting.BanchosucksWhatsNewShownVersion, shownVersion);
            }
        }
    }
}

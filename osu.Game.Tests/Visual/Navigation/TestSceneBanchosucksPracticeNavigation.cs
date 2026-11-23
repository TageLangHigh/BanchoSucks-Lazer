// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Practice;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// Banchosucks: how the player gets into the practice loop in the real game, from song select and from the fail overlay.
    /// </summary>
    public partial class TestSceneBanchosucksPracticeNavigation : OsuGameTestScene
    {
        private SoloSongSelect songSelect = null!;

        private BanchosucksPracticePlayer? practicePlayer => Game.ScreenStack.CurrentScreen as BanchosucksPracticePlayer;

        private void enterSongSelectWithBeatmap()
        {
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            // the easiest osu! difficulty of the set: without input the player survives longest on it
            AddStep("select Normal", () => Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(
                Game.Beatmap.Value.BeatmapSetInfo.Beatmaps.Single(b => b.Ruleset.OnlineID == 0 && b.DifficultyName == "Normal")));
            AddStep("no mods", () => Game.SelectedMods.Value = Array.Empty<Mod>());
        }

        private void waitForPractice()
            => AddUntilStep("practice player running", () =>
            {
                DismissAnyNotifications();
                return practicePlayer?.IsLoaded == true && practicePlayer.ChildrenOfType<GameplayClockContainer>().Single().IsRunning;
            });

        private void quitPracticeThroughPauseMenu()
        {
            AddUntilStep("pause", () => practicePlayer!.Pause() || practicePlayer!.ChildrenOfType<PauseOverlay>().Single().State.Value == Visibility.Visible);
            AddUntilStep("pause overlay shown", () => practicePlayer!.ChildrenOfType<PauseOverlay>().Single().State.Value == Visibility.Visible);
            AddStep("quit", () => practicePlayer!.ChildrenOfType<PauseOverlay>().Single().Buttons.Last().TriggerClick());
            AddUntilStep("back at the setup", () => Game.ScreenStack.CurrentScreen is BanchosucksPracticeSetupScreen);
        }

        [Test]
        public void TestPractiseSectionFromSongSelect()
        {
            BanchosucksPracticeSetupScreen setup = null!;

            enterSongSelectWithBeatmap();

            AddStep("choose \"Practise section\" in the beatmap menu", () =>
                songSelect.GetForwardActions(Game.Beatmap.Value.BeatmapInfo).Single(item => item.Text.Value.ToString() == "Practise section").Action.Value!.Invoke());

            AddUntilStep("setup open", () => (setup = (Game.ScreenStack.CurrentScreen as BanchosucksPracticeSetupScreen)!)?.IsLoaded == true);
            AddAssert("opened on top of song select", () => setup.GetParentScreen() == songSelect);
            AddAssert("covers the map", () => setup.CanStart && setup.StartTime.Value == setup.MapStart && setup.EndTime.Value == setup.MapEnd);

            AddStep("pick section", () =>
            {
                setup.EndTime.Value = 7000;
                setup.StartTime.Value = 4000;
                setup.Rate.Value = 0.75;
            });
            AddStep("click start", () =>
            {
                InputManager.MoveMouseTo(setup.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.RoundedButton>().Single());
                InputManager.Click(MouseButton.Left);
            });

            waitForPractice();
            AddAssert("section and speed applied", () => practicePlayer!.JumpTime == 4000 && practicePlayer.Section.EndTime == 7000 && practicePlayer.Section.Rate == 0.75);

            quitPracticeThroughPauseMenu();

            AddStep("leave setup", () => setup.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen == songSelect);
            AddAssert("no score saved", () => Game.Realm.Run(r => r.All<ScoreInfo>().Count()), () => Is.Zero);
        }

        [Test]
        public void TestRetryFromTheMissOnFailOverlay()
        {
            Player failedPlayer = null!;
            BanchosucksPracticePlayer practice = null!;
            double failTime = 0;

            enterSongSelectWithBeatmap();

            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                failedPlayer = (Game.ScreenStack.CurrentScreen as SoloPlayer)!;
                return failedPlayer?.IsLoaded == true;
            });

            // nobody presses a key in this test, so the health is topped up until the point the play is meant to fail at
            AddStep("keep alive for 7.5 s", () =>
            {
                var health = failedPlayer.ChildrenOfType<HealthProcessor>().Single();
                var clock = failedPlayer.ChildrenOfType<GameplayClockContainer>().Single();

                failedPlayer.OnUpdate += _ =>
                {
                    if (clock.CurrentTime < 7500 && !health.HasFailed)
                        health.Health.Value = 1;
                };
            });
            AddUntilStep("played 7.5 s", () => failedPlayer.ChildrenOfType<GameplayClockContainer>().Single().CurrentTime >= 7500);
            AddAssert("still alive", () => !failedPlayer.GameplayState.HasFailed);
            AddStep("fail", () => failedPlayer.ChildrenOfType<HealthProcessor>().Single().TriggerFailure());

            AddUntilStep("fail overlay shown", () => failedPlayer.ChildrenOfType<FailOverlay>().Single().State.Value == Visibility.Visible);
            AddStep("read fail time", () => failTime = failedPlayer.BanchosucksFailTime!.Value);
            AddAssert("failed late enough to test the five seconds", () => failTime, () => Is.GreaterThan(6500));

            AddAssert("button sits between Retry and Quit", () =>
            {
                var buttons = failedPlayer.ChildrenOfType<FailOverlay>().Single().Buttons;
                return buttons.Count == 3 && buttons[1].Text.ToString() == "Retry from the miss";
            });
            AddStep("click \"Retry from the miss\"", () => failedPlayer.ChildrenOfType<FailOverlay>().Single().Buttons[1].TriggerClick());

            waitForPractice();
            AddStep("remember practice player", () => practice = practicePlayer!);

            AddAssert("starts five seconds before the fail", () => practice.JumpTime, () => Is.EqualTo(failTime - 5000).Within(50));
            AddAssert("runs to the end of the map at normal speed", () =>
                practice.Section.Rate == 1 && practice.Section.RunsToMapEnd(Game.Beatmap.Value.GetPlayableBeatmap(Game.Ruleset.Value).HitObjects.Max(h => h.GetEndTime())));
            AddAssert("same difficulty", () => Game.Beatmap.Value.BeatmapInfo.DifficultyName, () => Is.EqualTo("Normal"));
            AddAssert("failed play was left", () => !failedPlayer.IsCurrentScreen() && failedPlayer.GetChildScreen() == null);
            AddAssert("setup sits on song select", () => practice.GetParentScreen()?.GetParentScreen() is BanchosucksPracticeSetupScreen setup && setup.GetParentScreen() == songSelect);

            quitPracticeThroughPauseMenu();
            AddAssert("setup shows the section", () =>
            {
                var setup = (BanchosucksPracticeSetupScreen)Game.ScreenStack.CurrentScreen;
                return Math.Abs(setup.StartTime.Value - (failTime - 5000)) <= 50 && setup.EndTime.Value == setup.MapEnd && setup.Rate.Value == 1;
            });

            AddStep("leave setup", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen == songSelect);
            AddAssert("no score saved", () => Game.Realm.Run(r => r.All<ScoreInfo>().Count()), () => Is.Zero);
        }
    }
}

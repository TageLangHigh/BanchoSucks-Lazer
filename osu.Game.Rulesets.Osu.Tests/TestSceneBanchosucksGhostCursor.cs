// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Banchosucks: the ghost of the replay comparison inside a real replay player. The watched replay never
    /// presses a key, the ghost replay hits every circle perfectly: if the ghost had any effect on the play, the
    /// result would not be all misses.
    /// </summary>
    public partial class TestSceneBanchosucksGhostCursor : ScreenTestScene
    {
        private static readonly Vector2[] circles =
        {
            new Vector2(100, 100),
            new Vector2(400, 100),
            new Vector2(400, 300),
            new Vector2(100, 300),
        };

        private static readonly Vector2 watched_rest = new Vector2(256, 192);

        private ReplayPlayer player = null!;

        private Score createGhost()
        {
            var frames = new List<ReplayFrame> { new OsuReplayFrame(0, new Vector2(20, 20)) };

            for (int i = 0; i < circles.Length; i++)
            {
                double time = 1000 + i * 1000;

                // on the circle exactly in time, held for a while so the test can catch it there
                frames.Add(new OsuReplayFrame(time, circles[i], OsuAction.LeftButton));
                frames.Add(new OsuReplayFrame(time + 400, circles[i]));
            }

            frames.Add(new OsuReplayFrame(6000, circles[^1]));

            return new Score
            {
                ScoreInfo = { User = new APIUser { Id = 55, Username = "ghost player" } },
                Replay = new Replay { Frames = frames },
            };
        }

        private void play(Score? ghost)
        {
            AddStep("play replay", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new Beatmap<OsuHitObject>
                {
                    HitObjects = circles.Select((position, i) => (OsuHitObject)new HitCircle { StartTime = 1000 + i * 1000, Position = position }).ToList(),
                    BeatmapInfo =
                    {
                        Ruleset = new OsuRuleset().RulesetInfo
                    },
                });

                SelectedMods.Value = new Mod[] { new OsuModNoFail() };

                // the watched player keeps the cursor in the middle and never clicks
                var watched = new Score
                {
                    Replay = new Replay
                    {
                        Frames = new List<ReplayFrame>
                        {
                            new OsuReplayFrame(0, watched_rest),
                            new OsuReplayFrame(6000, watched_rest),
                        }
                    }
                };

                LoadScreen(player = new ReplayPlayer(watched, new PlayerConfiguration { ShowResults = false }) { BanchosucksGhostScore = ghost });
            });

            AddUntilStep("player loaded", () => player.IsCurrentScreen() && player.IsLoaded && player.ChildrenOfType<DrawableOsuRuleset>().Any());
        }

        private BanchosucksGhostCursorOverlay overlay => player.ChildrenOfType<BanchosucksGhostCursorOverlay>().Single();

        private double gameplayTime => player.ChildrenOfType<DrawableOsuRuleset>().Single().FrameStableClock.CurrentTime;

        [Test]
        public void TestGhostFollowsItsReplayAndDoesNotPlay()
        {
            play(createGhost());

            AddAssert("ghost overlay there", () => player.ChildrenOfType<BanchosucksGhostCursorOverlay>().Count(), () => Is.EqualTo(1));
            AddAssert("it knows its frames", () => overlay.Track.FrameCount, () => Is.EqualTo(10));

            AddUntilStep("gameplay running", () => gameplayTime > 300);
            AddAssert("ghost is where its replay says", () =>
            {
                // the overlay updated with the frame-stable clock a moment ago; allow for the frame in between
                Vector2 expected = overlay.Track.PositionAt(gameplayTime);
                return (overlay.CursorPosition - expected).Length < 40;
            });

            AddUntilStep("ghost reaches the first circle and presses", () => overlay.ShownPressed && (overlay.CursorPosition - circles[0]).Length < 5);
            AddAssert("watched cursor is somewhere else", () => (player.ChildrenOfType<DrawableOsuRuleset>().Single().Playfield.Cursor!.ActiveCursor.Position - circles[0]).Length > 50);

            AddUntilStep("replay played", () => player.GameplayState.HasPassed);

            AddAssert("every circle was judged", () => player.ChildrenOfType<ScoreProcessor>().Single().HitEvents.Count, () => Is.EqualTo(circles.Length));
            AddAssert("all misses: the ghost's clicks hit nothing", () => player.ChildrenOfType<ScoreProcessor>().Single().HitEvents.All(e => e.Result == HitResult.Miss));
            AddAssert("score is zero", () => player.GameplayState.ScoreProcessor.TotalScore.Value, () => Is.EqualTo(0));
            AddAssert("combo is zero", () => player.GameplayState.ScoreProcessor.HighestCombo.Value, () => Is.EqualTo(0));
            AddAssert("the score shown is the watched one", () => player.Score.ScoreInfo.User.Username, () => Is.Not.EqualTo("ghost player"));
        }

        [Test]
        public void TestNoGhostWithoutComparison()
        {
            play(null);

            AddAssert("no ghost overlay", () => player.ChildrenOfType<BanchosucksGhostCursorOverlay>(), () => Is.Empty);
        }

        [Test]
        public void TestGhostWithoutFramesShowsNothing()
        {
            play(new Score { Replay = new Replay() });

            AddUntilStep("gameplay running", () => gameplayTime > 300);
            AddAssert("overlay invisible", () => overlay.Alpha, () => Is.EqualTo(0));
        }
    }
}

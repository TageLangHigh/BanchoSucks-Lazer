// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// Banchosucks: "render as video" outside of song select (results screen button, profile score menu) and the render
    /// queue. danser-go itself never runs here: the renderer's work is replaced by tasks the test completes by hand.
    /// </summary>
    public partial class TestSceneBanchosucksRenderVideo : OsuTestScene
    {
        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private NotificationOverlay notificationOverlay = null!;
        private BanchosucksReplayRenderer renderer = null!;
        private RulesetStore rulesets = null!;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> work = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        private static bool platformCanRender => BanchosucksReplayRenderer.CanRenderRuleset(new OsuRuleset().RulesetInfo);

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            this.rulesets = rulesets;

            base.Content.AddRange(new Drawable[]
            {
                Content,
                notificationOverlay = new NotificationOverlay
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
            });

            Dependencies.CacheAs<INotificationOverlay>(notificationOverlay);

            renderer = new BanchosucksReplayRenderer
            {
                TestWork = (title, cancellation) =>
                {
                    var source = work.GetOrAdd(title, _ => new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
                    cancellation.Register(() => source.TrySetCanceled());
                    return source.Task;
                }
            };

            base.Content.Add(renderer);
            Dependencies.Cache(renderer);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("finish leftover renders", () =>
            {
                foreach (var source in work.Values)
                    source.TrySetCanceled();
            });
            AddUntilStep("renderer idle", () => !renderer.IsRendering && renderer.WaitingCount == 0);
            AddStep("clear", () =>
            {
                work.Clear();
                notificationOverlay.ChildrenOfType<Notification>().ToArray().ForEach(n => n.Close(false));
                Clear();
            });
        }

        [Test]
        public void TestResultsButtonOnlyForRenderableScores()
        {
            BanchosucksRenderVideoButton osuButton = null!;
            BanchosucksRenderVideoButton maniaButton = null!;
            BanchosucksRenderVideoButton noReplayButton = null!;

            AddStep("add buttons", () =>
            {
                var osuScore = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
                osuScore.OnlineID = 1001;
                osuScore.HasOnlineReplay = true;

                var maniaScore = TestResources.CreateTestScoreInfo(new ManiaRuleset().RulesetInfo);
                maniaScore.OnlineID = 1002;
                maniaScore.HasOnlineReplay = true;

                var noReplayScore = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
                noReplayScore.OnlineID = 1003;
                noReplayScore.HasOnlineReplay = false;

                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        osuButton = new BanchosucksRenderVideoButton { Score = { Value = osuScore } },
                        maniaButton = new BanchosucksRenderVideoButton { Score = { Value = maniaScore } },
                        noReplayButton = new BanchosucksRenderVideoButton { Score = { Value = noReplayScore } },
                    }
                };
            });

            AddUntilStep("buttons loaded", () => osuButton.IsLoaded && maniaButton.IsLoaded && noReplayButton.IsLoaded);
            AddAssert("osu! score with a replay can be rendered", () => osuButton.Available, () => Is.EqualTo(platformCanRender));
            AddAssert("other rulesets are hidden", () => maniaButton.Available, () => Is.False);
            AddAssert("no replay, no button", () => noReplayButton.Available, () => Is.False);
            AddUntilStep("hidden buttons take no room", () => !maniaButton.IsPresent && !noReplayButton.IsPresent);
            AddStep("score goes away", () => osuButton.Score.Value = null);
            AddAssert("button gone", () => osuButton.Available, () => Is.False);
        }

        [Test]
        public void TestProfileScoreMenu()
        {
            DrawableProfileScore osuScore = null!;
            DrawableProfileScore maniaScore = null!;
            DrawableProfileScore noReplayScore = null!;

            AddStep("add profile scores", () => Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Width = 0.8f,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        osuScore = new DrawableProfileScore(createProfileScore(0, 2001, true)),
                        maniaScore = new DrawableProfileScore(createProfileScore(3, 2002, true)),
                        noReplayScore = new DrawableProfileScore(createProfileScore(0, 2003, false)),
                    }
                }
            });

            AddUntilStep("loaded", () => osuScore.IsLoaded && maniaScore.IsLoaded && noReplayScore.IsLoaded);
            AddAssert("osu! score offers the render", () => osuScore.ContextMenuItems.Length, () => Is.EqualTo(platformCanRender ? 1 : 0));
            AddAssert("mania score does not", () => maniaScore.ContextMenuItems, () => Is.Empty);
            AddAssert("score without replay does not", () => noReplayScore.ContextMenuItems, () => Is.Empty);
        }

        [Test]
        public void TestProfileScoreBecomesRenderableScore()
        {
            ScoreInfo info = null!;

            AddStep("convert with checksum", () => info = BanchosucksProfileScoreActions.CreateScoreInfo(
                createProfileScore(0, 2001, true, checksum: "abc"), rulesets, _ => "local", new APIUser { Id = 7, Username = "profile owner" }));
            AddAssert("server checksum wins", () => info.BeatmapInfo!.MD5Hash, () => Is.EqualTo("abc"));
            AddAssert("online replay known", () => info.HasOnlineReplay && info.OnlineID == 2001);
            AddAssert("osu! ruleset", () => info.Ruleset.ShortName, () => Is.EqualTo("osu"));
            AddAssert("player from the profile", () => info.User.Username, () => Is.EqualTo("profile owner"));
            AddAssert("renderable", () => BanchosucksReplayRenderer.CanRender(info), () => Is.EqualTo(platformCanRender));

            AddStep("convert without checksum", () => info = BanchosucksProfileScoreActions.CreateScoreInfo(
                createProfileScore(0, 2001, true, username: "from score"), rulesets, id => id == 55 ? "local" : null, new APIUser { Id = 7, Username = "profile owner" }));
            AddAssert("local copy's hash used", () => info.BeatmapInfo!.MD5Hash, () => Is.EqualTo("local"));
            AddAssert("player from the score", () => info.User.Username, () => Is.EqualTo("from score"));
        }

        [Test]
        public void TestQueue()
        {
            AddStep("first request", () => renderer.TestRequest("first"));
            AddUntilStep("renders right away", () => renderer.IsRendering && work.ContainsKey("first"));
            AddAssert("nothing waiting", () => renderer.WaitingCount, () => Is.EqualTo(0));

            AddStep("two more requests", () =>
            {
                renderer.TestRequest("second");
                renderer.TestRequest("third");
            });
            AddAssert("both wait", () => renderer.WaitingCount, () => Is.EqualTo(2));
            AddUntilStep("three notifications", () => progressNotifications.Count(), () => Is.EqualTo(3));
            AddAssert("waiting ones are queued", () => notificationFor("second").State == ProgressNotificationState.Queued && notificationFor("third").State == ProgressNotificationState.Queued);
            AddAssert("places are told", () => notificationFor("second").Text.ToString().Contains("place 1") && notificationFor("third").Text.ToString().Contains("place 2"));
            AddAssert("not started yet", () => !work.ContainsKey("second") && !work.ContainsKey("third"));

            AddStep("cancel the second from its notification", () => notificationFor("second").Close(false));
            AddUntilStep("one waiting", () => renderer.WaitingCount, () => Is.EqualTo(1));
            AddUntilStep("third moved up", () => notificationFor("third").Text.ToString(), () => Does.Contain("place 1"));
            AddAssert("first still rendering", () => renderer.IsRendering && !work["first"].Task.IsCompleted);

            AddStep("first finishes", () => work["first"].SetResult("first.mp4"));
            AddUntilStep("third starts", () => work.ContainsKey("third"));
            AddAssert("second never started", () => !work.ContainsKey("second"));
            AddAssert("queue empty", () => renderer.WaitingCount, () => Is.EqualTo(0));
            AddUntilStep("third is active", () => notificationFor("third").State, () => Is.EqualTo(ProgressNotificationState.Active));

            AddStep("third finishes", () => work["third"].SetResult("third.mp4"));
            AddUntilStep("idle", () => !renderer.IsRendering);
        }

        [Test]
        public void TestQueueLimit()
        {
            AddStep("one running, five waiting", () =>
            {
                for (int i = 0; i < 6; i++)
                    renderer.TestRequest($"job {i}");
            });
            AddUntilStep("running", () => work.ContainsKey("job 0"));
            AddAssert("five wait", () => renderer.WaitingCount, () => Is.EqualTo(5));

            AddStep("one more", () => renderer.TestRequest("too many"));
            AddAssert("refused", () => renderer.WaitingCount, () => Is.EqualTo(5));
            AddUntilStep("the player is told", () => notificationOverlay.ChildrenOfType<SimpleNotification>().Any(n => n.Text.ToString().Contains("queue is full")));
            AddAssert("no progress notification for it", () => progressNotifications.All(n => !n.Text.ToString().Contains("too many")));

            AddStep("cancel the running one", () => notificationFor("job 0").Close(false));
            AddUntilStep("next takes over", () => work.ContainsKey("job 1"));
            AddAssert("four wait", () => renderer.WaitingCount, () => Is.EqualTo(4));

            AddStep("finish all", () => Scheduler.AddDelayed(finishAll, 50, true));
            AddUntilStep("idle", () => !renderer.IsRendering && renderer.WaitingCount == 0);
            AddStep("stop finishing", () => Scheduler.CancelDelayedTasks());
        }

        private void finishAll()
        {
            foreach (var (title, source) in work)
                source.TrySetResult($"{title}.mp4");
        }

        private IEnumerable<ProgressNotification> progressNotifications => notificationOverlay.ChildrenOfType<ProgressNotification>();

        private ProgressNotification notificationFor(string title) => progressNotifications.Single(n => n.Text.ToString().EndsWith(title, StringComparison.Ordinal));

        private static SoloScoreInfo createProfileScore(int rulesetId, long onlineId, bool hasReplay, string? checksum = null, string? username = null) => new SoloScoreInfo
        {
            ID = (ulong)onlineId,
            RulesetID = rulesetId,
            BeatmapID = 55,
            HasReplay = hasReplay,
            PP = 100,
            Rank = ScoreRank.A,
            Accuracy = 0.97,
            Ranked = true,
            Preserve = true,
            Processed = true,
            EndedAt = DateTimeOffset.Now,
            User = username == null ? null : new APIUser { Id = 3, Username = username },
            Mods = new[] { new APIMod { Acronym = rulesetId == 0 ? new OsuModHidden().Acronym : "NF" } },
            Beatmap = new APIBeatmap
            {
                OnlineID = 55,
                Checksum = checksum ?? string.Empty,
                DifficultyName = "Insane",
                Status = BeatmapOnlineStatus.Ranked,
                BeatmapSet = new APIBeatmapSet { Title = "title", Artist = "artist" },
            },
        };
    }
}

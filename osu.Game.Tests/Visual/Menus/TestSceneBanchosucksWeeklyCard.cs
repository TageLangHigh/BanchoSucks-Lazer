// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Banchosucks;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// The weekly tile of the main menu with fake plugins, a real database and a downloader whose downloads never finish.
    /// </summary>
    public partial class TestSceneBanchosucksWeeklyCard : OsuTestScene
    {
        private static readonly DateTimeOffset now = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

        private BanchosucksWeeklyCard card = null!;

        private BanchosucksMapOfTheWeek? map;
        private BanchosucksCommunityGoal? goal;

        private int mapRequests;
        private int goalRequests;
        private int lookups;
        private bool lookupFails;
        private readonly List<int> downloads = new List<int>();
        private readonly List<ArchiveDownloadRequest<IBeatmapSetInfo>> running = new List<ArchiveDownloadRequest<IBeatmapSetInfo>>();

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            BeatmapManager beatmaps;

            Dependencies.Cache(Realm);
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, API, Audio, Resources, host, Beatmap.Default));
            Dependencies.CacheAs<BeatmapModelDownloader>(new TestBeatmapModelDownloader(beatmaps, API));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            // the texts follow a static language switch which a game test in the same run may have set to German
            AddStep("english", () => ClickerStrings.IsGerman = false);

            AddStep("online, empty library", () =>
            {
                // downloads of the last test never finish on their own
                foreach (var download in running)
                    download.Fail(new OperationCanceledException());

                running.Clear();

                dummyAPI.SetState(APIState.Online);
                Realm.Write(r => r.RemoveAll<BeatmapSetInfo>());
            });

            AddStep("fake plugins", () =>
            {
                mapRequests = goalRequests = lookups = 0;
                lookupFails = false;
                downloads.Clear();

                map = new BanchosucksMapOfTheWeek
                {
                    BeatmapId = 129891,
                    BeatmapSetId = 39804,
                    RoomId = 412,
                    Title = "xi - FREEDOM DiVE [FOUR DIMENSIONS]",
                    Stars = 7.81,
                    NominatedBy = "Lidl",
                    Votes = 4,
                    EndsAt = "2026-11-29T17:55:00+00:00",
                };

                goal = new BanchosucksCommunityGoal
                {
                    Target = 2000,
                    Total = 1234,
                    Stable = 900,
                    Lazer = 334,
                    Participants = 45,
                    EndsAt = "2026-11-30T00:00:00+00:00",
                };

                dummyAPI.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetBanchosucksMapOfTheWeekRequest mapRequest:
                            mapRequests++;
                            mapRequest.TriggerSuccess(new BanchosucksMapOfTheWeekResponse { Current = map });
                            return true;

                        case GetBanchosucksCommunityGoalRequest goalRequest:
                            goalRequests++;
                            goalRequest.TriggerSuccess(new BanchosucksCommunityGoalResponse { Goal = goal });
                            return true;

                        case GetBeatmapSetRequest lookup:
                            lookups++;

                            if (lookupFails)
                                return false;

                            lookup.TriggerSuccess(new APIBeatmapSet { OnlineID = lookup.ID, Title = "FREEDOM DiVE", Artist = "xi" });
                            return true;

                        case ArchiveDownloadRequest<IBeatmapSetInfo> download:
                            downloads.Add(download.Model.OnlineID);
                            running.Add(download);
                            return true;

                        default:
                            return false;
                    }
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("reset api", () =>
            {
                dummyAPI.HandleRequest = null;
                dummyAPI.SetState(APIState.Online);
            });
        }

        private void create()
        {
            AddStep("create tile", () => Child = card = new BanchosucksWeeklyCard
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.TopLeft,
                X = -200,
                Y = -150,
                Now = () => now,
            });
        }

        private void clickHeader() => AddStep("click the line", () => card.ChildrenOfType<OsuClickableContainer>().First().TriggerClick());

        private void clickMapButton() => AddStep("click the map button", () => card.ChildrenOfType<RoundedButton>().Single().TriggerClick());

        [Test]
        public void TestSummaryAndPanel()
        {
            create();

            AddUntilStep("tile shown", () => card.Shown);
            AddAssert("summary", () => card.HeaderText, () => Is.EqualTo("Map of the week: xi - FREEDOM DiVE [FOUR DIMENSIONS] · community goal 61 %"));
            AddAssert("panel closed", () => card.Expanded, () => Is.False);

            clickHeader();
            AddAssert("panel open", () => card.Expanded, () => Is.True);
            AddAssert("goal numbers", () => card.GoalText, () => Is.EqualTo("1,234 / 2,000 plays (61 %)"));
            AddAssert("goal bar", () => card.GoalFraction, () => Is.EqualTo(0.617).Within(0.001));
            AddAssert("map not in the library", () => card.MapButtonText, () => Is.EqualTo("Download"));

            // opening the panel right after the first request does not ask again
            AddWaitStep("wait", 10);
            AddAssert("one request each", () => (mapRequests, goalRequests), () => Is.EqualTo((1, 1)));

            clickHeader();
            AddAssert("panel closed again", () => card.Expanded, () => Is.False);
        }

        [Test]
        public void TestDownloadTheMap()
        {
            create();
            AddUntilStep("tile shown", () => card.Shown);
            clickHeader();

            clickMapButton();
            AddUntilStep("set looked up once", () => lookups, () => Is.EqualTo(1));
            AddUntilStep("download started through the normal path", () => downloads, () => Is.EqualTo(new[] { 39804 }));
            AddUntilStep("button shows the download", () => card.MapButtonText, () => Is.EqualTo("Downloading..."));

            clickMapButton();
            AddWaitStep("wait", 5);
            AddAssert("not started twice", () => downloads, () => Has.Count.EqualTo(1));
        }

        [Test]
        public void TestMapNotOnTheServer()
        {
            AddStep("lookup fails", () => lookupFails = true);
            create();
            AddUntilStep("tile shown", () => card.Shown);
            clickHeader();

            clickMapButton();
            AddUntilStep("looked up", () => lookups, () => Is.EqualTo(1));
            AddUntilStep("button back", () => card.MapButtonText, () => Is.EqualTo("Download"));
            AddAssert("nothing downloaded", () => downloads, () => Is.Empty);
        }

        [Test]
        public void TestMapInTheLibrary()
        {
            AddStep("set in the library", () => Realm.Write(r =>
            {
                r.Add(new BeatmapSetInfo { OnlineID = 39804 });
            }));

            create();
            AddUntilStep("tile shown", () => card.Shown);
            clickHeader();

            AddUntilStep("button offers song select", () => card.MapButtonText, () => Is.EqualTo("Show in song select"));
        }

        [Test]
        public void TestGoalReachedAndNoMap()
        {
            AddStep("no map, goal reached", () =>
            {
                map = null;
                goal = new BanchosucksCommunityGoal { Target = 300, Total = 450, ReachedAt = "2026-11-24T20:00:00+00:00", Participants = 12 };
            });

            create();
            AddUntilStep("tile shown", () => card.Shown);
            AddAssert("summary", () => card.HeaderText, () => Is.EqualTo("Community goal reached"));

            clickHeader();
            AddAssert("goal numbers", () => card.GoalText, () => Is.EqualTo("450 / 300 plays (100 %)"));
            AddAssert("bar full", () => card.GoalFraction, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNothingThisWeek()
        {
            AddStep("no map, no goal", () =>
            {
                map = null;
                goal = null;
            });

            create();
            AddUntilStep("asked", () => (mapRequests, goalRequests), () => Is.EqualTo((1, 1)));
            AddWaitStep("wait", 5);
            AddAssert("tile hidden", () => card.Shown, () => Is.False);
        }

        [Test]
        public void TestEndedMapIsNotShown()
        {
            AddStep("map ended yesterday", () => map!.EndsAt = "2026-11-24T12:00:00+00:00");

            create();
            AddUntilStep("tile shown for the goal", () => card.Shown);
            AddAssert("summary without the map", () => card.HeaderText, () => Is.EqualTo("Community goal 61 %"));
        }

        [Test]
        public void TestNoRequestsWhileLoggedOut()
        {
            AddStep("logged out", () => dummyAPI.SetState(APIState.Offline));
            create();

            AddWaitStep("wait", 10);
            AddAssert("nothing requested", () => mapRequests + goalRequests, () => Is.Zero);
            AddAssert("tile hidden", () => card.Shown, () => Is.False);

            AddStep("log in", () => dummyAPI.SetState(APIState.Online));
            AddUntilStep("tile shown", () => card.Shown);
            AddAssert("one request each", () => (mapRequests, goalRequests), () => Is.EqualTo((1, 1)));
        }

        private class TestBeatmapModelDownloader : BeatmapModelDownloader
        {
            public TestBeatmapModelDownloader(IModelImporter<BeatmapSetInfo> importer, IAPIProvider api)
                : base(importer, api)
            {
            }

            protected override ArchiveDownloadRequest<IBeatmapSetInfo> CreateDownloadRequest(IBeatmapSetInfo set, bool minimiseDownloadSize) => new TestDownloadRequest(set);
        }

        private class TestDownloadRequest : ArchiveDownloadRequest<IBeatmapSetInfo>
        {
            public TestDownloadRequest(IBeatmapSetInfo model)
                : base(model)
            {
            }

            protected override string Target => string.Empty;
        }
    }
}

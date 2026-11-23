// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// "Download missing maps" and "top plays as collection" of the profile overlay, with a fake api, a real database and a
    /// downloader whose downloads never finish on their own.
    /// </summary>
    public partial class TestSceneBanchosucksTopPlaysActions : OsuTestScene
    {
        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private DialogOverlay dialogOverlay = null!;
        private NotificationOverlay notificationOverlay = null!;
        private BanchosucksBeatmapDownloadQueue queue = null!;

        private readonly Bindable<UserProfileData?> user = new Bindable<UserProfileData?>();
        private readonly List<SoloScoreInfo> loaded = new List<SoloScoreInfo>();
        private readonly List<ArchiveDownloadRequest<IBeatmapSetInfo>> downloads = new List<ArchiveDownloadRequest<IBeatmapSetInfo>>();

        private BanchosucksTopPlaysActions actions = null!;
        private int scoreRequests;
        private int mostDownloadsAtOnce;

        // eight top plays on seven sets; set 10 is in the library
        private static readonly (int set, string md5)[] top_plays =
        {
            (10, "aa01"), (20, "aa02"), (20, "aa03"), (30, "aa04"), (40, "aa05"), (50, "aa06"), (60, "aa07"), (70, "aa08"),
        };

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            BeatmapManager beatmaps;

            Dependencies.Cache(Realm);
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, API, Audio, Resources, host, Beatmap.Default));
            Dependencies.CacheAs<BeatmapModelDownloader>(new TestBeatmapModelDownloader(beatmaps, API));

            base.Content.AddRange(new Drawable[]
            {
                queue = new BanchosucksBeatmapDownloadQueue(),
                Content,
                dialogOverlay = new DialogOverlay(),
                notificationOverlay = new NotificationOverlay
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
            });

            Dependencies.Cache(queue);
            Dependencies.CacheAs<IDialogOverlay>(dialogOverlay);
            Dependencies.CacheAs<INotificationOverlay>(notificationOverlay);
        }

        private static SoloScoreInfo score(int index)
        {
            var (set, md5) = top_plays[index];

            return new SoloScoreInfo
            {
                ID = (ulong)(1000 + index),
                Beatmap = new APIBeatmap
                {
                    OnlineID = 500 + index,
                    OnlineBeatmapSetID = set,
                    Checksum = md5,
                    BeatmapSet = new APIBeatmapSet { OnlineID = set, Title = $"Title {set}", Artist = "Artist" },
                },
            };
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            // the collection name follows a static language switch which a game test in the same run may have set to German
            AddStep("english", () => ClickerStrings.IsGerman = false);
            AddStep("close dialogs", () => dialogOverlay.CurrentDialog?.Hide());
            AddStep("clear notifications", () => notificationOverlay.ChildrenOfType<Notification>().ToArray().ForEach(n => n.Close(false)));

            // stays in place between tests: a download the queue starts must always reach the handler, or it never ends
            AddStep("fake api", () => ((DummyAPIAccess)API).HandleRequest = request =>
            {
                switch (request)
                {
                    case GetUserScoresRequest scores:
                        scoreRequests++;
                        scores.TriggerSuccess(Enumerable.Range(0, top_plays.Length).Select(score).ToList());
                        return true;

                    case ArchiveDownloadRequest<IBeatmapSetInfo> download:
                        downloads.Add(download);
                        mostDownloadsAtOnce = Math.Max(mostDownloadsAtOnce, downloads.Count);
                        return true;

                    default:
                        return false;
                }
            });

            // whatever the last test left running or waiting is ended, including what starts while doing so
            AddUntilStep("queue idle", () =>
            {
                foreach (var download in downloads.ToArray())
                    download.Fail(new OperationCanceledException());

                downloads.Clear();
                return queue.Slots.Active == 0 && queue.Slots.Waiting == 0;
            });

            AddStep("library with set 10", () => Realm.Write(r =>
            {
                r.RemoveAll<BeatmapCollection>();
                r.RemoveAll<BeatmapSetInfo>();
                r.Add(new BeatmapSetInfo { OnlineID = 10 });
            }));

            AddStep("reset counters", () =>
            {
                scoreRequests = 0;
                mostDownloadsAtOnce = 0;
            });

            AddStep("show the buttons with five plays loaded", () =>
            {
                loaded.Clear();
                loaded.AddRange(Enumerable.Range(0, 5).Select(score));

                user.Value = new UserProfileData(new APIUser { Id = 2, Username = "Lidl", ScoresBestCount = top_plays.Length }, new OsuRuleset().RulesetInfo);

                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 800,
                    AutoSizeAxes = Axes.Y,
                    Child = actions = new BanchosucksTopPlaysActions(user, () => loaded),
                };
            });
            AddUntilStep("loaded", () => actions.IsLoaded);
        }

        private void clickDownload() => AddStep("click download", () => actions.ChildrenOfType<RoundedButton>().First().TriggerClick());

        private void clickCollection() => AddStep("click collection", () => actions.ChildrenOfType<RoundedButton>().Last().TriggerClick());

        private string dialogText => dialogOverlay.CurrentDialog?.HeaderText.ToString() ?? string.Empty;

        private int[] downloadingSets => downloads.Select(d => d.Model.OnlineID).ToArray();

        private void finish(int set, bool success)
        {
            var download = downloads.Single(d => d.Model.OnlineID == set);

            downloads.Remove(download);
            // a download that ends without a file counts as failed in the game; that is all a test without a server can do
            download.Fail(success ? new OperationCanceledException() : new InvalidOperationException("no server"));
        }

        [Test]
        public void TestDownloadMissingMapsOfTheLoadedPlays()
        {
            clickDownload();

            // five plays loaded: sets 10 (local), 20 (twice), 30, 40
            AddUntilStep("one confirmation with the count", () => dialogText, () => Is.EqualTo("Download 3 missing beatmap sets from the 5 top plays of Lidl?"));
            AddAssert("nothing started yet", () => downloads, () => Is.Empty);
            AddAssert("no extra request for scores", () => scoreRequests, () => Is.Zero);

            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());

            AddUntilStep("two downloads running", () => downloadingSets, () => Is.EqualTo(new[] { 20, 30 }));
            AddWaitStep("wait", 5);
            AddAssert("the third waits", () => queue.Slots.Waiting, () => Is.EqualTo(1));
            AddAssert("still two", () => downloads, () => Has.Count.EqualTo(2));

            AddStep("first download ends", () => finish(20, false));
            AddUntilStep("the third starts", () => downloadingSets, () => Is.EqualTo(new[] { 30, 40 }));

            AddStep("the others end", () =>
            {
                finish(30, false);
                finish(40, false);
            });
            AddUntilStep("queue idle", () => queue.Slots.Active == 0 && queue.Slots.Waiting == 0);
            AddAssert("never more than two at once", () => mostDownloadsAtOnce, () => Is.EqualTo(2));
        }

        [Test]
        public void TestAllTopPlaysAreFetchedFirstWhenTicked()
        {
            AddStep("tick all 100", () => actions.IncludeAll.Value = true);
            clickDownload();

            AddUntilStep("confirmation counts all plays", () => dialogText, () => Is.EqualTo("Download 6 missing beatmap sets from the 8 top plays of Lidl?"));
            AddAssert("one request for the scores", () => scoreRequests, () => Is.EqualTo(1));

            AddStep("cancel", () => dialogOverlay.CurrentDialog!.PerformAction<Overlays.Dialog.PopupDialogCancelButton>());
            AddWaitStep("wait", 5);
            AddAssert("nothing downloaded", () => downloads, () => Is.Empty);
            AddAssert("nothing queued", () => queue.Slots.Waiting + queue.Slots.Active, () => Is.Zero);
        }

        [Test]
        public void TestSetsAlreadyDownloadingAreSkipped()
        {
            clickDownload();
            AddUntilStep("confirmation", () => dialogText, () => Does.StartWith("Download 3 missing"));
            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("two downloads running", () => downloads, () => Has.Count.EqualTo(2));

            // 20 and 30 are running, 40 waits: nothing is left to ask for
            clickDownload();
            AddUntilStep("told that nothing is to do", () => actions.LastMessage.ToString(), () => Does.StartWith("Nothing to download: 1 of 4 beatmap sets are in your library, 3 are downloading"));
            AddAssert("no new dialog", () => dialogOverlay.CurrentDialog?.State.Value, () => Is.Not.EqualTo(Visibility.Visible));
        }

        [Test]
        public void TestNothingToDownloadWhenEverythingIsLocal()
        {
            AddStep("all sets in the library", () => Realm.Write(r =>
            {
                foreach (int set in new[] { 20, 30, 40 })
                    r.Add(new BeatmapSetInfo { OnlineID = set });
            }));

            clickDownload();

            AddUntilStep("told so", () => actions.LastMessage.ToString(), () => Is.EqualTo("Nothing to download: all 4 beatmap sets of these 5 top plays are in your library."));
            AddAssert("no dialog", () => dialogOverlay.CurrentDialog?.State.Value, () => Is.Not.EqualTo(Visibility.Visible));
        }

        [Test]
        public void TestTopPlaysAsCollection()
        {
            clickCollection();

            AddUntilStep("collection created", () => hashes("Top plays of Lidl"), () => Is.EqualTo(new[] { "aa01", "aa02", "aa03", "aa04", "aa05" }));
            AddAssert("message", () => actions.LastMessage.ToString(), () => Is.EqualTo("Collection \"Top plays of Lidl\" created with 5 difficulties."));

            // the difficulties are stored by hash although only set 10 is in the library
            AddAssert("no downloads", () => downloads, () => Is.Empty);

            AddStep("tick all 100", () => actions.IncludeAll.Value = true);
            clickCollection();

            AddUntilStep("collection extended", () => hashes("Top plays of Lidl"), () => Has.Length.EqualTo(8).And.Unique);
            AddAssert("message", () => actions.LastMessage.ToString(), () => Is.EqualTo("Collection \"Top plays of Lidl\": 3 added, 8 in total."));
            AddAssert("still one collection", () => Realm.Run(r => r.All<BeatmapCollection>().Count()), () => Is.EqualTo(1));

            clickCollection();
            AddUntilStep("nothing added twice", () => actions.LastMessage.ToString(), () => Is.EqualTo("Collection \"Top plays of Lidl\": 0 added, 8 in total."));
        }

        [Test]
        public void TestCollectionMadeInGermanIsFoundAgain()
        {
            AddStep("german collection exists", () => Realm.Write(r => r.Add(new BeatmapCollection("Top-Plays von Lidl", new List<string> { "aa01", "ff99" }))));

            clickCollection();

            AddUntilStep("extended, not duplicated", () => hashes("Top-Plays von Lidl"), () => Is.EqualTo(new[] { "aa01", "ff99", "aa02", "aa03", "aa04", "aa05" }));
            AddAssert("one collection", () => Realm.Run(r => r.All<BeatmapCollection>().Count()), () => Is.EqualTo(1));
        }

        private string[] hashes(string collection)
            => Realm.Run(r => r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == collection)?.BeatmapMD5Hashes.ToArray()) ?? Array.Empty<string>();

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

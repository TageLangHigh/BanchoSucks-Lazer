// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Chat;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Banchosucks: beatmap preview cards under chat messages, against a server that only exists in the test.
    /// The sets have no cover urls, so nothing is loaded from anywhere.
    /// </summary>
    public partial class TestSceneBanchosucksChatBeatmapPreview : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private BanchosucksChatBeatmapLookup lookup = null!;
        private Channel channel = null!;
        private DrawableChannel drawableChannel = null!;

        private readonly List<GetBeatmapSetRequest> requests = new List<GetBeatmapSetRequest>();
        private string originalWebsiteRoot = null!;
        private long nextMessageId;

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        // once per fixture: the steps of a test run in the base class's tear-down, after any tear-down of this class.
        // this also runs when a test failed, and other chat tests rely on the website root.
        [OneTimeSetUp]
        public void RememberWebsiteRoot() => originalWebsiteRoot = MessageFormatter.WebsiteRootUrl;

        [OneTimeTearDown]
        public void RestoreWebsiteRoot() => MessageFormatter.WebsiteRootUrl = originalWebsiteRoot;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("website is banchosucks.cc/lazer", () => MessageFormatter.WebsiteRootUrl = "https://banchosucks.cc/lazer");

            AddStep("previews on", () => config.SetValue(OsuSetting.BanchosucksChatBeatmapPreviews, true));

            AddStep("set up server", () =>
            {
                requests.Clear();

                dummyAPI.HandleRequest = request =>
                {
                    if (request is not GetBeatmapSetRequest setRequest)
                        return false;

                    requests.Add(setRequest);

                    // set 404 and beatmap 4040 do not exist
                    if (setRequest.ID == 404 || setRequest.ID == 4040)
                    {
                        setRequest.Fail(new InvalidOperationException("not found"));
                        return true;
                    }

                    int setId = setRequest.Type == BeatmapSetLookupType.SetId ? setRequest.ID : setRequest.ID / 10;
                    setRequest.TriggerSuccess(createSet(setId));
                    return true;
                };
            });

            AddStep("create channel", () =>
            {
                channel = new Channel { Id = 1, Name = "#osu" };

                // a fresh lookup for every test, so nothing is answered from an earlier test's cache
                lookup = new BanchosucksChatBeatmapLookup();

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(BanchosucksChatBeatmapLookup), lookup) },
                    Children = new Drawable[]
                    {
                        lookup,
                        drawableChannel = new DrawableChannel(channel) { RelativeSizeAxes = Axes.Both },
                    }
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("restore setting", () => config.SetValue(OsuSetting.BanchosucksChatBeatmapPreviews, true));
            AddStep("remove server", () => dummyAPI.HandleRequest = null);
        }

        /// <summary>
        /// Set N has the difficulties N * 10 + 1 (2.5 stars) and N * 10 + 2 (5.75 stars).
        /// </summary>
        private static APIBeatmapSet createSet(int id) => new APIBeatmapSet
        {
            OnlineID = id,
            Title = $"Title {id}",
            Artist = $"Artist {id}",
            Status = BeatmapOnlineStatus.Ranked,
            Author = new APIUser { Id = 9, Username = "mapper" },
            Beatmaps = new[]
            {
                new APIBeatmap { OnlineID = id * 10 + 1, OnlineBeatmapSetID = id, DifficultyName = "Normal", StarRating = 2.5 },
                new APIBeatmap { OnlineID = id * 10 + 2, OnlineBeatmapSetID = id, DifficultyName = "Insane", StarRating = 5.75 },
            }
        };

        private void addMessage(string content, bool echo = false)
        {
            var sender = new APIUser { Id = 2, Username = "someone" };

            if (echo)
                channel.AddLocalEcho(new LocalEchoMessage { Sender = sender, Content = content, Timestamp = DateTimeOffset.Now, Uuid = Guid.NewGuid().ToString() });
            else
                channel.AddNewMessages(new Message(++nextMessageId) { Sender = sender, Content = content, Timestamp = DateTimeOffset.Now });
        }

        private IEnumerable<BanchosucksChatBeatmapCard> cards => drawableChannel.ChildrenOfType<BanchosucksChatBeatmapCard>();

        private IEnumerable<BanchosucksChatBeatmapPreview> previews => drawableChannel.ChildrenOfType<BanchosucksChatBeatmapPreview>();

        [Test]
        public void TestSetLinkGetsCard()
        {
            AddStep("message with set link", () => addMessage("try https://banchosucks.cc/lazer/beatmapsets/7 it is fun"));
            AddUntilStep("card shown", () => cards.Count(), () => Is.EqualTo(1));
            AddAssert("requests counted by the lookup too", () => lookup.RequestsSent, () => Is.EqualTo(1));
            AddAssert("looked up once, by set id", () => requests.Count == 1 && requests[0].ID == 7 && requests[0].Type == BeatmapSetLookupType.SetId);
            AddAssert("title and artist", () => cards.Single().Info.Title == "Title 7" && cards.Single().Info.Artist == "Artist 7");
            AddAssert("mapper", () => cards.Single().Info.Mapper, () => Is.EqualTo("mapper"));
            AddAssert("star range of the set", () => cards.Single().Info.StarText, () => Is.EqualTo("2.50 - 5.75"));
            AddAssert("card belongs to set 7", () => cards.Single().BeatmapSet.OnlineID, () => Is.EqualTo(7));
            AddAssert("card is loaded and has its size", () => cards.Single().IsLoaded && cards.Single().DrawWidth > 0 && cards.Single().DrawHeight > 0);
        }

        [Test]
        public void TestDifficultyLinkShowsThatDifficulty()
        {
            AddStep("message with osu://b link", () => addMessage("osu://b/72"));
            AddUntilStep("card shown", () => cards.Count(), () => Is.EqualTo(1));
            AddAssert("looked up by beatmap id", () => requests.Single().ID == 72 && requests.Single().Type == BeatmapSetLookupType.BeatmapId);
            AddAssert("stars of the linked difficulty", () => cards.Single().Info.StarText, () => Is.EqualTo("5.75"));
            AddAssert("difficulty named", () => cards.Single().Info.DifficultyName, () => Is.EqualTo("Insane"));
        }

        [Test]
        public void TestSameSetIsLookedUpOnce()
        {
            AddStep("set link", () => addMessage("osu://s/7"));
            AddUntilStep("card shown", () => cards.Count(), () => Is.EqualTo(1));

            AddStep("same set again, and one of its difficulties", () =>
            {
                addMessage("again https://banchosucks.cc/lazer/beatmapsets/7");
                addMessage("https://banchosucks.cc/lazer/beatmapsets/7#osu/71");
            });

            AddUntilStep("three cards", () => cards.Count(), () => Is.EqualTo(3));
            AddAssert("still one request", () => requests.Count, () => Is.EqualTo(1));
            AddAssert("the difficulty card shows its stars", () => cards.Count(c => c.Info.StarText == "2.50"), () => Is.EqualTo(1));
        }

        [Test]
        public void TestNoCardForOtherMessages()
        {
            AddStep("messages without beatmap links", () =>
            {
                addMessage("hello");
                addMessage("https://osu.ppy.sh/beatmapsets/7");
                addMessage("https://banchosucks.cc/lazer/users/3");
            });

            AddWaitStep("wait", 10);
            AddAssert("no previews wanted", () => previews.All(p => p.Link == null));
            AddAssert("nothing looked up", () => requests, () => Is.Empty);
            AddAssert("no cards", () => cards, () => Is.Empty);
        }

        [Test]
        public void TestUnknownSetGetsNoCardAndIsNotAskedAgain()
        {
            AddStep("link to a set that does not exist", () => addMessage("osu://s/404"));
            AddUntilStep("looked up", () => requests.Count, () => Is.EqualTo(1));

            AddStep("the same link again", () => addMessage("osu://s/404 ?"));
            AddWaitStep("wait", 15);
            AddAssert("no second request", () => requests.Count, () => Is.EqualTo(1));
            AddAssert("no cards", () => cards, () => Is.Empty);
        }

        [Test]
        public void TestSettingTurnsPreviewsOff()
        {
            AddStep("previews off", () => config.SetValue(OsuSetting.BanchosucksChatBeatmapPreviews, false));
            AddStep("message with set link", () => addMessage("osu://s/7"));
            AddWaitStep("wait", 15);
            AddAssert("nothing looked up", () => requests, () => Is.Empty);
            AddAssert("no cards", () => cards, () => Is.Empty);

            AddStep("previews on", () => config.SetValue(OsuSetting.BanchosucksChatBeatmapPreviews, true));
            AddUntilStep("card appears for the message on screen", () => cards.Count(), () => Is.EqualTo(1));

            AddStep("previews off again", () => config.SetValue(OsuSetting.BanchosucksChatBeatmapPreviews, false));
            AddAssert("card gone at once", () => cards, () => Is.Empty);
        }

        [Test]
        public void TestOlderMessagesOffScreenAreNotLookedUp()
        {
            AddStep("an old message with a link, then a wall of text", () =>
            {
                var messages = new List<Message>();
                var sender = new APIUser { Id = 2, Username = "someone" };

                messages.Add(new Message(++nextMessageId) { Sender = sender, Content = "old osu://s/5", Timestamp = DateTimeOffset.Now });

                for (int i = 0; i < 120; i++)
                    messages.Add(new Message(++nextMessageId) { Sender = sender, Content = $"line {i}", Timestamp = DateTimeOffset.Now });

                messages.Add(new Message(++nextMessageId) { Sender = sender, Content = "new osu://s/6", Timestamp = DateTimeOffset.Now });

                channel.AddNewMessages(messages.ToArray());
            });

            AddUntilStep("card for the newest message", () => cards.Count(), () => Is.EqualTo(1));
            AddWaitStep("wait", 15);
            AddAssert("only the visible message was looked up", () => requests.Select(r => r.ID), () => Is.EqualTo(new[] { 6 }));

            AddStep("scroll up to the old message", () => drawableChannel.ChildrenOfType<ChannelScrollContainer>().Single().ScrollToStart());
            AddUntilStep("now it gets its card too", () => requests.Select(r => r.ID), () => Is.EquivalentTo(new[] { 5, 6 }));
            AddUntilStep("two cards", () => cards.Count(), () => Is.EqualTo(2));
        }

        [Test]
        public void TestOwnMessageGetsCardOnceSent()
        {
            LocalEchoMessage echo = null!;

            AddStep("echo of own message", () =>
            {
                echo = new LocalEchoMessage
                {
                    Sender = API.LocalUser.Value,
                    Content = "osu://s/7",
                    Timestamp = DateTimeOffset.Now,
                    Uuid = Guid.NewGuid().ToString(),
                };
                channel.AddLocalEcho(echo);
            });

            AddWaitStep("wait", 15);
            AddAssert("no lookup for the echo", () => requests, () => Is.Empty);

            AddStep("server confirms the message", () => channel.ReplaceMessage(echo, new Message(++nextMessageId)
            {
                Sender = API.LocalUser.Value,
                Content = "osu://s/7",
                Timestamp = DateTimeOffset.Now,
                Uuid = echo.Uuid,
            }));

            AddUntilStep("card shown", () => cards.Count(), () => Is.EqualTo(1));
        }

        [Test]
        public void TestRequestsAreSpreadOut()
        {
            AddStep("eight different links at once", () =>
            {
                for (int i = 1; i <= 8; i++)
                    addMessage($"osu://s/{i}");
            });

            AddUntilStep("first request out", () => requests.Count, () => Is.GreaterThanOrEqualTo(1));
            AddAssert("not all at once", () => requests.Count, () => Is.LessThan(8));
            AddUntilStep("all cards arrive in the end", () => cards.Count(), () => Is.EqualTo(8));
            AddAssert("one request per set", () => requests.Count, () => Is.EqualTo(8));
            AddAssert("newest message first", () => requests[0].ID, () => Is.EqualTo(8));
        }
    }
}

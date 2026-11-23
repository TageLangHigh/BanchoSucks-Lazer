// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: which chat messages get a beatmap preview card, and what the card says.
    /// </summary>
    [TestFixture]
    public class BanchosucksChatBeatmapLinkTest
    {
        private string originalWebsiteRoot = null!;

        [SetUp]
        public void SetUp()
        {
            originalWebsiteRoot = MessageFormatter.WebsiteRootUrl;
            MessageFormatter.WebsiteRootUrl = "https://banchosucks.cc/lazer";
        }

        [TearDown]
        public void TearDown()
        {
            MessageFormatter.WebsiteRootUrl = originalWebsiteRoot;
        }

        [TestCase("look at https://banchosucks.cc/lazer/beatmapsets/123", true, 123)]
        [TestCase("https://banchosucks.cc/lazer/beatmapsets/123#osu/456 is hard", false, 456)]
        [TestCase("https://banchosucks.cc/lazer/beatmapsets/123#whatever", true, 123)]
        [TestCase("https://banchosucks.cc/lazer/beatmaps/456", false, 456)]
        [TestCase("https://banchosucks.cc/lazer/beatmaps/456?mode=osu", false, 456)]
        [TestCase("https://banchosucks.cc/lazer/b/456", false, 456)]
        [TestCase("https://banchosucks.cc/lazer/s/123", true, 123)]
        [TestCase("http://banchosucks.cc/lazer/beatmapsets/123", true, 123)]
        [TestCase("osu://b/456", false, 456)]
        [TestCase("play osu://s/123 with me", true, 123)]
        [TestCase("osu://dl/123", true, 123)]
        [TestCase("[this map](https://banchosucks.cc/lazer/beatmapsets/5)", true, 5)]
        [TestCase("[https://banchosucks.cc/lazer/beatmaps/77 Artist - Title [Insane]]", false, 77)]
        public void TestBeatmapLinksAreFound(string text, bool isSet, int id)
        {
            var link = BanchosucksChatBeatmapLink.Find(text);

            Assert.That(link, Is.Not.Null);
            Assert.That(link!.Value.IsSet, Is.EqualTo(isSet));
            Assert.That(link.Value.Id, Is.EqualTo(id));
        }

        [TestCase("hello there")]
        [TestCase("")]
        [TestCase("https://banchosucks.cc/lazer/users/3")]
        [TestCase("https://banchosucks.cc/lazer/beatmapsets/discussions/5")]
        [TestCase("https://banchosucks.cc/lazer/beatmapsets")]
        [TestCase("https://osu.ppy.sh/beatmapsets/123")]
        [TestCase("https://osu.ppy.sh/b/456")]
        [TestCase("https://example.com/lazer/beatmapsets/123")]
        [TestCase("osu://b/abc")]
        [TestCase("osu://b/0")]
        [TestCase("osu://b/-5")]
        [TestCase("osu://chan/#osu")]
        [TestCase("osu://u/123")]
        [TestCase("#osu")]
        public void TestOtherMessagesGetNoPreview(string text)
        {
            Assert.That(BanchosucksChatBeatmapLink.Find(text), Is.Null);
        }

        [Test]
        public void TestFirstLinkWins()
        {
            var link = BanchosucksChatBeatmapLink.Find("first osu://s/11 then https://banchosucks.cc/lazer/beatmaps/22 and https://banchosucks.cc/lazer/users/3");

            Assert.That(link, Is.EqualTo(new BanchosucksChatBeatmapLink(true, 11)));

            link = BanchosucksChatBeatmapLink.Find("profile https://banchosucks.cc/lazer/users/3 then osu://b/22 then osu://s/11");

            Assert.That(link, Is.EqualTo(new BanchosucksChatBeatmapLink(false, 22)), "links that are not beatmaps are skipped");
        }

        [Test]
        public void TestFormattedMessage()
        {
            var message = MessageFormatter.FormatMessage(new Message(1) { Content = "np: [https://banchosucks.cc/lazer/beatmapsets/9#osu/99 some map]" });

            Assert.That(message.DisplayContent, Is.EqualTo("np: some map"));
            Assert.That(BanchosucksChatBeatmapLink.Find(message.Links), Is.EqualTo(new BanchosucksChatBeatmapLink(false, 99)));
            Assert.That(BanchosucksChatBeatmapLink.Find((System.Collections.Generic.IEnumerable<Link>?)null), Is.Null);
        }

        [Test]
        public void TestEquality()
        {
            Assert.That(new BanchosucksChatBeatmapLink(true, 5), Is.EqualTo(new BanchosucksChatBeatmapLink(true, 5)));
            Assert.That(new BanchosucksChatBeatmapLink(true, 5), Is.Not.EqualTo(new BanchosucksChatBeatmapLink(false, 5)), "set 5 and beatmap 5 are different things");
            Assert.That(new BanchosucksChatBeatmapLink(true, 5), Is.Not.EqualTo(new BanchosucksChatBeatmapLink(true, 6)));
            Assert.That(Nullable.Equals<BanchosucksChatBeatmapLink>(null, null), Is.True);
            Assert.That(Nullable.Equals<BanchosucksChatBeatmapLink>(new BanchosucksChatBeatmapLink(true, 5), null), Is.False);
        }

        private static APIBeatmapSet createSet() => new APIBeatmapSet
        {
            OnlineID = 10,
            Title = "Title",
            TitleUnicode = "タイトル",
            Artist = "Artist",
            Author = new APIUser { Id = 4, Username = "Mapper" },
            Beatmaps = new[]
            {
                new APIBeatmap { OnlineID = 101, DifficultyName = "Easy", StarRating = 1.8 },
                new APIBeatmap { OnlineID = 102, DifficultyName = "Insane", StarRating = 5.2149 },
                new APIBeatmap { OnlineID = 103, DifficultyName = "Extra", StarRating = 6.431 },
            }
        };

        [Test]
        public void TestPreviewOfASet()
        {
            var info = BanchosucksChatBeatmapPreviewInfo.From(createSet(), new BanchosucksChatBeatmapLink(true, 10));

            Assert.That(info.Title, Is.EqualTo("Title"));
            Assert.That(info.TitleUnicode, Is.EqualTo("タイトル"));
            Assert.That(info.Artist, Is.EqualTo("Artist"));
            Assert.That(info.ArtistUnicode, Is.EqualTo("Artist"), "falls back to the romanised artist");
            Assert.That(info.Mapper, Is.EqualTo("Mapper"));
            Assert.That(info.DifficultyName, Is.Null);
            Assert.That(info.StarText, Is.EqualTo("1.80 - 6.43"));
        }

        [Test]
        public void TestPreviewOfOneDifficulty()
        {
            var info = BanchosucksChatBeatmapPreviewInfo.From(createSet(), new BanchosucksChatBeatmapLink(false, 102));

            Assert.That(info.DifficultyName, Is.EqualTo("Insane"));
            Assert.That(info.StarText, Is.EqualTo("5.21"));
        }

        [Test]
        public void TestPreviewOfUnknownDifficultyShowsTheSet()
        {
            var info = BanchosucksChatBeatmapPreviewInfo.From(createSet(), new BanchosucksChatBeatmapLink(false, 999));

            Assert.That(info.DifficultyName, Is.Null);
            Assert.That(info.StarText, Is.EqualTo("1.80 - 6.43"));
        }

        [Test]
        public void TestPreviewWithoutDifficulties()
        {
            var set = createSet();
            set.Beatmaps = Array.Empty<APIBeatmap>();

            var info = BanchosucksChatBeatmapPreviewInfo.From(set, new BanchosucksChatBeatmapLink(true, 10));

            Assert.That(info.Stars, Is.Null);
            Assert.That(info.StarText, Is.Empty);
        }

        [Test]
        public void TestPreviewOfSingleDifficultySetShowsOneNumber()
        {
            var set = createSet();
            set.Beatmaps = new[] { set.Beatmaps[0] };

            Assert.That(BanchosucksChatBeatmapPreviewInfo.From(set, new BanchosucksChatBeatmapLink(true, 10)).StarText, Is.EqualTo("1.80"));
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Share;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Banchosucks: the profile card as a drawable, its button in the profile header and where the best play comes
    /// from. The test users have no avatar url, so nothing is loaded from anywhere.
    /// </summary>
    public partial class TestSceneBanchosucksProfileCard : OsuTestScene
    {
        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private NotificationOverlay notificationOverlay = null!;
        private BanchosucksCardExporter exporter = null!;

        private BanchosucksProfileCard card = null!;

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        [BackgroundDependencyLoader]
        private void load()
        {
            // cached before the exporter is created: it resolves the overlay when it loads
            notificationOverlay = new NotificationOverlay
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
            };
            Dependencies.CacheAs<INotificationOverlay>(notificationOverlay);

            exporter = new BanchosucksCardExporter { Timeout = 1500 };
            Dependencies.Cache(exporter);

            base.Content.AddRange(new Drawable[] { Content, exporter, notificationOverlay });
        }

        private bool germanBefore;

        // the button writes the card in the client language, which other test scenes switch; this one wants English.
        // once per fixture: the steps of a test run in the base class's tear-down, after any tear-down of this class.
        [OneTimeSetUp]
        public void RememberLanguage() => germanBefore = ClickerStrings.IsGerman;

        [OneTimeTearDown]
        public void RestoreLanguage() => ClickerStrings.IsGerman = germanBefore;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("cards in English", () => ClickerStrings.IsGerman = false);
            AddUntilStep("exporter idle", () => !exporter.Busy);
            AddStep("clear", () =>
            {
                BanchosucksProfileBestScores.Clear();
                notificationOverlay.ChildrenOfType<Notification>().ToArray().ForEach(n => n.Close(false));
                dummyAPI.HandleRequest = null;
                Clear();
            });
        }

        private static APIUser createUser(bool longTexts = false) => new APIUser
        {
            Id = 1001,
            Username = longTexts ? "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWW" : "Lidl",
            CountryCode = longTexts ? CountryCode.GS : CountryCode.DE,
            Statistics = new UserStatistics
            {
                GlobalRank = longTexts ? 1234567 : 1234,
                CountryRank = longTexts ? 123456 : 56,
                PP = longTexts ? 123456.7m : 5432.6m,
                Accuracy = 98.7654,
                PlayCount = longTexts ? 12345678 : 12345,
                Level = new UserStatistics.LevelInfo { Current = 101, Progress = 45 },
            }
        };

        private static SoloScoreInfo createBest(bool longTexts = false, double pp = 523.4) => new SoloScoreInfo
        {
            ID = 77,
            PP = pp,
            Rank = ScoreRank.S,
            Accuracy = 0.99,
            Ranked = true,
            Preserve = true,
            Processed = true,
            EndedAt = DateTimeOffset.Now,
            BeatmapID = 55,
            Beatmap = new APIBeatmap
            {
                OnlineID = 55,
                Status = BeatmapOnlineStatus.Ranked,
                DifficultyName = longTexts ? string.Join(' ', Enumerable.Repeat("Extra Extreme", 10)) : "Extra",
                BeatmapSet = new APIBeatmapSet
                {
                    Title = longTexts ? string.Join(' ', Enumerable.Repeat("A Very Long Song Title", 10)) : "Best Song",
                    Artist = "Best Artist",
                },
            }
        };

        private void showCard(bool longTexts, bool german = false, bool withBest = true) => AddStep("show card", () => Child = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(BanchosucksCardExporter.CARD_WIDTH, BanchosucksCardExporter.CARD_HEIGHT),
            Scale = new Vector2(0.6f),
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.DarkGray },
                card = new BanchosucksProfileCard(BanchosucksProfileCardData.From(createUser(longTexts), new OsuRuleset().RulesetInfo, withBest ? createBest(longTexts) : null, german)),
            }
        });

        private bool textsInsideCard()
        {
            RectangleF bounds = card.ScreenSpaceDrawQuad.AABBFloat.Inflate(2);

            return card.ChildrenOfType<SpriteText>().All(t =>
            {
                RectangleF text = t.ScreenSpaceDrawQuad.AABBFloat;
                return text.Width == 0 || bounds.Contains(text.TopLeft) && bounds.Contains(text.BottomRight);
            });
        }

        private bool hasText(string text) => card.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == text);

        [Test]
        public void TestCardBuilds()
        {
            showCard(false);

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("fixed size", () => card.DrawSize, () => Is.EqualTo(new Vector2(1200, 630)));
            AddAssert("name", () => hasText("Lidl"));
            AddAssert("country", () => hasText("Germany"));
            AddAssert("ranks", () => hasText("#1,234") && hasText("#56"));
            AddAssert("pp, accuracy, plays, level", () => hasText("5,433") && hasText("98.76%") && hasText("12,345") && hasText("101"));
            AddAssert("best play", () => hasText("Best Artist - Best Song [Extra]") && hasText("523pp") && hasText("BEST PLAY"));
            AddAssert("website mark", () => hasText("banchosucks.cc"));
            AddAssert("avatar and flag have a picture", () => card.ChildrenOfType<Sprite>().Count(s => s.Texture != null), () => Is.GreaterThanOrEqualTo(2));
            AddWaitStep("let it lay out", 3);
            AddAssert("everything inside the card", textsInsideCard);
        }

        [Test]
        public void TestGermanCardWithoutBestPlay()
        {
            showCard(false, german: true, withBest: false);

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("labels", () => hasText("WELTWEIT") && hasText("LAND") && hasText("GENAUIGKEIT") && hasText("SPIELE"));
            AddAssert("numbers", () => hasText("#1.234") && hasText("98,76%"));
            AddAssert("no best play line", () => !hasText("BESTER SCORE"));
        }

        [Test]
        public void TestLongTextsStayInside()
        {
            showCard(true);

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddWaitStep("let it lay out", 3);
            AddAssert("everything inside the card", textsInsideCard);
        }

        [Test]
        public void TestBestPlayComesFromTheProfilesScoreList()
        {
            var profile = new Bindable<UserProfileData?>();
            BanchosucksProfileCardButton button = null!;
            int scoreRequests = 0;

            AddStep("set up server", () =>
            {
                dummyAPI.HandleRequest = request =>
                {
                    if (request is not GetUserScoresRequest scoresRequest)
                        return false;

                    scoreRequests++;
                    scoresRequest.TriggerSuccess(new List<SoloScoreInfo> { createBest(pp: 700), createBest(pp: 650) });
                    return true;
                };
            });

            AddStep("show header button and the best list", () =>
            {
                profile.Value = null;

                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        button = new BanchosucksProfileCardButton { User = { BindTarget = profile } },
                        new PaginatedScoreContainer(ScoreType.Best, profile, "Best performance"),
                    }
                };
            });

            AddUntilStep("button loaded", () => button.IsLoaded);
            AddAssert("nothing to make a card of yet", () => !button.Available && button.CreateData() == null);

            AddStep("profile arrives", () => profile.Value = new UserProfileData(createUser(), new OsuRuleset().RulesetInfo));
            AddAssert("button offered", () => button.Available);
            AddUntilStep("the list asked the server once", () => scoreRequests, () => Is.EqualTo(1));
            AddUntilStep("best play known to the card", () => button.CreateData()?.BestPpText, () => Is.EqualTo("700pp"));
            AddAssert("the card asked nothing itself", () => scoreRequests, () => Is.EqualTo(1));
            AddAssert("statistics from the profile", () => button.CreateData()!.GlobalRankText, () => Is.EqualTo("#1,234"));

            AddStep("click", () => button.TriggerClick());
            AddUntilStep("export ran", () => !exporter.Busy && notificationOverlay.ChildrenOfType<SimpleNotification>().Any(n => n.Text.ToString().Contains("Profile card")));
            AddAssert("still one request", () => scoreRequests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestExport()
        {
            Task<string?> export = null!;

            AddStep("export", () => export = exporter.Export(
                new BanchosucksProfileCard(BanchosucksProfileCardData.From(createUser(), new OsuRuleset().RulesetInfo, createBest())), "test profile", ("Profile card", "Profil-Karte")));
            AddUntilStep("export finished", () => export.IsCompleted);
            AddAssert("file named after the card, or the host does not draw", () =>
            {
                string? filename = export.GetResultSafely();
                return filename == null || filename.StartsWith("test profile_", StringComparison.Ordinal);
            });
            AddAssert("not busy any more", () => !exporter.Busy);
        }
    }
}

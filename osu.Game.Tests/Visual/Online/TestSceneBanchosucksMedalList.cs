// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// The medal overview of the profile overlay with a fake catalogue. The scene has a texture store without any source, so
    /// no medal image is fetched from anywhere.
    /// </summary>
    public partial class TestSceneBanchosucksMedalList : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private readonly BanchosucksMedalCatalogueCache cache = new BanchosucksMedalCatalogueCache();
        private readonly Bindable<UserProfileData?> user = new Bindable<UserProfileData?>();

        private BanchosucksMedalList list = null!;
        private FillFlowContainer lists = null!;

        private int requests;
        private bool fail;

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private static readonly DateTimeOffset monday = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            Dependencies.Cache(new LargeTextureStore(host.Renderer));
        }

        private static List<BanchosucksCatalogueMedal> catalogue()
        {
            var medals = new List<BanchosucksCatalogueMedal>
            {
                new BanchosucksCatalogueMedal { Id = 200001, Name = "Lazer Pioneer", Description = "Dein erster Play auf Banchosucks Lazer.", Slug = "banchosucks_lazer_first", Banchosucks = true },
                new BanchosucksCatalogueMedal { Id = 200009, Name = "Marathon", Description = "Eine Map mit mindestens 10 Minuten Spielzeit gepasst.", Slug = "banchosucks_marathon", Banchosucks = true },
                new BanchosucksCatalogueMedal { Id = 200012, Name = "Teamplayer", Description = "Beim erreichten Community-Ziel der Woche mitgespielt.", Slug = "banchosucks_teamplayer", Banchosucks = true },
                new BanchosucksCatalogueMedal { Id = 100001, Name = "First Anniversary", Description = "g0v0 turns one.", Slug = "g0v0_first_anniversary" },
            };

            // a long list of osu! medals, as on the real server
            for (int i = 1; i <= 40; i++)
                medals.Add(new BanchosucksCatalogueMedal { Id = i, Name = $"osu! medal {i}", Description = $"Description {i}", Slug = $"osu-medal-{i}" });

            return medals;
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("english", () => ClickerStrings.IsGerman = false);

            AddStep("fake plugin", () =>
            {
                requests = 0;
                fail = false;
                cache.Clear();
                dummyAPI.SetState(APIState.Online);

                dummyAPI.HandleRequest = request =>
                {
                    if (request is not GetBanchosucksMedalCatalogueRequest catalogueRequest)
                        return false;

                    requests++;

                    if (fail)
                        return false;

                    catalogueRequest.TriggerSuccess(new BanchosucksMedalCatalogueResponse { Medals = catalogue() });
                    return true;
                };
            });

            AddStep("player with three medals", () => user.Value = new UserProfileData(new APIUser
            {
                Id = 2,
                Username = "Lidl",
                Achievements = new[]
                {
                    new APIUserAchievement { ID = 200009, AchievedAt = monday },
                    new APIUserAchievement { ID = 200001, AchievedAt = monday.AddDays(-3) },
                    new APIUserAchievement { ID = 7, AchievedAt = monday.AddDays(-40) },
                },
            }, new OsuRuleset().RulesetInfo));
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
            AddStep("create list", () => Child = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(20),
                Child = lists = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Child = list = new BanchosucksMedalList(user, cache),
                },
            });
        }

        private BanchosucksMedalList.MedalGroup group(BanchosucksMedalGroup kind) => list.ChildrenOfType<BanchosucksMedalList.MedalGroup>().Single(g => g.Group.Kind == kind);

        [Test]
        public void TestGroupsCountsAndHighlight()
        {
            create();

            AddUntilStep("overview shown", () => list.Overview != null);
            AddAssert("x of y", () => list.SummaryText, () => Is.EqualTo("3 of 44 medals"));
            AddAssert("three groups", () => list.ChildrenOfType<BanchosucksMedalList.MedalGroup>().Select(g => g.Group.Kind),
                () => Is.EqualTo(new[] { BanchosucksMedalGroup.Banchosucks, BanchosucksMedalGroup.G0v0, BanchosucksMedalGroup.Osu }));

            // our own medals are listed in full, the achieved ones first and solid
            AddAssert("all banchosucks medals listed", () => group(BanchosucksMedalGroup.Banchosucks).ShownMedals, () => Is.EqualTo(3));
            AddAssert("achieved first", () => group(BanchosucksMedalGroup.Banchosucks).ChildrenOfType<BanchosucksMedalList.MedalTile>().Select(t => t.Entry.Medal.Id),
                () => Is.EqualTo(new[] { 200009, 200001, 200012 }));
            AddAssert("achieved solid, others faint", () => group(BanchosucksMedalGroup.Banchosucks).ChildrenOfType<BanchosucksMedalList.MedalTile>().Select(t => t.Alpha == 1),
                () => Is.EqualTo(new[] { true, true, false }));

            AddAssert("tooltip of an achieved medal", () => group(BanchosucksMedalGroup.Banchosucks).ChildrenOfType<BanchosucksMedalList.MedalTile>().First().TooltipText.ToString(),
                () => Does.StartWith("Marathon\nEine Map mit mindestens 10 Minuten Spielzeit gepasst.\nAchieved on "));
            AddAssert("tooltip of a missing medal", () => group(BanchosucksMedalGroup.Banchosucks).ChildrenOfType<BanchosucksMedalList.MedalTile>().Last().TooltipText.ToString(),
                () => Does.EndWith("Not achieved yet"));

            // of the other groups only the player's medals, until asked
            AddAssert("one osu! medal listed", () => group(BanchosucksMedalGroup.Osu).ShownMedals, () => Is.EqualTo(1));
            AddAssert("no g0v0 medal listed", () => group(BanchosucksMedalGroup.G0v0).ShownMedals, () => Is.Zero);

            AddStep("show all osu! medals", () => group(BanchosucksMedalGroup.Osu).ShowAll());
            AddAssert("all forty listed", () => group(BanchosucksMedalGroup.Osu).ShownMedals, () => Is.EqualTo(40));
            AddAssert("the player's one first", () => group(BanchosucksMedalGroup.Osu).ChildrenOfType<BanchosucksMedalList.MedalTile>().First().Entry.Medal.Id, () => Is.EqualTo(7));

            AddAssert("one request", () => requests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestCatalogueIsAskedForOnce()
        {
            create();
            AddUntilStep("overview shown", () => list.Overview != null);

            // the next profile: another list, the same catalogue
            AddStep("open another profile", () =>
            {
                user.Value = new UserProfileData(new APIUser { Id = 3, Username = "Hodenlos", Achievements = Array.Empty<APIUserAchievement>() }, new OsuRuleset().RulesetInfo);
                lists.Add(new BanchosucksMedalList(user, cache));
            });

            AddUntilStep("both lists show the new player", () => lists.ChildrenOfType<BanchosucksMedalList>().All(l => l.SummaryText == "0 of 44 medals"));
            AddAssert("still one request", () => requests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNoCountWhenTheServerSentNoMedalsForThePlayer()
        {
            AddStep("player without a medal list", () => user.Value = new UserProfileData(new APIUser { Id = 4, Username = "Guest" }, new OsuRuleset().RulesetInfo));
            create();

            AddUntilStep("overview shown", () => list.Overview != null);
            AddAssert("no x of y", () => list.SummaryText, () => Does.StartWith("44 medals (the server did not send"));
            AddAssert("nothing faint", () => list.ChildrenOfType<BanchosucksMedalList.MedalTile>().All(t => t.Alpha == 1));
        }

        [Test]
        public void TestCatalogueUnavailable()
        {
            AddStep("plugin fails", () => fail = true);
            create();

            AddUntilStep("said so", () => list.SummaryText, () => Is.EqualTo("The medal list could not be loaded."));
            AddAssert("no groups", () => list.ChildrenOfType<BanchosucksMedalList.MedalGroup>(), () => Is.Empty);
            AddWaitStep("wait", 5);
            AddAssert("not retried at once", () => requests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNoRequestWhileLoggedOut()
        {
            AddStep("logged out", () => dummyAPI.SetState(APIState.Offline));
            create();

            AddWaitStep("wait", 5);
            AddAssert("nothing requested", () => requests, () => Is.Zero);
            AddAssert("said so", () => list.SummaryText, () => Is.EqualTo("The medal list could not be loaded."));
        }
    }
}

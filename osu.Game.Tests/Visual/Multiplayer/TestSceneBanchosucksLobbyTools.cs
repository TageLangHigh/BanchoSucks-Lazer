// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets;
using osu.Game.Screens.Banchosucks.Lobby;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osu.Game.Tests.Resources;
using MatchType = osu.Game.Online.Rooms.MatchType;

namespace osu.Game.Tests.Visual.Multiplayer
{
    /// <summary>
    /// Banchosucks: the host's lobby tournament tools inside a multiplayer room, against the test multiplayer server.
    /// </summary>
    public partial class TestSceneBanchosucksLobbyTools : MultiplayerTestScene
    {
        private const int room_channel = 4321;

        private MultiplayerMatchSubScreen screen = null!;
        private ChannelManager channelManager = null!;

        /// <summary>
        /// What the panel announced (through the test hook).
        /// </summary>
        private readonly List<string> announced = new List<string>();

        /// <summary>
        /// What reached the chat API of the test server.
        /// </summary>
        private readonly List<Message> posted = new List<Message>();

        private BanchosucksLobbyToolsPanel panel => screen.ChildrenOfType<BanchosucksLobbyToolsPanel>().Single();
        private BanchosucksLobbyToolsButton button => screen.ChildrenOfType<BanchosucksLobbyToolsButton>().Single();
        private BanchosucksLobbyTemplateManager templates => screen.ChildrenOfType<BanchosucksLobbyTemplateManager>().Single();

        private int localId => API.LocalUser.Value.Id;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            BeatmapManager beatmaps;

            Dependencies.Cache(new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);
            Dependencies.CacheAs<BeatmapStore>(new RealmDetachedBeatmapStore());
            Dependencies.Cache(channelManager = new ChannelManager(API));

            // lobby templates of this scene go to its own temporary folder, not into the game's data
            Dependencies.CacheAs(LocalStorage);

            beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();

            Realm.Write(r =>
            {
                foreach (var beatmapInfo in r.All<BeatmapInfo>())
                    beatmapInfo.OnlineMD5Hash = beatmapInfo.MD5Hash;
            });
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("answer chat posts", () =>
            {
                announced.Clear();
                posted.Clear();

                var dummy = (DummyAPIAccess)API;
                var inner = dummy.HandleRequest;

                dummy.HandleRequest = request =>
                {
                    if (request is PostMessageRequest post)
                    {
                        var message = new Message(posted.Count + 1)
                        {
                            ChannelId = post.Message.ChannelId,
                            Content = post.Message.Content,
                            IsAction = post.Message.IsAction,
                            Timestamp = post.Message.Timestamp,
                            Sender = post.Message.Sender,
                            Uuid = post.Message.Uuid,
                        };

                        posted.Add(message);
                        post.TriggerSuccess(message);
                        return true;
                    }

                    return inner?.Invoke(request) ?? false;
                };
            });
        }

        private void joinRoom(MatchType type, bool withChat = true, bool captureAnnouncements = true)
        {
            AddStep("create room", () =>
            {
                var room = CreateDefaultRoom(type);
                room.ChannelId = withChat ? room_channel : 0;
                LoadScreen(screen = new MultiplayerMatchSubScreen(room));
            });
            AddUntilStep("wait for load", () => screen.IsCurrentScreen() && screen.ChildrenOfType<MultiplayerMatchSettingsOverlay>().Any(o => o.IsLoaded));
            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();
            AddUntilStep("wait for join", () => RoomJoined);
            AddUntilStep("panel loaded", () => panel.IsLoaded);

            if (captureAnnouncements)
            {
                AddStep("capture announcements", () => panel.AnnounceOverride = text =>
                {
                    announced.Add(text);
                    return true;
                });
            }
        }

        private void addPlayers(params int[] ids)
        {
            AddStep($"add players {string.Join(", ", ids)}", () =>
            {
                // the room shows the names the user lookup of the test server gives: "User <id>"
                foreach (int id in ids)
                    MultiplayerClient.AddUser(new APIUser { Id = id, Username = $"User {id}" });
            });
            AddUntilStep("players arrived", () => panel.Players.Count == ids.Length + 1);
        }

        [Test]
        public void TestButtonOpensThePanelForTheHostOnly()
        {
            joinRoom(MatchType.HeadToHead);

            AddUntilStep("button shown to the host", () => button.Alpha == 1);
            AddAssert("panel closed", () => panel.State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddStep("click the button", () => button.TriggerClick());
            AddUntilStep("panel open", () => panel.State.Value == Visibility.Visible && panel.Alpha == 1);

            AddStep("back", () => screen.OnBackButton());
            AddAssert("panel closed, room not left", () => panel.State.Value == Visibility.Hidden && screen.IsCurrentScreen() && RoomJoined);

            AddStep("open again", () => button.TriggerClick());
            AddUntilStep("panel open", () => panel.State.Value == Visibility.Visible);

            addPlayers(PLAYER_1_ID);
            AddStep("somebody else becomes host", () => MultiplayerClient.TransferHost(PLAYER_1_ID).FireAndForget());
            AddUntilStep("button hidden", () => button.Alpha == 0);
            AddUntilStep("panel closed", () => panel.State.Value == Visibility.Hidden);

            AddAssert("tools refuse", () => !panel.DrawTeams("x") && !panel.RollSelected() && !panel.SendServerRoll());
            AddAssert("with a reason", () => panel.Note, () => Does.Contain("host"));
            AddAssert("nothing announced", () => announced, () => Is.Empty);
            AddAssert("client still connected", () => MultiplayerClient.IsConnected.Value);
        }

        [Test]
        public void TestTeamDrawInHeadToHeadOnlyAnnounces()
        {
            joinRoom(MatchType.HeadToHead);
            addPlayers(PLAYER_1_ID, PLAYER_2_ID, 57);

            AddStep("draw with a seed", () => Assert.That(panel.DrawTeams("123456")));

            AddAssert("same teams as the pure draw", () =>
            {
                var expected = BanchosucksLobbyDraw.DrawTeams(panel.Players, "123456");
                return panel.LastDraw!.Red.SequenceEqual(expected.Red) && panel.LastDraw.Blue.SequenceEqual(expected.Blue);
            });
            AddAssert("everybody drawn", () => panel.LastDraw!.Red.Count + panel.LastDraw.Blue.Count, () => Is.EqualTo(4));
            AddAssert("announced with seed and names", () => announced, () => Is.EqualTo(BanchosucksLobbyToolsText.TeamDraw(panel.LastDraw!, false)));
            AddAssert("seed in the announcement", () => announced.Single(), () => Does.Contain("seed 123456").And.Contain("User 57"));
            AddAssert("nobody asked to switch", () => announced.Single(), () => Does.Not.Contain("switch"));

            AddStep("draw without a seed", () =>
            {
                announced.Clear();
                Assert.That(panel.DrawTeams());
            });
            AddAssert("a seed was made up and announced", () => panel.LastDraw!.Seed.Length == 6 && announced.Single().Contains($"seed {panel.LastDraw.Seed}"));
        }

        [Test]
        public void TestTeamDrawInTeamVersusSetsOnlyTheHostsTeam()
        {
            BanchosucksLobbyTeam drawnForHost = default;
            string seed = string.Empty;

            joinRoom(MatchType.TeamVersus);
            addPlayers(PLAYER_1_ID, PLAYER_2_ID, 57);

            AddStep("find a seed that moves the host to the other team", () =>
            {
                int current = ((TeamVersusUserState)MultiplayerClient.LocalUser!.MatchState!).TeamID;

                seed = Enumerable.Range(0, 100).Select(i => i.ToString()).First(s => (int)BanchosucksLobbyDraw.DrawTeams(panel.Players, s).TeamOf(localId)!.Value != current);
                drawnForHost = BanchosucksLobbyDraw.DrawTeams(panel.Players, seed).TeamOf(localId)!.Value;
            });

            int[] teamsBefore = null!;

            AddStep("remember the others' teams", () => teamsBefore = MultiplayerClient.ServerRoom!.Users.Where(u => u.UserID != localId).Select(u => ((TeamVersusUserState)u.MatchState!).TeamID).ToArray());

            AddStep("draw", () => Assert.That(panel.DrawTeams(seed)));

            AddUntilStep("host moved to the drawn team", () => ((TeamVersusUserState)MultiplayerClient.LocalUser!.MatchState!).TeamID == (int)drawnForHost);
            AddAssert("the others were not touched", () => MultiplayerClient.ServerRoom!.Users.Where(u => u.UserID != localId).Select(u => ((TeamVersusUserState)u.MatchState!).TeamID),
                () => Is.EqualTo(teamsBefore));
            AddAssert("players asked to switch themselves", () => announced.Last(), () => Does.EndWith("Please switch to your team yourselves."));

            AddUntilStep("panel knows who still has to switch", () =>
            {
                var expected = MultiplayerClient.ServerRoom!.Users
                                                .Where(u => (int)panel.LastDraw!.TeamOf(u.UserID)!.Value != ((TeamVersusUserState)u.MatchState!).TeamID)
                                                .Select(u => u.UserID);

                return panel.NotOnDrawnTeam.Select(p => p.UserId).OrderBy(id => id).SequenceEqual(expected.OrderBy(id => id)) && panel.NotOnDrawnTeam.All(p => p.UserId != localId);
            });
        }

        [Test]
        public void TestRollForChosenPlayersAndServerRoll()
        {
            joinRoom(MatchType.HeadToHead);
            addPlayers(PLAYER_1_ID, PLAYER_2_ID);

            AddAssert("nobody ticked: refused", () => !panel.RollSelected() && panel.Note.Contains("at least one"));

            AddStep("tick two players", () => panel.SelectForRoll(new[] { PLAYER_1_ID, PLAYER_2_ID }));
            AddAssert("checkboxes follow", () => panel.ChildrenOfType<OsuCheckbox>().Count(c => c.Current.Value), () => Is.EqualTo(2));

            AddStep("roll", () =>
            {
                var numbers = new Queue<int>(new[] { 42, 77 });
                panel.RollSource = (_, _) => numbers.Dequeue();
                Assert.That(panel.RollSelected());
            });
            AddAssert("announced", () => announced.Single(), () => Is.EqualTo($"Roll 1-100: User {PLAYER_1_ID} 42, User {PLAYER_2_ID} 77. Highest: User {PLAYER_2_ID}."));

            AddStep("untick one in the panel", () => panel.ChildrenOfType<OsuCheckbox>().First(c => c.Current.Value).Current.Value = false);
            AddAssert("one left", () => panel.SelectedForRoll.Count, () => Is.EqualTo(1));

            AddStep("draw teams and tick the captains", () =>
            {
                panel.DrawTeams("captains");
                panel.SelectCaptains();
            });
            AddAssert("first drawn player of each team", () => panel.SelectedForRoll, () => Is.EquivalentTo(new[] { panel.LastDraw!.Red[0].UserId, panel.LastDraw.Blue[0].UserId }));

            AddStep("server roll", () =>
            {
                announced.Clear();
                Assert.That(panel.SendServerRoll());
            });
            AddAssert("the bot command went to the chat as it is", () => announced.Single(), () => Is.EqualTo("!roll"));
        }

        [Test]
        public void TestAnnouncementsAreOrdinaryRoomChatMessages()
        {
            joinRoom(MatchType.HeadToHead, captureAnnouncements: false);
            addPlayers(PLAYER_1_ID);

            AddUntilStep("room chat joined", () => channelManager.JoinedChannels.Any(c => c.Type == ChannelType.Multiplayer && c.Id == room_channel));

            AddStep("draw teams", () => Assert.That(panel.DrawTeams("chat")));
            AddUntilStep("posted to the room channel", () => posted.Count == 1);
            AddAssert("as a normal message of the host", () =>
            {
                var message = posted.Single();
                return message.ChannelId == room_channel && !message.IsAction && message.Sender.Id == localId && message.Content.StartsWith("Team draw, 2 players, seed chat:", System.StringComparison.Ordinal);
            });

            AddStep("send !roll", () => Assert.That(panel.SendServerRoll()));
            AddUntilStep("posted unchanged", () => posted.Count == 2 && posted[1].Content == "!roll" && posted[1].ChannelId == room_channel);
            AddAssert("client still connected", () => MultiplayerClient.IsConnected.Value);
        }

        [Test]
        public void TestWithoutRoomChatNothingIsPretended()
        {
            joinRoom(MatchType.HeadToHead, withChat: false, captureAnnouncements: false);
            addPlayers(PLAYER_1_ID);

            AddAssert("draw refused", () => !panel.DrawTeams("x"));
            AddAssert("host told why", () => panel.Note, () => Does.Contain("room chat is not available"));
            AddAssert("no draw kept", () => panel.LastDraw, () => Is.Null);
            AddAssert("nothing posted", () => posted, () => Is.Empty);
        }

        [Test]
        public void TestBanPickBoardFromTheRoomQueue()
        {
            joinRoom(MatchType.TeamVersus);

            AddStep("queue three more maps", () =>
            {
                foreach (int id in new[] { 9001, 9002, 9003 })
                    MultiplayerClient.AddPlaylistItem(new MultiplayerPlaylistItem { BeatmapID = id, RulesetID = 0 }).FireAndForget();
            });
            AddUntilStep("queued", () => MultiplayerClient.ServerRoom!.Playlist.Count == 4);

            AddStep("blue bans first, one ban each", () =>
            {
                panel.SetFirstTeam(BanchosucksLobbyTeam.Blue);
                panel.SetBans(1);
                panel.SetSource(null);
                panel.StartBoard();
            });
            AddUntilStep("board started with the queue's maps", () => panel.Board?.Maps.Count == 4);
            AddAssert("maps have names", () => panel.Board!.Maps.All(m => m.Contains(" - ") && m.Contains('[')));
            AddAssert("start announced", () => announced.Single(), () => Does.Contain("4 maps").And.Contain("1 ban(s) per team").And.Contain("Team Blue bans first"));
            AddAssert("a row per map", () => panel.ChildrenOfType<BanchosucksLobbyToolsPanel.BoardRow>().Count(), () => Is.EqualTo(4));

            AddStep("ban, ban, pick", () =>
            {
                announced.Clear();
                Assert.That(panel.Act(0));
                Assert.That(panel.Act(0), Is.False, "a banned map cannot be taken again");
                Assert.That(panel.Act(1));
                Assert.That(panel.Act(3));
            });

            AddAssert("every step announced, then the decider", () => announced, () => Is.EqualTo(new[]
            {
                $"Team Blue bans: {panel.Board!.Maps[0]}",
                $"Team Red bans: {panel.Board.Maps[1]}",
                $"Team Red picks: {panel.Board.Maps[3]}",
                $"Decider: {panel.Board.Maps[2]}",
            }));
            AddAssert("board finished", () => panel.Board!.NextTurn == null && panel.Board.Decider == 2);
            AddAssert("no buttons left on the rows", () => !panel.ChildrenOfType<BanchosucksLobbyToolsPanel.BoardRow>().SelectMany(r => r.ChildrenOfType<OsuButton>()).Any());

            AddStep("undo", () => Assert.That(panel.UndoBoard()));
            AddAssert("announced too", () => announced.Last(), () => Is.EqualTo($"Taken back: Team Red's pick of {panel.Board!.Maps[3]}"));
            AddAssert("two maps can be picked again", () => panel.ChildrenOfType<BanchosucksLobbyToolsPanel.BoardRow>().Count(r => r.ChildrenOfType<OsuButton>().Any()), () => Is.EqualTo(2));

            AddAssert("the room was never touched", () => MultiplayerClient.ServerRoom!.Playlist.Count == 4 && MultiplayerClient.ServerRoom.Playlist.All(i => !i.Expired));
        }

        [Test]
        public void TestBanPickBoardFromATemplate()
        {
            joinRoom(MatchType.HeadToHead);

            AddStep("save a template with five maps", () =>
            {
                var template = new BanchosucksLobbyTemplate
                {
                    Name = "Pool",
                    Playlist = Enumerable.Range(1, 5).Select(i => new BanchosucksLobbyTemplateItem { BeatmapId = 100 + i, Title = $"Pool map {i}" }).ToList(),
                };

                template.Playlist[1].RequiredMods.Add(new BanchosucksLobbyTemplateMod { Acronym = "HD" });
                template.Playlist[1].RequiredMods.Add(new BanchosucksLobbyTemplateMod { Acronym = "HR" });

                Assert.That(templates.Store.Save(template));
            });

            AddAssert("source starts at the room queue", () => panel.BoardTemplate, () => Is.Null);
            AddStep("step to the template", () => panel.CycleSource());
            AddAssert("template chosen", () => panel.BoardTemplate?.Name, () => Is.EqualTo("Pool"));

            AddStep("two bans each", () =>
            {
                panel.SetBans(2);
                panel.StartBoard();
            });
            AddAssert("board has the template's maps, mods named", () => panel.Board!.Maps, () => Is.EqualTo(new[] { "Pool map 1", "Pool map 2 +HDHR", "Pool map 3", "Pool map 4", "Pool map 5" }));
            AddAssert("two bans each fit five maps", () => panel.Board!.BansPerTeam, () => Is.EqualTo(2));

            AddStep("step on", () => panel.CycleSource());
            AddAssert("back at the room queue", () => panel.BoardTemplate, () => Is.Null);

            AddStep("clean up", () => templates.Delete("Pool"));
        }
    }
}

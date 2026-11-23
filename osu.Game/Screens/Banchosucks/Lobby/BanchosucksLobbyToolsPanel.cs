// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: the host's tool panel inside a multiplayer room for a small tournament match: draw teams, roll,
    /// and keep a ban/pick board. Everything is worked out on the host's client and announced in the room chat as
    /// ordinary messages from the host, so every player sees it and the server needs nothing new.
    /// </summary>
    /// <remarks>
    /// The panel only reads the multiplayer client. The one thing it sends besides chat messages is the host's own
    /// team after a draw in team versus mode, through the same request a click on the own team colour sends; the game
    /// has no way for a host to put other players into a team. Scores and results are never touched.
    /// </remarks>
    public partial class BanchosucksLobbyToolsPanel : VisibilityContainer
    {
        private const float row_height = 30;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved]
        private BanchosucksLobbyTemplateManager templates { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        /// <summary>
        /// Replaces posting to the room chat (tests). Returns whether the message was accepted.
        /// </summary>
        internal Func<string, bool>? AnnounceOverride;

        /// <summary>
        /// The source of local rolls: a number from the first to the second argument, both included.
        /// </summary>
        internal Func<int, int, int> RollSource = (min, max) => RandomNumberGenerator.GetInt32(min, max + 1);

        /// <summary>
        /// The last team draw of this room visit.
        /// </summary>
        public BanchosucksTeamDrawResult? LastDraw { get; private set; }

        /// <summary>
        /// The ban/pick board, once one was started.
        /// </summary>
        public BanchosucksBanPickBoard? Board { get; private set; }

        /// <summary>
        /// Bans per team for the next board.
        /// </summary>
        public int BansPerTeam { get; private set; } = 1;

        /// <summary>
        /// The team that bans first on the next board.
        /// </summary>
        public BanchosucksLobbyTeam FirstTeam { get; private set; } = BanchosucksLobbyTeam.Red;

        /// <summary>
        /// The template the next board takes its maps from; null means the room's queue.
        /// </summary>
        public BanchosucksLobbyTemplate? BoardTemplate { get; private set; }

        /// <summary>
        /// What the panel last told the host (an error, or nothing).
        /// </summary>
        public string Note { get; private set; } = string.Empty;

        public OsuTextBox SeedBox { get; private set; } = null!;

        private readonly HashSet<int> selectedForRoll = new HashSet<int>();

        private OsuTextFlowContainer noteText = null!;
        private OsuTextFlowContainer drawText = null!;
        private FillFlowContainer<OsuCheckbox> rollPlayers = null!;
        private RoundedButton captainsButton = null!;
        private RoundedButton sourceButton = null!;
        private RoundedButton firstTeamButton = null!;
        private OsuSpriteText bansText = null!;
        private OsuSpriteText turnText = null!;
        private FillFlowContainer boardRows = null!;
        private RoundedButton undoButton = null!;

        private bool startingBoard;

        public BanchosucksLobbyToolsPanel()
        {
            RelativeSizeAxes = Axes.Both;
            // leaves the chat column free, so the host sees the announcements arrive
            Width = 0.66f;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4Extensions.FromHex(@"2d2933"),
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(15),
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Padding = new MarginPadding { Right = 12 },
                        Children = new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = row_height,
                                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 110) },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = ClickerStrings.Text("Lobby tournament tools", "Mini-Turnier in der Lobby"),
                                            Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                        },
                                        button(ClickerStrings.Pick("Close", "Schließen"), Hide, colours.Gray5),
                                    }
                                }
                            },
                            paragraph(ClickerStrings.Pick(
                                "Results are announced in the room chat as messages from you. Nothing here changes the room, a score or a result.",
                                "Ergebnisse werden als Nachrichten von dir im Raum-Chat angesagt. Nichts hier ändert den Raum, einen Score oder ein Ergebnis.")),
                            noteText = new OsuTextFlowContainer(t =>
                            {
                                t.Font = OsuFont.GetFont(size: 14);
                                t.Colour = colours.Red1;
                            })
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },

                            // ---- teams
                            header(ClickerStrings.Pick("Draw teams", "Teams auslosen")),
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 40,
                                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 10), new Dimension(GridSizeMode.Absolute, 160) },
                                Content = new[]
                                {
                                    new Drawable?[]
                                    {
                                        SeedBox = new OsuTextBox
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            LengthLimit = 40,
                                            PlaceholderText = ClickerStrings.Text("Seed (optional, so others can check the draw)", "Seed (optional, damit andere die Auslosung nachprüfen können)"),
                                        },
                                        null,
                                        button(ClickerStrings.Pick("Draw teams", "Teams auslosen"), () => DrawTeams(SeedBox.Text)),
                                    }
                                }
                            },
                            drawText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },

                            // ---- roll
                            header(ClickerStrings.Pick("Roll (1 to 100)", "Würfeln (1 bis 100)")),
                            paragraph(ClickerStrings.Pick(
                                "Tick the players to roll for. These rolls are made on your PC and announced by you. \"!roll\" asks the server's chat bot to roll for yourself.",
                                "Hake die Spieler an, für die gewürfelt wird. Diese Würfe entstehen auf deinem PC und werden von dir angesagt. „!roll“ lässt den Chat-Bot des Servers für dich selbst würfeln.")),
                            rollPlayers = new FillFlowContainer<OsuCheckbox>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(8, 6),
                                Children = new Drawable[]
                                {
                                    sized(button(ClickerStrings.Pick("Roll for ticked players", "Für Angehakte würfeln"), () => RollSelected()), 200),
                                    sized(captainsButton = button(ClickerStrings.Pick("Tick the captains", "Kapitäne anhaken"), SelectCaptains), 170),
                                    sized(button(ClickerStrings.Pick("Send !roll", "!roll senden"), () => SendServerRoll()), 130),
                                }
                            },

                            // ---- ban / pick
                            header(ClickerStrings.Pick("Ban / pick board", "Bann- und Pick-Board")),
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(8, 6),
                                Children = new Drawable[]
                                {
                                    sized(sourceButton = button(string.Empty, CycleSource), 300),
                                    sized(firstTeamButton = button(string.Empty, () => SetFirstTeam(FirstTeam.Other())), 220),
                                    sized(button("-", () => SetBans(BansPerTeam - 1)), 36),
                                    new Container
                                    {
                                        AutoSizeAxes = Axes.X,
                                        Height = row_height,
                                        Child = bansText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.GetFont(size: 14),
                                        },
                                    },
                                    sized(button("+", () => SetBans(BansPerTeam + 1)), 36),
                                    sized(button(ClickerStrings.Pick("Start new board", "Neues Board starten"), StartBoard, colours.Green2.Darken(0.6f)), 180),
                                    sized(undoButton = button(ClickerStrings.Pick("Undo last step", "Letzten Schritt zurück"), () => UndoBoard(), colours.Gray5), 180),
                                }
                            },
                            turnText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                            },
                            boardRows = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 3),
                            },
                        }
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.RoomUpdated += onRoomUpdated;

            refreshPlayers();
            refreshBoardControls();
            refreshBoard();
        }

        // ------------------------------------------------------------------ room

        private void onRoomUpdated() => Scheduler.AddOnce(refreshPlayers);

        protected override void Update()
        {
            base.Update();

            // the tools are the host's; if the host role moves on while the panel is open, it closes
            if (State.Value == Visibility.Visible && (client.Room == null || !client.IsHost))
                Hide();
        }

        /// <summary>
        /// The players a team draw or roll can include: everybody in the room who is not spectating or refereeing.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyPlayer> Players
        {
            get
            {
                var room = client.Room;

                if (room == null)
                    return Array.Empty<BanchosucksLobbyPlayer>();

                return room.Users
                           .Where(u => u.State != MultiplayerUserState.Spectating && u.Role != MultiplayerRoomUserRole.Referee)
                           .Select(u => new BanchosucksLobbyPlayer(u.UserID, u.User?.Username ?? $"User {u.UserID}"))
                           .ToList();
            }
        }

        private void refreshPlayers()
        {
            var players = Players;

            selectedForRoll.IntersectWith(players.Select(p => p.UserId));

            rollPlayers.Clear();

            foreach (var player in players)
            {
                var checkbox = new OsuCheckbox
                {
                    LabelText = player.Name,
                    Current = { Value = selectedForRoll.Contains(player.UserId) },
                };

                int id = player.UserId;

                checkbox.Current.BindValueChanged(ticked =>
                {
                    if (ticked.NewValue)
                        selectedForRoll.Add(id);
                    else
                        selectedForRoll.Remove(id);
                });

                rollPlayers.Add(checkbox);
            }

            refreshDraw();
        }

        // ------------------------------------------------------------------ teams

        /// <summary>
        /// Draws two teams from the players in the room and announces them. An empty seed gets a random one, which is
        /// announced as well.
        /// </summary>
        public bool DrawTeams(string? seed = null)
        {
            if (!hostCheck())
                return false;

            var players = Players;

            if (players.Count < 2)
            {
                setNote(ClickerStrings.Pick("A team draw needs at least two players in the room.", "Für eine Auslosung müssen mindestens zwei Spieler im Raum sein."));
                return false;
            }

            string usedSeed = string.IsNullOrWhiteSpace(seed) ? BanchosucksLobbyRandom.NewSeed() : seed.Trim();
            var result = BanchosucksLobbyDraw.DrawTeams(players, usedSeed);

            bool teamVersus = client.Room?.MatchState is TeamVersusRoomState;

            if (!announce(BanchosucksLobbyToolsText.TeamDraw(result, teamVersus)))
                return false;

            LastDraw = result;

            // The game lets everybody change only their own team. So the host's client puts the host into the drawn
            // team, exactly like a click on the own team colour does, and the others follow the announcement.
            if (teamVersus && client.LocalUser is MultiplayerRoomUser local && local.MatchState is TeamVersusUserState state
                && result.TeamOf(local.UserID) is BanchosucksLobbyTeam ownTeam && state.TeamID != (int)ownTeam)
                client.SendMatchRequest(new ChangeTeamRequest { TeamID = (int)ownTeam }).FireAndForget();

            refreshDraw();
            return true;
        }

        private void refreshDraw()
        {
            drawText.Clear();
            captainsButton.Enabled.Value = LastDraw != null;

            if (LastDraw == null)
                return;

            drawText.AddParagraph($"Seed: {LastDraw.Seed}");
            drawText.AddParagraph($"{BanchosucksLobbyToolsText.TeamName(BanchosucksLobbyTeam.Red)}: {names(LastDraw.Red)}", t => t.Colour = colours.Red1);
            drawText.AddParagraph($"{BanchosucksLobbyToolsText.TeamName(BanchosucksLobbyTeam.Blue)}: {names(LastDraw.Blue)}", t => t.Colour = colours.Blue1);

            var waiting = NotOnDrawnTeam;

            if (waiting.Count > 0)
            {
                drawText.AddParagraph(ClickerStrings.Pick(
                    $"Not on their team yet: {names(waiting)}",
                    $"Noch nicht im ausgelosten Team: {names(waiting)}"), t => t.Colour = colours.Yellow);
            }

            static string names(IEnumerable<BanchosucksLobbyPlayer> players)
            {
                string joined = string.Join(", ", players.Select(p => p.Name));
                return joined.Length == 0 ? "-" : joined;
            }
        }

        /// <summary>
        /// In team versus mode: the players of the last draw who are in the room but on the other team.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyPlayer> NotOnDrawnTeam
        {
            get
            {
                var room = client.Room;

                if (LastDraw == null || room?.MatchState is not TeamVersusRoomState)
                    return Array.Empty<BanchosucksLobbyPlayer>();

                var waiting = new List<BanchosucksLobbyPlayer>();

                foreach (var user in room.Users)
                {
                    if (LastDraw.TeamOf(user.UserID) is BanchosucksLobbyTeam drawn && user.MatchState is TeamVersusUserState state && state.TeamID != (int)drawn)
                        waiting.Add(new BanchosucksLobbyPlayer(user.UserID, user.User?.Username ?? $"User {user.UserID}"));
                }

                return waiting;
            }
        }

        // ------------------------------------------------------------------ roll

        /// <summary>
        /// The user ids ticked for the next roll.
        /// </summary>
        public IReadOnlyCollection<int> SelectedForRoll => selectedForRoll;

        public void SelectForRoll(IEnumerable<int> userIds)
        {
            selectedForRoll.Clear();
            selectedForRoll.UnionWith(userIds);
            refreshPlayers();
        }

        /// <summary>
        /// Ticks the first drawn player of each team of the last draw.
        /// </summary>
        public void SelectCaptains()
        {
            if (LastDraw == null)
                return;

            SelectForRoll(new[] { LastDraw.Red, LastDraw.Blue }.Where(team => team.Count > 0).Select(team => team[0].UserId));
        }

        /// <summary>
        /// Rolls 1 to 100 on this PC for every ticked player and announces the numbers.
        /// </summary>
        public bool RollSelected()
        {
            if (!hostCheck())
                return false;

            var chosen = Players.Where(p => selectedForRoll.Contains(p.UserId)).ToList();

            if (chosen.Count == 0)
            {
                setNote(ClickerStrings.Pick("Tick at least one player to roll for.", "Hake mindestens einen Spieler an, für den gewürfelt wird."));
                return false;
            }

            return announce(BanchosucksLobbyToolsText.Roll(BanchosucksLobbyDraw.Roll(chosen, RollSource)));
        }

        /// <summary>
        /// Writes "!roll" into the room chat, the command of the server's chat bot. The bot rolls for whoever wrote it,
        /// so this is the host's own roll; whether the bot answers in room chats is up to the server.
        /// </summary>
        public bool SendServerRoll() => hostCheck() && announce(new[] { "!roll" });

        // ------------------------------------------------------------------ ban / pick

        public void SetBans(int bansPerTeam)
        {
            BansPerTeam = Math.Clamp(bansPerTeam, 0, BanchosucksBanPickBoard.MAX_BANS_PER_TEAM);
            refreshBoardControls();
        }

        public void SetFirstTeam(BanchosucksLobbyTeam team)
        {
            FirstTeam = team;
            refreshBoardControls();
        }

        /// <summary>
        /// Chooses where the next board takes its maps from: null for the room's queue, or a lobby template.
        /// </summary>
        public void SetSource(BanchosucksLobbyTemplate? template)
        {
            BoardTemplate = template;
            refreshBoardControls();
        }

        /// <summary>
        /// Steps through the possible sources: the room's queue, then every saved lobby template.
        /// </summary>
        public void CycleSource()
        {
            var available = templates.Store.Templates;

            if (available.Count == 0)
            {
                SetSource(null);
                return;
            }

            int index = BoardTemplate == null ? -1 : available.ToList().FindIndex(t => t.Name == BoardTemplate.Name);
            SetSource(index + 1 < available.Count ? available[index + 1] : null);
        }

        private void refreshBoardControls()
        {
            sourceButton.Text = BoardTemplate == null
                ? ClickerStrings.Pick("Maps: the room's queue", "Maps: Warteschlange des Raums")
                : ClickerStrings.Pick($"Maps: template \"{BoardTemplate.Name}\"", $"Maps: Vorlage „{BoardTemplate.Name}“");

            firstTeamButton.Text = ClickerStrings.Pick(
                $"First ban: {BanchosucksLobbyToolsText.TeamName(FirstTeam)}",
                $"Erster Bann: {BanchosucksLobbyToolsText.TeamName(FirstTeam)}");

            bansText.Text = ClickerStrings.Pick($"Bans per team: {BansPerTeam}", $"Banns je Team: {BansPerTeam}");
        }

        /// <summary>
        /// Starts a fresh board from the chosen source with the chosen rules. A running board is thrown away.
        /// </summary>
        public void StartBoard()
        {
            if (!hostCheck() || startingBoard)
                return;

            if (BoardTemplate != null)
            {
                createBoard(BoardTemplate.Playlist.Select(i => withMods(i.DisplayTitle, i.RequiredMods.Select(m => m.Acronym))).ToList());
                return;
            }

            var room = client.Room;

            if (room == null)
                return;

            var queue = room.GetUpcomingItems().ToList();
            int[] ids = queue.Select(i => i.BeatmapID).Distinct().ToArray();

            startingBoard = true;

            // the queue only knows beatmap ids; the names come from the lookup the playlist display uses as well
            templates.ResolveTitles(ids, titles =>
            {
                startingBoard = false;
                createBoard(queue.Select(i => withMods(titles.TryGetValue(i.BeatmapID, out string? title) && title.Length > 0 ? title : $"Beatmap #{i.BeatmapID}",
                    i.RequiredMods.Select(m => m.Acronym))).ToList());
            });

            static string withMods(string title, IEnumerable<string> acronyms)
            {
                string mods = string.Concat(acronyms);
                return mods.Length == 0 ? title : $"{title} +{mods}";
            }
        }

        private void createBoard(IReadOnlyList<string> maps)
        {
            if (maps.Count < 2)
            {
                setNote(ClickerStrings.Pick("A board needs at least two maps.", "Ein Board braucht mindestens zwei Maps."));
                return;
            }

            Board = new BanchosucksBanPickBoard(maps, BansPerTeam, FirstTeam);
            setNote(string.Empty);

            announce(new[]
            {
                ClickerStrings.Pick(
                    $"Ban/pick board: {maps.Count} maps, {Board.BansPerTeam} ban(s) per team, {BanchosucksLobbyToolsText.TeamName(Board.FirstTeam)} bans first.",
                    $"Bann-/Pick-Board: {maps.Count} Maps, {Board.BansPerTeam} Bann(s) je Team, {BanchosucksLobbyToolsText.TeamName(Board.FirstTeam)} bannt zuerst.")
            });

            refreshBoard();
        }

        /// <summary>
        /// Lets the team whose turn it is ban or pick the given map of the board and announces it.
        /// </summary>
        public bool Act(int mapIndex)
        {
            if (Board == null || !hostCheck())
                return false;

            if (!Board.TryApply(mapIndex, out var entry))
                return false;

            var messages = new List<string> { BanchosucksLobbyToolsText.BanPick(Board, entry) };

            if (BanchosucksLobbyToolsText.Decider(Board) is string decider)
                messages.Add(decider);

            announce(messages);
            refreshBoard();
            return true;
        }

        /// <summary>
        /// Takes back the last ban or pick and announces that.
        /// </summary>
        public bool UndoBoard()
        {
            if (Board == null || !hostCheck())
                return false;

            if (Board.Undo() is not BanchosucksBanPickEntry entry)
                return false;

            announce(new[] { BanchosucksLobbyToolsText.Undone(Board, entry) });
            refreshBoard();
            return true;
        }

        private void refreshBoard()
        {
            boardRows.Clear();

            if (Board == null)
            {
                turnText.Text = string.Empty;
                undoButton.Enabled.Value = false;
                return;
            }

            var board = Board;
            var next = board.NextTurn;

            turnText.Text = BanchosucksLobbyToolsText.NextTurn(board);
            turnText.Colour = next == null ? Color4.White : teamColour(next.Value.Team);
            undoButton.Enabled.Value = board.History.Count > 0;

            for (int i = 0; i < board.Maps.Count; i++)
            {
                int index = i;
                var entry = board.EntryOf(index);

                Drawable action = new Container();

                if (entry == null && next != null)
                {
                    action = button(next.Value.Action == BanchosucksBanPickAction.Ban ? ClickerStrings.Pick("Ban", "Bannen") : ClickerStrings.Pick("Pick", "Picken"),
                        () => Act(index), teamColour(next.Value.Team).Darken(0.4f));
                }

                boardRows.Add(new BoardRow(index, entry)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = row_height,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 200),
                            new Dimension(GridSizeMode.Absolute, 90),
                        },
                        Content = new[]
                        {
                            new[]
                            {
                                new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = $"{index + 1}. {board.Maps[index]}",
                                    Font = OsuFont.GetFont(size: 14),
                                    Alpha = entry?.Action == BanchosucksBanPickAction.Ban ? 0.5f : 1,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = BanchosucksLobbyToolsText.State(board, index),
                                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                                    Colour = entry == null ? colours.Yellow : teamColour(entry.Value.Team),
                                },
                                action,
                            }
                        }
                    }
                });
            }
        }

        // ------------------------------------------------------------------ helpers

        private Color4 teamColour(BanchosucksLobbyTeam team) => team == BanchosucksLobbyTeam.Red ? colours.Red1 : colours.Blue1;

        private bool hostCheck()
        {
            if (client.Room != null && client.IsHost)
                return true;

            setNote(ClickerStrings.Pick("Only the host of the room can use these tools.", "Nur der Host des Raums kann dieses Werkzeug benutzen."));
            return false;
        }

        /// <summary>
        /// Posts the messages into the room chat as the host. Returns false, with a note for the host, when the room has no chat to post to.
        /// </summary>
        private bool announce(IEnumerable<string> messages)
        {
            foreach (string message in messages)
            {
                if (AnnounceOverride != null)
                {
                    if (!AnnounceOverride(message))
                        return false;

                    continue;
                }

                var room = client.Room;
                var channel = room == null ? null : channelManager?.JoinedChannels.FirstOrDefault(c => c.Type == ChannelType.Multiplayer && c.Id == room.ChannelID);

                if (channelManager == null || channel == null)
                {
                    setNote(ClickerStrings.Pick("The room chat is not available, so this could not be announced.", "Der Raum-Chat ist nicht verfügbar, deshalb konnte das nicht angesagt werden."));
                    return false;
                }

                channelManager.PostMessage(message, false, channel);
            }

            setNote(string.Empty);
            return true;
        }

        private void setNote(string text)
        {
            Note = text;
            noteText.Text = text;
        }

        private RoundedButton button(string text, Action action, Color4? colour = null)
        {
            var created = new RoundedButton
            {
                RelativeSizeAxes = Axes.X,
                Height = row_height,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Action = action,
            };

            if (colour != null)
                created.BackgroundColour = colour.Value;

            return created;
        }

        /// <summary>
        /// Gives a button made by <see cref="button"/> a fixed width, for rows that wrap.
        /// </summary>
        private static Drawable sized(Drawable drawable, float width) => new Container
        {
            Width = width,
            Height = row_height,
            Child = drawable,
        };

        private static Drawable header(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
            Margin = new MarginPadding { Top = 10 },
        };

        private static Drawable paragraph(string text) => new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = text,
        };

        protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);

        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        // the panel covers part of the room; clicks and scrolling on it must not reach what is below
        protected override bool OnClick(ClickEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.RoomUpdated -= onRoomUpdated;
        }

        /// <summary>
        /// One map on the board.
        /// </summary>
        public partial class BoardRow : Container
        {
            public readonly int MapIndex;
            public readonly BanchosucksBanPickEntry? Entry;

            public BoardRow(int mapIndex, BanchosucksBanPickEntry? entry)
            {
                MapIndex = mapIndex;
                Entry = entry;
            }
        }
    }
}

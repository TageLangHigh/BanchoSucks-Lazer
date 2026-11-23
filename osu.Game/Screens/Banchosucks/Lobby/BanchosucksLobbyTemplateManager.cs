// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.OnlinePlay;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: saves and loads lobby templates for the multiplayer room screen it lives in.
    /// </summary>
    /// <remarks>
    /// It only reads the multiplayer client's state and changes the room through the calls the game's own room
    /// settings and song select use: <see cref="MultiplayerClient.ChangeSettings(osu.Game.Online.Multiplayer.MultiplayerRoomSettings)"/>
    /// and <see cref="MultiplayerClient.AddPlaylistItem"/>, one after the other. Nothing of the client is bound.
    /// Whether the server has a map is asked through the beatmap lookup the playlist display already uses.
    /// The component sits directly in the room screen (not in the settings overlay, which stops updating while
    /// hidden), so adding maps goes on after the overlay closed.
    /// </remarks>
    public partial class BanchosucksLobbyTemplateManager : Component
    {
        private readonly Room room;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private BeatmapLookupCache beatmapLookup { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OngoingOperationTracker? operationTracker { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public BanchosucksLobbyTemplateStore Store { get; private set; } = null!;

        /// <summary>
        /// Replaces the server lookup (tests).
        /// </summary>
        internal Func<int[], Task<APIBeatmap?[]>>? LookupOverride;

        /// <summary>
        /// Fired on the update thread when <see cref="Status"/>, <see cref="Busy"/> or <see cref="PendingCount"/> changed.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Whether a template is being applied to the room right now.
        /// </summary>
        public bool Busy { get; private set; }

        /// <summary>
        /// What the last save or load did, in the player's language.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyTemplateText.Line> Status { get; private set; } = Array.Empty<BanchosucksLobbyTemplateText.Line>();

        /// <summary>
        /// Maps of a template loaded for a room that is not created yet; they are added once it exists.
        /// </summary>
        public int PendingCount => pending.Count;

        private readonly List<BanchosucksLobbyTemplateItem> pending = new List<BanchosucksLobbyTemplateItem>();
        private BanchosucksLobbyTemplatePlan? pendingPlan;

        /// <summary>
        /// What the server answered for beatmap ids so far (checksum for adding, names for the texts).
        /// </summary>
        private readonly Dictionary<int, APIBeatmap> known = new Dictionary<int, APIBeatmap>();

        private IDisposable? operation;

        public BanchosucksLobbyTemplateManager(Room room)
        {
            this.room = room;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Store = new BanchosucksLobbyTemplateStore(storage);
        }

        protected override void Update()
        {
            base.Update();

            // a template loaded before the room existed: the rest of its maps follows as soon as the room is there
            // and the creation itself has finished
            if (pending.Count > 0 && pendingPlan != null && !Busy && client.Room != null && operationTracker?.InProgress.Value != true)
            {
                var plan = pendingPlan;
                var items = pending.ToList();

                pending.Clear();
                pendingPlan = null;

                // the first map of the template went in with the room itself, hence one map is already counted as added
                if (client.IsHost && beginOperation())
                    addItems(plan, items, 0, 1, new List<(BanchosucksLobbyTemplateItem, string)>());
                else
                    Changed?.Invoke();
            }
        }

        #region Saving

        /// <summary>
        /// Saves the room's current setup under <paramref name="name"/>: the given settings and the maps waiting in the
        /// queue (for a room that is not created yet, the picked map and what a loaded template still wants to add).
        /// </summary>
        /// <param name="name">Name of the template; one of the same name is replaced.</param>
        /// <param name="roomNamePattern">The room name, possibly with placeholders.</param>
        /// <param name="settings">The settings as the form shows them.</param>
        /// <param name="done">Called on the update thread with whether the template is on disk.</param>
        public void Save(string name, string roomNamePattern, BanchosucksLobbyTemplateSettings settings, Action<bool>? done = null)
        {
            var items = new List<BanchosucksLobbyTemplateItem>();
            int withoutOnlineId = 0;

            if (client.Room != null)
            {
                foreach (var item in client.Room.GetUpcomingItems())
                    items.Add(BanchosucksLobbyTemplateStore.ItemFrom(item, known.TryGetValue(item.BeatmapID, out var online) ? BanchosucksLobbyTemplateStore.TitleOf(online) : string.Empty));
            }
            else
            {
                foreach (var item in room.Playlist)
                {
                    if (item.Beatmap.OnlineID <= 0)
                    {
                        withoutOnlineId++;
                        continue;
                    }

                    items.Add(BanchosucksLobbyTemplateStore.ItemFrom(item));
                }

                items.AddRange(pending);
            }

            var template = new BanchosucksLobbyTemplate
            {
                Name = name,
                RoomName = roomNamePattern.Trim(),
                MatchType = settings.Type,
                QueueMode = settings.QueueMode,
                AutoStartSeconds = settings.AutoStartSeconds,
                AutoSkip = settings.AutoSkip,
                Playlist = items.Take(BanchosucksLobbyTemplateFile.MAX_PLAYLIST_LENGTH).ToList(),
                Saved = DateTimeOffset.Now,
            };

            // the names of the maps are only kept to show them to the host later; saving does not wait for them forever
            int[] nameless = template.Playlist.Where(i => string.IsNullOrEmpty(i.Title)).Select(i => i.BeatmapId).Distinct().ToArray();

            lookup(nameless).ContinueWith(task => Schedule(() =>
            {
                remember(task);

                foreach (var item in template.Playlist)
                {
                    if (string.IsNullOrEmpty(item.Title) && known.TryGetValue(item.BeatmapId, out var online))
                        item.Title = BanchosucksLobbyTemplateStore.TitleOf(online);
                }

                bool saved = Store.Save(template);

                var lines = new List<BanchosucksLobbyTemplateText.Line>();

                if (saved)
                {
                    lines.Add(new BanchosucksLobbyTemplateText.Line(ClickerStrings.Pick(
                        $"Template \"{template.Name}\" saved ({BanchosucksLobbyTemplateText.Summary(template)}). The password is not stored.",
                        $"Vorlage „{template.Name}“ gespeichert ({BanchosucksLobbyTemplateText.Summary(template)}). Das Passwort wird nicht gespeichert.")));

                    if (withoutOnlineId > 0)
                    {
                        lines.Add(new BanchosucksLobbyTemplateText.Line(ClickerStrings.Pick(
                            $"{withoutOnlineId} map(s) without an online id could not be saved.",
                            $"{withoutOnlineId} Map(s) ohne Online-ID konnten nicht gespeichert werden."), true));
                    }
                }
                else
                {
                    lines.Add(new BanchosucksLobbyTemplateText.Line(Store.ReadOnly
                        ? ClickerStrings.Pick("The template file was written by a newer client and is not changed by this one.", "Die Vorlagen-Datei stammt von einem neueren Client und wird von diesem nicht verändert.")
                        : ClickerStrings.Pick("The template could not be saved (name missing, file full or not writable).", "Die Vorlage konnte nicht gespeichert werden (Name fehlt, Datei voll oder nicht schreibbar)."), true));
                }

                setStatus(lines);
                done?.Invoke(saved);
            }));
        }

        public bool Delete(string name)
        {
            bool deleted = Store.Delete(name);

            if (deleted)
            {
                setStatus(new[]
                {
                    new BanchosucksLobbyTemplateText.Line(ClickerStrings.Pick($"Template \"{name}\" deleted.", $"Vorlage „{name}“ gelöscht."))
                });
            }

            return deleted;
        }

        #endregion

        #region Loading

        /// <summary>
        /// Asks the server which maps of the template it has and works out what loading it would change. Nothing is changed yet.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="current">The settings as the form shows them now.</param>
        /// <param name="done">Called on the update thread with the plan.</param>
        public void Prepare(BanchosucksLobbyTemplate template, BanchosucksLobbyTemplateSettings current, Action<BanchosucksLobbyTemplatePlan> done)
        {
            int[] ids = template.Playlist.Select(i => i.BeatmapId).Distinct().ToArray();

            lookup(ids).ContinueWith(task => Schedule(() =>
            {
                var available = remember(task);

                // name the maps the file has no name for, as far as the server knows them
                foreach (var item in template.Playlist)
                {
                    if (string.IsNullOrEmpty(item.Title) && known.TryGetValue(item.BeatmapId, out var online))
                        item.Title = BanchosucksLobbyTemplateStore.TitleOf(online);
                }

                // before the room exists there is no queue: the template's maps replace the one picked map
                var queue = client.Room?.GetUpcomingItems().Select(i => (i.BeatmapID, i.RulesetID)) ?? Enumerable.Empty<(int, int)>();

                done(BanchosucksLobbyTemplatePlan.Create(template, current, queue, available, api.LocalUser.Value.Username, DateTime.Today));
            }));
        }

        /// <summary>
        /// Applies a plan made by <see cref="Prepare"/>. In a joined room the settings are changed and the maps added through
        /// the multiplayer client; before the room exists the settings go into <paramref name="form"/>, the first map into
        /// the room's playlist, and the others wait until the room is created.
        /// </summary>
        /// <returns>False when nothing was started (not the host, another operation running).</returns>
        public bool Apply(BanchosucksLobbyTemplatePlan plan, IBanchosucksLobbyTemplateForm form)
        {
            if (Busy)
                return false;

            if (client.Room == null)
            {
                form.BanchosucksWriteSettings(plan.Settings);

                pending.Clear();
                pendingPlan = null;

                var lines = new List<BanchosucksLobbyTemplateText.Line>();

                if (plan.ToAdd.Count > 0)
                {
                    room.Playlist = [BanchosucksLobbyTemplateStore.ToPlaylistItem(plan.ToAdd[0], known[plan.ToAdd[0].BeatmapId])];

                    pending.AddRange(plan.ToAdd.Skip(1));
                    pendingPlan = plan;
                }

                lines.Add(new BanchosucksLobbyTemplateText.Line(pending.Count > 0
                    ? ClickerStrings.Pick(
                        $"Template \"{plan.Template.Name}\" loaded. {pending.Count} more {(pending.Count == 1 ? "map is" : "maps are")} added once the room is created.",
                        $"Vorlage „{plan.Template.Name}“ geladen. {pending.Count} weitere {(pending.Count == 1 ? "Map kommt" : "Maps kommen")} dazu, sobald der Raum erstellt ist.")
                    : ClickerStrings.Pick($"Template \"{plan.Template.Name}\" loaded.", $"Vorlage „{plan.Template.Name}“ geladen.")));

                if (plan.Unavailable.Count > 0)
                    lines.AddRange(BanchosucksLobbyTemplateText.Report(plan, 0, Array.Empty<(BanchosucksLobbyTemplateItem, string)>()).Where(l => l.Warning));

                setStatus(lines);
                return true;
            }

            if (!client.IsHost)
            {
                setStatus(new[]
                {
                    new BanchosucksLobbyTemplateText.Line(ClickerStrings.Pick("Only the host can load a template into the room.", "Nur der Host kann eine Vorlage in den Raum laden."), true)
                });
                return false;
            }

            if (!beginOperation())
            {
                setStatus(new[]
                {
                    new BanchosucksLobbyTemplateText.Line(ClickerStrings.Pick("Another change to the room is still running. Please try again in a moment.",
                        "Eine andere Änderung am Raum läuft noch. Bitte gleich noch einmal versuchen."), true)
                });
                return false;
            }

            if (plan.Changes.Count == 0)
            {
                addItems(plan, plan.ToAdd, 0, 0, new List<(BanchosucksLobbyTemplateItem, string)>());
                return true;
            }

            // the password is left out on purpose: the client then sends the one the room already has
            client.ChangeSettings(
                      name: plan.Settings.Name,
                      matchType: plan.Settings.Type,
                      queueMode: plan.Settings.QueueMode,
                      autoStartDuration: TimeSpan.FromSeconds(plan.Settings.AutoStartSeconds),
                      autoSkip: plan.Settings.AutoSkip)
                  .ContinueWith(task => Schedule(() =>
                  {
                      if (task.IsCompletedSuccessfully)
                          addItems(plan, plan.ToAdd, 0, 0, new List<(BanchosucksLobbyTemplateItem, string)>());
                      else
                          finish(BanchosucksLobbyTemplateText.Report(plan, 0, Array.Empty<(BanchosucksLobbyTemplateItem, string)>(), describe(task.Exception)));
                  }));

            return true;
        }

        /// <summary>
        /// Adds the maps one after the other, each through the client's ordinary "add item" call, waiting for the server's answer in between.
        /// </summary>
        private void addItems(BanchosucksLobbyTemplatePlan plan, IReadOnlyList<BanchosucksLobbyTemplateItem> items, int index, int added, List<(BanchosucksLobbyTemplateItem, string)> failed)
        {
            if (index >= items.Count)
            {
                finish(BanchosucksLobbyTemplateText.Report(plan, added, failed));
                return;
            }

            var item = items[index];

            if (client.Room == null)
            {
                // the room was left or closed in the meantime
                for (int i = index; i < items.Count; i++)
                    failed.Add((items[i], ClickerStrings.Pick("the room was left", "der Raum wurde verlassen")));

                finish(BanchosucksLobbyTemplateText.Report(plan, added, failed));
                return;
            }

            if (!known.TryGetValue(item.BeatmapId, out var online))
            {
                failed.Add((item, ClickerStrings.Pick("not found on the server", "auf dem Server nicht gefunden")));
                addItems(plan, items, index + 1, added, failed);
                return;
            }

            client.AddPlaylistItem(BanchosucksLobbyTemplateStore.ToMultiplayerItem(item, online)).ContinueWith(task => Schedule(() =>
            {
                if (task.IsCompletedSuccessfully)
                    added++;
                else
                    failed.Add((item, describe(task.Exception)));

                addItems(plan, items, index + 1, added, failed);
            }));
        }

        private bool beginOperation()
        {
            if (operationTracker?.InProgress.Value == true)
                return false;

            operation = operationTracker?.BeginOperation();
            Busy = true;
            Changed?.Invoke();
            return true;
        }

        private void finish(IReadOnlyList<BanchosucksLobbyTemplateText.Line> report)
        {
            operation?.Dispose();
            operation = null;
            Busy = false;

            setStatus(report);

            // the settings overlay may be closed by now, so the result also goes to the notifications
            notifications?.Post(new SimpleNotification { Text = string.Join(" ", report.Select(l => l.Text)) });
        }

        #endregion

        private void setStatus(IReadOnlyList<BanchosucksLobbyTemplateText.Line> lines)
        {
            Status = lines;
            Changed?.Invoke();
        }

        private Task<APIBeatmap?[]> lookup(int[] ids)
        {
            if (ids.Length == 0)
                return Task.FromResult(Array.Empty<APIBeatmap?>());

            return LookupOverride?.Invoke(ids) ?? beatmapLookup.GetBeatmapsAsync(ids);
        }

        /// <summary>
        /// Keeps what the server answered and returns the ids it knew.
        /// </summary>
        private HashSet<int> remember(Task<APIBeatmap?[]> task)
        {
            var found = new HashSet<int>();

            if (!task.IsCompletedSuccessfully)
                return found;

            foreach (var beatmap in task.GetResultSafely())
            {
                if (beatmap == null)
                    continue;

                known[beatmap.OnlineID] = beatmap;
                found.Add(beatmap.OnlineID);
            }

            return found;
        }

        private static string describe(Exception? exception)
        {
            if (exception is AggregateException aggregate)
                exception = aggregate.AsSingular();

            return exception?.GetHubExceptionMessage() ?? exception?.Message ?? ClickerStrings.Pick("unknown error", "unbekannter Fehler");
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            operation?.Dispose();
        }
    }

    /// <summary>
    /// Banchosucks: the part of the room settings form a lobby template reads and fills.
    /// Implemented by the settings of <c>MultiplayerMatchSettingsOverlay</c>.
    /// </summary>
    public interface IBanchosucksLobbyTemplateForm
    {
        BanchosucksLobbyTemplateSettings BanchosucksReadSettings();

        void BanchosucksWriteSettings(BanchosucksLobbyTemplateSettings settings);
    }
}

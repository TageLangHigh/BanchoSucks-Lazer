// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: the room settings a lobby template carries. The password is not part of it.
    /// </summary>
    public readonly record struct BanchosucksLobbyTemplateSettings(string Name, MatchType Type, QueueMode QueueMode, int AutoStartSeconds, bool AutoSkip);

    /// <summary>
    /// Banchosucks: one setting a template would change, for the preview shown before loading.
    /// </summary>
    public readonly record struct BanchosucksLobbyTemplateChange(BanchosucksLobbyTemplateField Field, string From, string To);

    public enum BanchosucksLobbyTemplateField
    {
        RoomName,
        MatchType,
        QueueMode,
        AutoStart,
        AutoSkip,
    }

    /// <summary>
    /// Banchosucks: what loading a lobby template does to a room, worked out before anything is sent, so the host can
    /// be shown it and asked once.
    /// </summary>
    public class BanchosucksLobbyTemplatePlan
    {
        public BanchosucksLobbyTemplate Template { get; }

        /// <summary>
        /// The settings after loading (room name already expanded).
        /// </summary>
        public BanchosucksLobbyTemplateSettings Settings { get; }

        /// <summary>
        /// The settings that differ from the current ones.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyTemplateChange> Changes { get; }

        /// <summary>
        /// Maps that will be added, in template order.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyTemplateItem> ToAdd { get; }

        /// <summary>
        /// Maps of the template that are already waiting in the room's queue and are therefore skipped.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyTemplateItem> AlreadyQueued { get; }

        /// <summary>
        /// Maps of the template the server does not have. They are reported to the host, never dropped silently.
        /// </summary>
        public IReadOnlyList<BanchosucksLobbyTemplateItem> Unavailable { get; }

        public bool ChangesAnything => Changes.Count > 0 || ToAdd.Count > 0;

        private BanchosucksLobbyTemplatePlan(BanchosucksLobbyTemplate template, BanchosucksLobbyTemplateSettings settings, IReadOnlyList<BanchosucksLobbyTemplateChange> changes,
                                             IReadOnlyList<BanchosucksLobbyTemplateItem> toAdd, IReadOnlyList<BanchosucksLobbyTemplateItem> alreadyQueued,
                                             IReadOnlyList<BanchosucksLobbyTemplateItem> unavailable)
        {
            Template = template;
            Settings = settings;
            Changes = changes;
            ToAdd = toAdd;
            AlreadyQueued = alreadyQueued;
            Unavailable = unavailable;
        }

        /// <summary>
        /// Works out what loading <paramref name="template"/> changes.
        /// </summary>
        /// <param name="template">The template to load.</param>
        /// <param name="current">The room's settings as they are now (the form when creating a room).</param>
        /// <param name="queue">Beatmap id and ruleset id of the maps waiting in the room's queue (played ones do not count).</param>
        /// <param name="availableOnServer">Online ids of the template's maps the server answered for.</param>
        /// <param name="username">The host's name, for the room name pattern.</param>
        /// <param name="today">Today's date, for the room name pattern.</param>
        public static BanchosucksLobbyTemplatePlan Create(BanchosucksLobbyTemplate template, BanchosucksLobbyTemplateSettings current, IEnumerable<(int BeatmapId, int RulesetId)> queue,
                                                          IReadOnlySet<int> availableOnServer, string username, DateTime today)
        {
            string roomName = ExpandRoomName(template.RoomName, username, today);

            // a template without a room name keeps the name the room has
            if (roomName.Length == 0)
                roomName = current.Name;

            var settings = new BanchosucksLobbyTemplateSettings(roomName, template.MatchType, template.QueueMode, Math.Max(0, template.AutoStartSeconds), template.AutoSkip);

            var changes = new List<BanchosucksLobbyTemplateChange>();

            if (settings.Name != current.Name)
                changes.Add(new BanchosucksLobbyTemplateChange(BanchosucksLobbyTemplateField.RoomName, current.Name, settings.Name));

            if (settings.Type != current.Type)
                changes.Add(new BanchosucksLobbyTemplateChange(BanchosucksLobbyTemplateField.MatchType, current.Type.ToString(), settings.Type.ToString()));

            if (settings.QueueMode != current.QueueMode)
                changes.Add(new BanchosucksLobbyTemplateChange(BanchosucksLobbyTemplateField.QueueMode, current.QueueMode.ToString(), settings.QueueMode.ToString()));

            if (settings.AutoStartSeconds != current.AutoStartSeconds)
            {
                changes.Add(new BanchosucksLobbyTemplateChange(BanchosucksLobbyTemplateField.AutoStart,
                    current.AutoStartSeconds.ToString(CultureInfo.InvariantCulture), settings.AutoStartSeconds.ToString(CultureInfo.InvariantCulture)));
            }

            if (settings.AutoSkip != current.AutoSkip)
                changes.Add(new BanchosucksLobbyTemplateChange(BanchosucksLobbyTemplateField.AutoSkip, current.AutoSkip.ToString(), settings.AutoSkip.ToString()));

            // every map waiting in the queue covers one equal entry of the template, so a map that is in the template twice
            // and in the queue once is still added once
            var waiting = queue.GroupBy(q => q).ToDictionary(g => g.Key, g => g.Count());

            var toAdd = new List<BanchosucksLobbyTemplateItem>();
            var alreadyQueued = new List<BanchosucksLobbyTemplateItem>();
            var unavailable = new List<BanchosucksLobbyTemplateItem>();

            foreach (var item in template.Playlist)
            {
                var key = (item.BeatmapId, item.RulesetId);

                if (waiting.TryGetValue(key, out int count) && count > 0)
                {
                    waiting[key] = count - 1;
                    alreadyQueued.Add(item);
                }
                else if (!availableOnServer.Contains(item.BeatmapId))
                    unavailable.Add(item);
                else
                    toAdd.Add(item);
            }

            return new BanchosucksLobbyTemplatePlan(template, settings, changes, toAdd, alreadyQueued, unavailable);
        }

        /// <summary>
        /// Fills the placeholders of a room name pattern: <c>{user}</c> (also <c>{name}</c>) is the host's name,
        /// <c>{date}</c> today's date as year-month-day. Anything else stays as written. The result is cut to the
        /// length the room settings accept.
        /// </summary>
        public static string ExpandRoomName(string? pattern, string username, DateTime today)
        {
            string name = (pattern ?? string.Empty)
                          .Replace("{user}", username, StringComparison.OrdinalIgnoreCase)
                          .Replace("{name}", username, StringComparison.OrdinalIgnoreCase)
                          .Replace("{date}", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                          .Trim();

            return name.Length > BanchosucksLobbyTemplateFile.MAX_ROOM_NAME_LENGTH ? name.Substring(0, BanchosucksLobbyTemplateFile.MAX_ROOM_NAME_LENGTH).Trim() : name;
        }
    }
}

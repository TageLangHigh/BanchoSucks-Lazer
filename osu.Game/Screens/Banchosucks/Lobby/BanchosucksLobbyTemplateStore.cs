// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: the lobby templates on disk (<c>&lt;game data&gt;/banchosucks/lobby-templates.json</c>, previous
    /// version in <c>lobby-templates.bak.json</c>) and the conversion between template entries and playlist items.
    /// </summary>
    public class BanchosucksLobbyTemplateStore
    {
        public const string FILE = "lobby-templates.json";
        public const string BACKUP_FILE = "lobby-templates.bak.json";

        private readonly Storage storage;

        private BanchosucksLobbyTemplateFile file;

        public BanchosucksLobbyTemplateStore(Storage gameStorage)
        {
            storage = gameStorage.GetStorageForDirectory("banchosucks");
            file = BanchosucksLobbyTemplateFile.Read(storage.GetFullPath(FILE)) ?? BanchosucksLobbyTemplateFile.Read(storage.GetFullPath(BACKUP_FILE)) ?? new BanchosucksLobbyTemplateFile();
        }

        /// <summary>
        /// Whether the file was written by a newer client; then it is shown but never changed.
        /// </summary>
        public bool ReadOnly => file.ReadOnly;

        public IReadOnlyList<BanchosucksLobbyTemplate> Templates => file.Templates;

        public BanchosucksLobbyTemplate? Find(string name) => file.Find(name);

        /// <summary>
        /// Stores a template under its name, replacing one of the same name. Returns whether it is on disk now.
        /// </summary>
        public bool Save(BanchosucksLobbyTemplate template)
        {
            if (ReadOnly)
                return false;

            var previous = file.Find(template.Name);

            if (!file.Upsert(template))
                return false;

            if (write())
                return true;

            // keep memory and disk the same
            file.Templates.Remove(template);
            if (previous != null)
                file.Templates.Add(previous);

            return false;
        }

        public bool Delete(string name)
        {
            if (ReadOnly)
                return false;

            var existing = file.Find(name);

            if (existing == null)
                return false;

            int index = file.Templates.IndexOf(existing);
            file.Templates.RemoveAt(index);

            if (write())
                return true;

            file.Templates.Insert(index, existing);
            return false;
        }

        private bool write()
        {
            try
            {
                file.Write(storage.GetFullPath(FILE, true), storage.GetFullPath(BACKUP_FILE, true));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Logger.Log($"Lobby templates could not be written ({e.Message})", LoggingTarget.Runtime, LogLevel.Important);
                return false;
            }
        }

        #region Conversion

        /// <summary>
        /// A template entry for a map of a joined room's queue.
        /// </summary>
        public static BanchosucksLobbyTemplateItem ItemFrom(MultiplayerPlaylistItem item, string title) => new BanchosucksLobbyTemplateItem
        {
            BeatmapId = item.BeatmapID,
            RulesetId = item.RulesetID,
            RequiredMods = item.RequiredMods.Select(ModFrom).ToList(),
            AllowedMods = item.AllowedMods.Select(ModFrom).ToList(),
            Freestyle = item.Freestyle,
            Title = title,
        };

        /// <summary>
        /// A template entry for a map picked for a room that is not created yet.
        /// </summary>
        public static BanchosucksLobbyTemplateItem ItemFrom(PlaylistItem item) => new BanchosucksLobbyTemplateItem
        {
            BeatmapId = item.Beatmap.OnlineID,
            RulesetId = item.RulesetID,
            RequiredMods = item.RequiredMods.Select(ModFrom).ToList(),
            AllowedMods = item.AllowedMods.Select(ModFrom).ToList(),
            Freestyle = item.Freestyle,
            Title = TitleOf(item.Beatmap),
        };

        /// <summary>
        /// The item sent through the multiplayer client's ordinary "add item" call.
        /// </summary>
        /// <param name="item">The template entry.</param>
        /// <param name="online">What the server answered for the map; its checksum is the one the server expects.</param>
        public static MultiplayerPlaylistItem ToMultiplayerItem(BanchosucksLobbyTemplateItem item, APIBeatmap online) => new MultiplayerPlaylistItem
        {
            BeatmapID = item.BeatmapId,
            BeatmapChecksum = online.Checksum,
            RulesetID = item.RulesetId,
            RequiredMods = item.RequiredMods.Select(ToApiMod).ToArray(),
            AllowedMods = item.AllowedMods.Select(ToApiMod).ToArray(),
            Freestyle = item.Freestyle,
            StarRating = online.StarRating,
        };

        /// <summary>
        /// The item put into the playlist of a room that is about to be created.
        /// </summary>
        public static PlaylistItem ToPlaylistItem(BanchosucksLobbyTemplateItem item, APIBeatmap online) => new PlaylistItem(online)
        {
            RulesetID = item.RulesetId,
            RequiredMods = item.RequiredMods.Select(ToApiMod).ToArray(),
            AllowedMods = item.AllowedMods.Select(ToApiMod).ToArray(),
            Freestyle = item.Freestyle,
        };

        // Mods keep the JSON shape osu! itself uses for them (acronym and settings), so they pass through Newtonsoft
        // like everywhere else in the client and through System.Text.Json only as part of our own file.
        public static BanchosucksLobbyTemplateMod ModFrom(APIMod mod)
            => JsonSerializer.Deserialize<BanchosucksLobbyTemplateMod>(JsonConvert.SerializeObject(mod)) ?? new BanchosucksLobbyTemplateMod { Acronym = mod.Acronym };

        public static APIMod ToApiMod(BanchosucksLobbyTemplateMod mod)
            => JsonConvert.DeserializeObject<APIMod>(JsonSerializer.Serialize(mod)) ?? new APIMod { Acronym = mod.Acronym };

        /// <summary>
        /// "Artist - Title [Difficulty]", or an empty text when the beatmap carries no metadata (only an id is known).
        /// </summary>
        public static string TitleOf(IBeatmapInfo? beatmap)
        {
            if (beatmap == null || string.IsNullOrEmpty(beatmap.Metadata.Title))
                return string.Empty;

            string difficulty = string.IsNullOrEmpty(beatmap.DifficultyName) ? string.Empty : $" [{beatmap.DifficultyName}]";
            string artist = string.IsNullOrEmpty(beatmap.Metadata.Artist) ? string.Empty : $"{beatmap.Metadata.Artist} - ";

            return $"{artist}{beatmap.Metadata.Title}{difficulty}";
        }

        #endregion
    }
}

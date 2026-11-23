// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using MatchType = osu.Game.Online.Rooms.MatchType;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: one mod of a playlist entry in a lobby template, in the same shape the API uses (acronym plus settings).
    /// </summary>
    public class BanchosucksLobbyTemplateMod
    {
        [JsonPropertyName("acronym")]
        public string Acronym { get; set; } = string.Empty;

        [JsonPropertyName("settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, JsonElement>? Settings { get; set; }
    }

    /// <summary>
    /// Banchosucks: one map of a lobby template.
    /// </summary>
    public class BanchosucksLobbyTemplateItem
    {
        /// <summary>
        /// Online id of the difficulty.
        /// </summary>
        [JsonPropertyName("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonPropertyName("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonPropertyName("required_mods")]
        public List<BanchosucksLobbyTemplateMod> RequiredMods { get; set; } = new List<BanchosucksLobbyTemplateMod>();

        [JsonPropertyName("allowed_mods")]
        public List<BanchosucksLobbyTemplateMod> AllowedMods { get; set; } = new List<BanchosucksLobbyTemplateMod>();

        [JsonPropertyName("freestyle")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Freestyle { get; set; }

        /// <summary>
        /// "Artist - Title [Difficulty]" as known when the template was saved. Only used to name the map to the player,
        /// for example when the server no longer has it.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        [JsonIgnore]
        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? $"Beatmap #{BeatmapId}" : Title;
    }

    /// <summary>
    /// Banchosucks: a saved multiplayer room setup. There is deliberately no field for the room password.
    /// </summary>
    public class BanchosucksLobbyTemplate
    {
        /// <summary>
        /// The name the host gave the template; unique in the file, compared without case.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The room name. May contain the placeholders of <see cref="BanchosucksLobbyTemplatePlan.ExpandRoomName"/>.
        /// </summary>
        [JsonPropertyName("room_name")]
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the <see cref="MatchType"/> value.
        /// </summary>
        [JsonPropertyName("match_type")]
        public string MatchTypeName { get; set; } = nameof(MatchType.HeadToHead);

        /// <summary>
        /// Name of the <see cref="QueueMode"/> value.
        /// </summary>
        [JsonPropertyName("queue_mode")]
        public string QueueModeName { get; set; } = nameof(QueueMode.HostOnly);

        [JsonPropertyName("auto_start_seconds")]
        public int AutoStartSeconds { get; set; }

        [JsonPropertyName("auto_skip")]
        public bool AutoSkip { get; set; }

        [JsonPropertyName("playlist")]
        public List<BanchosucksLobbyTemplateItem> Playlist { get; set; } = new List<BanchosucksLobbyTemplateItem>();

        [JsonPropertyName("saved")]
        public DateTimeOffset Saved { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>
        /// The match type; a name this client does not know (file from a newer client, edited by hand) falls back to head to head.
        /// </summary>
        [JsonIgnore]
        public MatchType MatchType
        {
            get => Enum.TryParse(MatchTypeName, true, out MatchType type) && BanchosucksLobbyTemplateFile.IsRoomMatchType(type) ? type : MatchType.HeadToHead;
            set => MatchTypeName = value.ToString();
        }

        /// <summary>
        /// The queue mode; an unknown name falls back to host only.
        /// </summary>
        [JsonIgnore]
        public QueueMode QueueMode
        {
            get => Enum.TryParse(QueueModeName, true, out QueueMode mode) && Enum.IsDefined(mode) ? mode : QueueMode.HostOnly;
            set => QueueModeName = value.ToString();
        }
    }

    /// <summary>
    /// Banchosucks: the document stored in <c>banchosucks/lobby-templates.json</c>. It never leaves the PC.
    /// </summary>
    public class BanchosucksLobbyTemplateFile
    {
        /// <summary>
        /// Format version this client writes. A file with a higher version is read but never written (see <see cref="ReadOnly"/>).
        /// </summary>
        public const int CURRENT_VERSION = 1;

        public const int MAX_TEMPLATES = 100;
        public const int MAX_NAME_LENGTH = 60;

        /// <summary>
        /// Longest room name the room settings accept.
        /// </summary>
        public const int MAX_ROOM_NAME_LENGTH = 100;

        /// <summary>
        /// Upper bound of maps in one template, so a damaged file cannot make the client queue thousands of maps.
        /// </summary>
        public const int MAX_PLAYLIST_LENGTH = 200;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CURRENT_VERSION;

        [JsonPropertyName("templates")]
        public List<BanchosucksLobbyTemplate> Templates { get; set; } = new List<BanchosucksLobbyTemplate>();

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>
        /// Whether the file comes from a newer client and must not be overwritten by this one.
        /// </summary>
        [JsonIgnore]
        public bool ReadOnly => Version > CURRENT_VERSION;

        /// <summary>
        /// Whether a match type is one a player can pick in the room settings (not playlists, matchmaking or ranked play).
        /// </summary>
        public static bool IsRoomMatchType(MatchType type) => type == MatchType.HeadToHead || type == MatchType.TeamVersus;

        public BanchosucksLobbyTemplate? Find(string name)
            => Templates.FirstOrDefault(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Adds a template, replacing one of the same name. Returns false when the name is empty or the file is full.
        /// </summary>
        public bool Upsert(BanchosucksLobbyTemplate template)
        {
            template.Name = CleanName(template.Name);

            if (template.Name.Length == 0)
                return false;

            var existing = Find(template.Name);

            if (existing != null)
            {
                Templates[Templates.IndexOf(existing)] = template;
                return true;
            }

            if (Templates.Count >= MAX_TEMPLATES)
                return false;

            Templates.Add(template);
            return true;
        }

        public bool Remove(string name)
        {
            var existing = Find(name);
            return existing != null && Templates.Remove(existing);
        }

        public static string CleanName(string? name)
        {
            string cleaned = (name ?? string.Empty).Trim();
            return cleaned.Length > MAX_NAME_LENGTH ? cleaned.Substring(0, MAX_NAME_LENGTH).Trim() : cleaned;
        }

        // indented, so the file can be read and edited by hand
        private static readonly JsonSerializerOptions serialiser_options = new JsonSerializerOptions { WriteIndented = true };

        public string Serialise() => JsonSerializer.Serialize(this, serialiser_options);

        /// <summary>
        /// Reads a document. Returns null for an empty or damaged one; templates and maps that make no sense are dropped.
        /// </summary>
        public static BanchosucksLobbyTemplateFile? Deserialise(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var file = JsonSerializer.Deserialize<BanchosucksLobbyTemplateFile>(json);

                if (file == null || file.Version < 1)
                    return null;

                var templates = new List<BanchosucksLobbyTemplate>();

                // tolerate hand-edited or half-written content: null lists, nameless or doubled templates, maps without an id
                foreach (var template in file.Templates ?? new List<BanchosucksLobbyTemplate>())
                {
                    if (template == null)
                        continue;

                    template.Name = CleanName(template.Name);

                    if (template.Name.Length == 0 || templates.Any(t => string.Equals(t.Name, template.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    template.RoomName ??= string.Empty;
                    template.MatchTypeName ??= string.Empty;
                    template.QueueModeName ??= string.Empty;
                    template.AutoStartSeconds = Math.Clamp(template.AutoStartSeconds, 0, 3600);

                    template.Playlist = (template.Playlist ?? new List<BanchosucksLobbyTemplateItem>())
                                        .Where(i => i != null && i.BeatmapId > 0 && i.RulesetId >= 0)
                                        .Take(MAX_PLAYLIST_LENGTH)
                                        .ToList();

                    foreach (var item in template.Playlist)
                    {
                        item.Title ??= string.Empty;
                        item.RequiredMods = cleanMods(item.RequiredMods);
                        item.AllowedMods = cleanMods(item.AllowedMods);
                    }

                    templates.Add(template);

                    if (templates.Count == MAX_TEMPLATES)
                        break;
                }

                file.Templates = templates;
                return file;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static List<BanchosucksLobbyTemplateMod> cleanMods(List<BanchosucksLobbyTemplateMod>? mods)
            => (mods ?? new List<BanchosucksLobbyTemplateMod>()).Where(m => m != null && !string.IsNullOrWhiteSpace(m.Acronym)).ToList();

        /// <summary>
        /// Reads the document from <paramref name="path"/>, or null when the file is missing or unreadable.
        /// </summary>
        public static BanchosucksLobbyTemplateFile? Read(string path)
        {
            try
            {
                return File.Exists(path) ? Deserialise(File.ReadAllText(path)) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the document atomically, the same way the activity log does: a new file next to the old one, then a
        /// swap that keeps the previous version as backup. A crash leaves either the old or the new file, never half of one.
        /// </summary>
        public void Write(string path, string backupPath)
        {
            string temp = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, Serialise());

            if (File.Exists(path))
                File.Replace(temp, path, backupPath);
            else
                File.Move(temp, path);
        }
    }
}

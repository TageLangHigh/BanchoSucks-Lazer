// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Banchosucks: what a skin folder looked like when it was measured: time of the newest file (UTC ticks),
    /// size of all files together, number of files. If any of the three differs, the folder changed.
    /// </summary>
    public readonly record struct BanchosucksSkinFingerprint(long NewestWriteTicks, long TotalSize, int FileCount);

    /// <summary>
    /// Banchosucks: the bookkeeping of the skin sync from osu!stable (F43): which folders of stable's Skins directory
    /// were imported and what they looked like then, so that nothing is imported twice. Kept as a small JSON file
    /// in the game data folder; it only ever holds folder names, the three numbers of the fingerprint and the ID of
    /// the imported skin.
    /// </summary>
    public class BanchosucksSkinSyncRecord
    {
        public const string FILENAME = "skin-sync.json";
        public const int VERSION = 1;

        public class Entry
        {
            public BanchosucksSkinFingerprint Fingerprint;

            /// <summary>
            /// The skin the folder was imported as.
            /// </summary>
            public Guid? SkinID;
        }

        /// <summary>
        /// The imported folders by folder name.
        /// </summary>
        public readonly Dictionary<string, Entry> Folders = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The folders that have to be imported: those never imported and those whose files changed since.
        /// Folders without files are no skins and are left out; folders that vanished from stable are simply not
        /// mentioned (their skins stay in the game).
        /// </summary>
        public List<string> FoldersToImport(IReadOnlyDictionary<string, BanchosucksSkinFingerprint> found)
            => found.Where(f => f.Value.FileCount > 0 && (!Folders.TryGetValue(f.Key, out var known) || known.Fingerprint != f.Value))
                    .Select(f => f.Key)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

        public void MarkImported(string folder, BanchosucksSkinFingerprint fingerprint, Guid? skinId)
            => Folders[folder] = new Entry { Fingerprint = fingerprint, SkinID = skinId };

        /// <summary>
        /// Measures a folder without opening any file (names, sizes and times only).
        /// </summary>
        public static BanchosucksSkinFingerprint Measure(string directory)
        {
            long newest = 0;
            long size = 0;
            int count = 0;

            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                newest = Math.Max(newest, file.LastWriteTimeUtc.Ticks);
                size += file.Length;
                count++;
            }

            return new BanchosucksSkinFingerprint(newest, size, count);
        }

        /// <summary>
        /// Measures every folder directly inside <paramref name="skinsDirectory"/>. A folder that cannot be read is skipped.
        /// </summary>
        public static Dictionary<string, BanchosucksSkinFingerprint> MeasureAll(string skinsDirectory)
        {
            var found = new Dictionary<string, BanchosucksSkinFingerprint>(StringComparer.OrdinalIgnoreCase);

            foreach (string directory in Directory.GetDirectories(skinsDirectory))
            {
                try
                {
                    found[Path.GetFileName(directory)] = Measure(directory);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // skipped; it is looked at again on the next check
                }
            }

            return found;
        }

        public string Serialise()
        {
            var folders = new JObject();

            foreach (var (name, entry) in Folders.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            {
                var item = new JObject
                {
                    ["newest"] = entry.Fingerprint.NewestWriteTicks,
                    ["size"] = entry.Fingerprint.TotalSize,
                    ["files"] = entry.Fingerprint.FileCount,
                };

                if (entry.SkinID is Guid id)
                    item["skin"] = id.ToString();

                folders[name] = item;
            }

            return new JObject
            {
                ["format"] = "banchosucks-skin-sync",
                ["version"] = VERSION,
                ["folders"] = folders,
            }.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Reads a record. A broken file gives an empty record, which only means everything is looked at once more
        /// (the importer itself recognises skins it already has).
        /// </summary>
        public static BanchosucksSkinSyncRecord Deserialise(string? json)
        {
            var record = new BanchosucksSkinSyncRecord();

            if (string.IsNullOrWhiteSpace(json))
                return record;

            try
            {
                if (BanchosucksJson.Parse(json) is not JObject root || root["folders"] is not JObject folders)
                    return record;

                foreach (var property in folders.Properties())
                {
                    if (property.Value is not JObject item
                        || item["newest"] is not JValue { Type: JTokenType.Integer, Value: long newest }
                        || item["size"] is not JValue { Type: JTokenType.Integer, Value: long size }
                        || BanchosucksJson.IntOf(item["files"]) is not int files)
                        continue;

                    record.Folders[property.Name] = new Entry
                    {
                        Fingerprint = new BanchosucksSkinFingerprint(newest, size, files),
                        SkinID = Guid.TryParse(BanchosucksJson.StringOf(item["skin"]), out var id) ? id : null,
                    };
                }
            }
            catch (JsonException)
            {
                record.Folders.Clear();
            }

            return record;
        }

        public static BanchosucksSkinSyncRecord Load(Storage storage)
        {
            try
            {
                var directory = storage.GetStorageForDirectory(BanchosucksKeyProfiles.DIRECTORY);

                if (!directory.Exists(FILENAME))
                    return new BanchosucksSkinSyncRecord();

                using (var stream = directory.GetStream(FILENAME))
                using (var reader = new StreamReader(stream))
                    return Deserialise(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks skin sync record could not be loaded: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return new BanchosucksSkinSyncRecord();
            }
        }

        public void Store(Storage storage)
        {
            using (var stream = storage.GetStorageForDirectory(BanchosucksKeyProfiles.DIRECTORY).CreateFileSafely(FILENAME))
            using (var writer = new StreamWriter(stream))
                writer.Write(Serialise());
        }
    }
}

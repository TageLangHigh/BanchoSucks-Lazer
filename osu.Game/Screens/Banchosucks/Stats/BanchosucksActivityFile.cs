// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.Game.Screens.Banchosucks.Stats
{
    /// <summary>
    /// Banchosucks: one finished play in the local activity log.
    /// </summary>
    /// <remarks>
    /// The log never leaves the PC. A later client version may add fields: unknown ones are kept in <see cref="Extra"/>
    /// and written back unchanged, so an older client does not strip them.
    /// </remarks>
    public class BanchosucksActivityEntry
    {
        /// <summary>
        /// When the play ended, in the PC's local time together with the UTC offset that applied at that moment.
        /// The day a play belongs to is read from this value as written, so a later change of the PC's time zone
        /// does not move old plays to another day.
        /// </summary>
        [JsonPropertyName("at")]
        public DateTimeOffset At { get; set; }

        /// <summary>
        /// Seconds of active gameplay (breaks, the lead-in, pauses and the fail animation do not count).
        /// </summary>
        [JsonPropertyName("seconds")]
        public double Seconds { get; set; }

        /// <summary>
        /// Short name of the ruleset ("osu", "taiko", "fruits", "mania").
        /// </summary>
        [JsonPropertyName("ruleset")]
        public string Ruleset { get; set; } = string.Empty;

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        /// <summary>
        /// Accuracy when the play ended, 0 to 1.
        /// </summary>
        [JsonPropertyName("accuracy")]
        public double Accuracy { get; set; }

        /// <summary>
        /// Set for entries rebuilt from scores saved before the log existed: only passed plays are known there and
        /// the duration is the beatmap's length divided by the rate of the mods.
        /// </summary>
        [JsonPropertyName("estimated")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Estimated { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>
        /// The local calendar day the play counts for (the day it ended on).
        /// </summary>
        [JsonIgnore]
        public DateOnly Day => DateOnly.FromDateTime(At.DateTime);

        [JsonIgnore]
        public DateTimeOffset Start => At.AddSeconds(-Math.Max(0, Seconds));
    }

    /// <summary>
    /// Banchosucks: the document stored in <c>banchosucks/activity-log.json</c>, shared by the daily goal and the personal statistics.
    /// </summary>
    public class BanchosucksActivityFile
    {
        /// <summary>
        /// Format version this client writes. A file with a higher version is read but never written (see <see cref="ReadOnly"/>).
        /// </summary>
        public const int CURRENT_VERSION = 1;

        /// <summary>
        /// Days kept in the log; older entries are dropped when the log is written.
        /// </summary>
        public const int KEEP_DAYS = 400;

        /// <summary>
        /// Upper bound of entries, whatever their age, so the file cannot grow without limit.
        /// </summary>
        public const int MAX_ENTRIES = 100_000;

        /// <summary>
        /// A play that was not passed and had less active time than this is not logged (retry spam at the start of a map).
        /// </summary>
        public const double MIN_SECONDS = 10;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CURRENT_VERSION;

        /// <summary>
        /// When the log was started on this PC. Plays before it are only known from saved scores (see <see cref="BanchosucksActivityEntry.Estimated"/>).
        /// </summary>
        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }

        /// <summary>
        /// Whether the saved scores from before <see cref="Created"/> were already taken over.
        /// </summary>
        [JsonPropertyName("backfilled")]
        public bool Backfilled { get; set; }

        /// <summary>
        /// Finished plays, oldest first.
        /// </summary>
        [JsonPropertyName("entries")]
        public List<BanchosucksActivityEntry> Entries { get; set; } = new List<BanchosucksActivityEntry>();

        /// <summary>
        /// Local days on which the daily goal was reached. A day is added the moment the goal is met and stays, so
        /// changing the goal later does not rewrite the streak.
        /// </summary>
        [JsonPropertyName("goal_days")]
        public List<DateOnly> GoalDays { get; set; } = new List<DateOnly>();

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>
        /// Whether the file comes from a newer client and must not be overwritten by this one.
        /// </summary>
        [JsonIgnore]
        public bool ReadOnly => Version > CURRENT_VERSION;

        /// <summary>
        /// Whether a play is worth an entry.
        /// </summary>
        public static bool ShouldLog(double seconds, bool passed) => passed || seconds >= MIN_SECONDS;

        /// <summary>
        /// Adds an entry at its place in time (entries stay sorted by <see cref="BanchosucksActivityEntry.At"/>).
        /// </summary>
        public void Add(BanchosucksActivityEntry entry)
        {
            int index = Entries.Count;
            while (index > 0 && Entries[index - 1].At > entry.At)
                index--;

            Entries.Insert(index, entry);
        }

        /// <summary>
        /// Drops everything older than <see cref="KEEP_DAYS"/> days before <paramref name="today"/> and, beyond
        /// <see cref="MAX_ENTRIES"/>, the oldest entries. Returns whether anything was removed.
        /// </summary>
        public bool Prune(DateOnly today)
        {
            DateOnly oldest = today.AddDays(-(KEEP_DAYS - 1));

            int removed = Entries.RemoveAll(e => e.Day < oldest);
            removed += GoalDays.RemoveAll(d => d < oldest);

            if (Entries.Count > MAX_ENTRIES)
            {
                removed += Entries.Count - MAX_ENTRIES;
                Entries.RemoveRange(0, Entries.Count - MAX_ENTRIES);
            }

            return removed > 0;
        }

        /// <summary>
        /// Marks <paramref name="day"/> as a day on which the goal was met. Returns false when it already was.
        /// </summary>
        public bool MarkGoalDay(DateOnly day)
        {
            if (GoalDays.Contains(day))
                return false;

            GoalDays.Add(day);
            GoalDays.Sort();
            return true;
        }

        /// <summary>
        /// A copy that can be serialised on another thread while this instance keeps changing (entries are never modified after they were added).
        /// </summary>
        public BanchosucksActivityFile Snapshot() => new BanchosucksActivityFile
        {
            Version = Version,
            Created = Created,
            Backfilled = Backfilled,
            Entries = new List<BanchosucksActivityEntry>(Entries),
            GoalDays = new List<DateOnly>(GoalDays),
            Extra = Extra,
        };

        public string Serialise() => JsonSerializer.Serialize(this);

        /// <summary>
        /// Reads a log. Returns null for an empty or damaged document; entries without a usable time are dropped.
        /// </summary>
        public static BanchosucksActivityFile? Deserialise(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var file = JsonSerializer.Deserialize<BanchosucksActivityFile>(json);

                if (file == null || file.Version < 1)
                    return null;

                // tolerate hand-edited or half-written content: null lists, null or timeless entries, unsorted order
                file.Entries = (file.Entries ?? new List<BanchosucksActivityEntry>())
                               .Where(e => e != null && e.At != default && !double.IsNaN(e.Seconds) && !double.IsInfinity(e.Seconds))
                               .OrderBy(e => e.At)
                               .ToList();
                file.GoalDays = (file.GoalDays ?? new List<DateOnly>()).Distinct().OrderBy(d => d).ToList();

                foreach (var entry in file.Entries)
                {
                    entry.Seconds = Math.Max(0, entry.Seconds);
                    entry.Accuracy = double.IsNaN(entry.Accuracy) ? 0 : Math.Clamp(entry.Accuracy, 0, 1);
                    entry.Ruleset ??= string.Empty;
                }

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

        /// <summary>
        /// Reads the log from <paramref name="path"/>, or null when the file is missing or unreadable.
        /// </summary>
        public static BanchosucksActivityFile? Read(string path)
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
        /// Writes the log atomically, the same way the osu! Clicker saves: a new file next to the old one, then a swap
        /// that keeps the previous version as backup. A crash or power loss leaves either the old or the new file, never half of one.
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

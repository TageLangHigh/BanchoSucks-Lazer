// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Configuration;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: reads the player's setup out of the running game into a <see cref="BanchosucksSettingsDocument"/>
    /// and applies such a document to the running game. Used by the settings export / import (F46), the osu!stable
    /// takeover (F44, which builds a document from the stable config) and the backup both of them write first.
    /// </summary>
    /// <remarks>
    /// Nothing leaves the PC here: an export is a file in the game's own exports folder. What may be in it is
    /// decided by <see cref="BanchosucksSettingsAllowList"/> alone.
    /// Call from the update thread (the database and the bindables are used directly).
    /// </remarks>
    public class BanchosucksSettingsTransfer
    {
        public const string BACKUP_FILENAME = "settings-backup.json";
        public const string EXPORT_PREFIX = "banchosucks-settings";

        private readonly OsuConfigManager config;
        private readonly FrameworkConfigManager frameworkConfig;
        private readonly RealmAccess realm;
        private readonly Storage storage;

        /// <summary>
        /// The mouse handler of the host, for sensitivity and raw input. Without one the mouse values are left out.
        /// </summary>
        public MouseHandler? Mouse { get; init; }

        /// <summary>
        /// Looks up the live settings of a ruleset by short name. Per-ruleset settings are only written through
        /// this (the bindable checks the value); without it they are exported but not applied.
        /// </summary>
        public Func<string, IBanchosucksTextSettings?>? RulesetSettingsLookup { get; init; }

        /// <summary>
        /// Switches the running game to a skin. Without it only the config value is written (takes effect on the next start).
        /// </summary>
        public Action<Guid>? SkinSelected { get; init; }

        /// <summary>
        /// Written into exports as information.
        /// </summary>
        public string ClientVersion { get; init; } = string.Empty;

        public BanchosucksSettingsTransfer(OsuConfigManager config, FrameworkConfigManager frameworkConfig, RealmAccess realm, Storage storage)
        {
            this.config = config;
            this.frameworkConfig = frameworkConfig;
            this.realm = realm;
            this.storage = storage;
        }

        public Storage ExportStorage => (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

        private Storage privateStorage => storage.GetStorageForDirectory(BanchosucksKeyProfiles.DIRECTORY);

        #region Reading the current setup

        /// <summary>
        /// The current setup, limited to what the allow-list permits.
        /// </summary>
        public BanchosucksSettingsDocument Capture()
        {
            var document = new BanchosucksSettingsDocument
            {
                Client = ClientVersion,
                Created = DateTime.UtcNow,
            };

            foreach (var setting in BanchosucksSettingsAllowList.ExportedGameSettings)
            {
                string name = setting.ToString();

                if (config.BanchosucksGetText(name) is string text)
                    document.Game[name] = text;
            }

            foreach (var setting in BanchosucksSettingsAllowList.FrameworkSettings)
            {
                if (frameworkText(setting) is string text)
                    document.Framework[setting.ToString()] = text;
            }

            if (Mouse != null)
            {
                document.Mouse[BanchosucksSettingsAllowList.MOUSE_SENSITIVITY] = Mouse.Sensitivity.Value.ToString(CultureInfo.InvariantCulture);
                document.Mouse[BanchosucksSettingsAllowList.MOUSE_RELATIVE_MODE] = Mouse.UseRelativeMode.Value.ToString(CultureInfo.InvariantCulture);
            }

            realm.Run(r =>
            {
                r.Refresh();

                if (Guid.TryParse(config.Get<string>(OsuSetting.Skin), out var skinId) && r.Find<SkinInfo>(skinId) is SkinInfo skin)
                    document.Skin = new BanchosucksSkinReference { Name = skin.Name, ID = skin.ID };

                document.KeyBindings = BanchosucksKeyBindings.FromRows(readBindingRows(r));

                foreach (var setting in r.All<RealmRulesetSetting>())
                {
                    if (!BanchosucksSettingsAllowList.IsRulesetSettingAllowed(setting.RulesetName, setting.Key))
                        continue;

                    // the live value is newer than the database row while a save is still queued
                    string value = setting.Variant == 0 && RulesetSettingsLookup?.Invoke(setting.RulesetName)?.BanchosucksGetText(setting.Key) is string live
                        ? live
                        : setting.Value;

                    document.RulesetSettings.Add(new BanchosucksRulesetSettingEntry
                    {
                        Ruleset = setting.RulesetName,
                        Variant = setting.Variant,
                        Key = setting.Key,
                        Value = value,
                    });
                }
            });

            document.KeyProfiles = BanchosucksKeyProfiles.Load(storage).Profiles;

            return document;
        }

        private static List<BanchosucksBindingRow> readBindingRows(Realms.Realm r)
            => r.All<RealmKeyBinding>().AsEnumerable()
                .Select(b => new BanchosucksBindingRow(b.ID, b.RulesetName ?? string.Empty, b.Variant ?? 0, b.ActionInt, b.KeyCombinationString))
                .ToList();

        #endregion

        #region Preview

        /// <summary>
        /// Prepares an incoming document for this installation and lists what applying it would change.
        /// The skin is looked up by name here: a skin that is not imported on this PC is taken out of the document
        /// and reported in <paramref name="notes"/>.
        /// </summary>
        public List<BanchosucksSettingsChange> Preview(BanchosucksSettingsDocument incoming, out List<BanchosucksTransferNote> notes)
        {
            notes = new List<BanchosucksTransferNote>();

            if (incoming.Skin != null)
            {
                var local = FindSkin(incoming.Skin);

                if (local == null)
                {
                    notes.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.SkinMissing, incoming.Skin.Name));
                    incoming.Skin = null;
                }
                else
                    incoming.Skin = local;
            }

            if (incoming.IsNewerVersion)
                notes.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.NewerFile, incoming.Version.ToString(CultureInfo.InvariantCulture)));

            if (incoming.Skipped.Count > 0)
            {
                notes.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.EntriesSkipped, incoming.Skipped.Count.ToString(CultureInfo.InvariantCulture)));
                Logger.Log($"Banchosucks settings import: skipped {string.Join("; ", incoming.Skipped.Take(40))}");
            }

            return BanchosucksSettingsDiff.Compare(Capture(), incoming);
        }

        /// <summary>
        /// The skin of this installation that a reference from a file means: the same ID if it exists here
        /// (same installation, built-in skins), otherwise the first skin with that name.
        /// </summary>
        public BanchosucksSkinReference? FindSkin(BanchosucksSkinReference reference)
        {
            return realm.Run(r =>
            {
                var skins = r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => (s.ID, s.Name)).ToList();
                string? match = MatchSkinName(reference.Name, skins.Select(s => s.Name));

                var found = skins.Where(s => reference.ID != null && s.ID == reference.ID).Concat(skins.Where(s => s.Name == match)).FirstOrDefault();

                return found.Name == null ? null : new BanchosucksSkinReference { Name = found.Name, ID = found.ID };
            });
        }

        /// <summary>
        /// Finds a skin by name among the imported ones. The importer names a skin after its skin.ini and appends the
        /// folder name in brackets when the two differ ("Cool Skin [cool-skin-v2]"), so a folder name matches either.
        /// </summary>
        public static string? MatchSkinName(string? wanted, IEnumerable<string> available)
        {
            if (string.IsNullOrWhiteSpace(wanted))
                return null;

            wanted = wanted.Trim();
            var names = available.ToList();

            return names.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault(n => n.EndsWith($" [{wanted}]", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Applying

        /// <summary>
        /// Applies a document. The current setup is written to the backup file first (unless told otherwise), so
        /// the same UI can bring it back.
        /// </summary>
        public BanchosucksTransferResult Apply(BanchosucksSettingsDocument incoming, bool writeBackup = true)
        {
            var result = new BanchosucksTransferResult();

            if (writeBackup)
                WriteBackup();

            foreach (var pair in incoming.Game)
                result.Count(BanchosucksSettingsAllowList.IsGameSettingAllowed(pair.Key) && config.BanchosucksTrySetText(pair.Key, pair.Value));

            foreach (var pair in incoming.Framework)
            {
                result.Count(BanchosucksSettingsAllowList.IsFrameworkSettingAllowed(pair.Key)
                             && Enum.TryParse(pair.Key, false, out FrameworkSetting setting)
                             && setFrameworkText(setting, pair.Value));
            }

            if (incoming.Mouse.Count > 0)
                applyMouse(incoming.Mouse, result);

            if (incoming.Skin?.ID is Guid skinId)
            {
                bool exists = realm.Run(r => r.Find<SkinInfo>(skinId) is SkinInfo skin && !skin.DeletePending);

                if (exists)
                {
                    if (SkinSelected != null)
                        SkinSelected(skinId);
                    else
                        config.SetValue(OsuSetting.Skin, skinId.ToString());
                }

                result.Count(exists);
            }

            if (incoming.KeyBindings.Count > 0)
            {
                realm.Write(r =>
                {
                    var plan = BanchosucksKeyBindings.Plan(readBindingRows(r), incoming.KeyBindings, out int skipped);

                    foreach (var (id, keys) in plan)
                        r.Find<RealmKeyBinding>(id)!.KeyCombinationString = keys;

                    result.Applied += plan.Count;
                    result.Skipped += skipped;
                });
            }

            foreach (var setting in incoming.RulesetSettings)
            {
                // only through the live bindable: a database row with a value the ruleset cannot parse would break the next start
                result.Count(setting.Variant == 0
                             && BanchosucksSettingsAllowList.IsRulesetSettingAllowed(setting.Ruleset, setting.Key)
                             && RulesetSettingsLookup?.Invoke(setting.Ruleset) is IBanchosucksTextSettings live
                             && live.BanchosucksTrySetText(setting.Key, setting.Value));
            }

            if (incoming.KeyProfiles.Count > 0)
            {
                var profiles = BanchosucksKeyProfiles.Load(storage);
                profiles.Merge(incoming.KeyProfiles);
                profiles.Store(storage);
                result.Applied += incoming.KeyProfiles.Count;
            }

            config.Save();
            frameworkConfig.Save();

            return result;
        }

        private void applyMouse(IDictionary<string, string> values, BanchosucksTransferResult result)
        {
            if (Mouse == null)
            {
                result.Skipped += values.Count;
                return;
            }

            // raw input first: the sensitivity only means something with it
            if (values.TryGetValue(BanchosucksSettingsAllowList.MOUSE_RELATIVE_MODE, out string? relative))
                result.Count(BanchosucksSettingsText.TrySet(Mouse.UseRelativeMode, relative));

            if (values.TryGetValue(BanchosucksSettingsAllowList.MOUSE_SENSITIVITY, out string? sensitivity))
                result.Count(BanchosucksSettingsText.TrySet(Mouse.Sensitivity, sensitivity));
        }

        #endregion

        #region Files

        /// <summary>
        /// Writes the current setup to a new file in the exports folder.
        /// </summary>
        /// <returns>The file name inside <see cref="ExportStorage"/>.</returns>
        public string Export()
        {
            var exportStorage = ExportStorage;
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string filename = $"{EXPORT_PREFIX}-{stamp}{BanchosucksSettingsDocument.FILE_EXTENSION}";

            for (int duplicate = 2; exportStorage.Exists(filename); duplicate++)
                filename = $"{EXPORT_PREFIX}-{stamp}-{duplicate}{BanchosucksSettingsDocument.FILE_EXTENSION}";

            write(exportStorage, filename, Capture().Serialise());
            return filename;
        }

        public bool HasBackup => privateStorage.Exists(BACKUP_FILENAME);

        /// <summary>
        /// Writes the current setup to the backup file in the game data folder (one file, overwritten each time).
        /// </summary>
        public void WriteBackup() => write(privateStorage, BACKUP_FILENAME, Capture().Serialise());

        /// <summary>
        /// The setup as it was before the last import; null when there is no usable backup.
        /// </summary>
        public BanchosucksSettingsDocument? ReadBackup()
        {
            try
            {
                if (!HasBackup)
                    return null;

                using (var stream = privateStorage.GetStream(BACKUP_FILENAME))
                using (var reader = new StreamReader(stream))
                    return BanchosucksSettingsDocument.Parse(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks settings backup could not be read: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }
        }

        private static void write(Storage target, string filename, string content)
        {
            using (var stream = target.CreateFileSafely(filename))
            using (var writer = new StreamWriter(stream))
                writer.Write(content);
        }

        #endregion

        #region Framework settings

        private string? frameworkText(FrameworkSetting setting)
        {
            switch (setting)
            {
                case FrameworkSetting.FrameSync:
                    return frameworkConfig.Get<FrameSync>(setting).ToString();

                case FrameworkSetting.ExecutionMode:
                    return frameworkConfig.Get<ExecutionMode>(setting).ToString();

                case FrameworkSetting.VolumeUniversal:
                case FrameworkSetting.VolumeEffect:
                case FrameworkSetting.VolumeMusic:
                    return frameworkConfig.Get<double>(setting).ToString(CultureInfo.InvariantCulture);

                case FrameworkSetting.ShowUnicode:
                    return frameworkConfig.Get<bool>(setting).ToString(CultureInfo.InvariantCulture);

                default:
                    return null;
            }
        }

        private bool setFrameworkText(FrameworkSetting setting, string text)
        {
            switch (setting)
            {
                case FrameworkSetting.FrameSync:
                    return BanchosucksSettingsText.TrySet(frameworkConfig.GetBindable<FrameSync>(setting), text);

                case FrameworkSetting.ExecutionMode:
                    return BanchosucksSettingsText.TrySet(frameworkConfig.GetBindable<ExecutionMode>(setting), text);

                case FrameworkSetting.VolumeUniversal:
                case FrameworkSetting.VolumeEffect:
                case FrameworkSetting.VolumeMusic:
                    return BanchosucksSettingsText.TrySet(frameworkConfig.GetBindable<double>(setting), text);

                case FrameworkSetting.ShowUnicode:
                    return BanchosucksSettingsText.TrySet(frameworkConfig.GetBindable<bool>(setting), text);

                default:
                    return false;
            }
        }

        #endregion
    }

    public enum BanchosucksTransferNoteType
    {
        /// <summary>
        /// The file names a skin that is not imported on this PC; the detail is the skin name.
        /// </summary>
        SkinMissing,

        /// <summary>
        /// The file was written by a newer client; the detail is its format version.
        /// </summary>
        NewerFile,

        /// <summary>
        /// Entries of the file were not understood and left out; the detail is how many.
        /// </summary>
        EntriesSkipped,

        /// <summary>
        /// A value of the osu!stable config could not be mapped; the detail is the stable key.
        /// </summary>
        StableValueUnusable,
    }

    public readonly record struct BanchosucksTransferNote(BanchosucksTransferNoteType Type, string Detail);

    public class BanchosucksTransferResult
    {
        public int Applied;
        public int Skipped;

        public void Count(bool applied)
        {
            if (applied)
                Applied++;
            else
                Skipped++;
        }
    }
}

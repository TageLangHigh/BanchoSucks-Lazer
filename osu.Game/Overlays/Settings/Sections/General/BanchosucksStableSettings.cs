// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Skinning;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: "Von osu!stable übernehmen". Reads the player's osu!stable config and offers to apply its
    /// key bindings, audio offset, mouse settings, volumes, cursor size, background dim and skin
    /// (mapping: <see cref="BanchosucksStableConfig"/>). Below that the opt-in skin sync (<see cref="BanchosucksSkinSync"/>).
    /// The stable folder is only read.
    /// </summary>
    public partial class BanchosucksStableSettings : BanchosucksTransferSubsection
    {
        /// <summary>
        /// A stable config is a few kB; anything far beyond that is not one.
        /// </summary>
        private const long max_file_size = 2 * 1024 * 1024;

        protected override LocalisableString Header => "Von osu!stable übernehmen / Take over from osu!stable";

        [Resolved(CanBeNull = true)]
        private LegacyImportManager? legacyImportManager { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogs { get; set; }

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private BanchosucksSkinSync? skinSync { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private Bindable<bool> skinSyncEnabled = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new SettingsButtonV2
            {
                Text = "Stable-Einstellungen übernehmen … / Take over stable settings …",
                TooltipText = "Liest die Einstellungsdatei deiner osu!stable-Installation und zeigt, was sich übernehmen lässt: Tasten für alle vier Modi, Audio-Offset, Maus, Lautstärke, Cursorgröße, Hintergrund-Abdunklung und Skin. Nichts wird ohne Rückfrage geändert, in stable wird nichts geschrieben. / Reads the settings file of your osu!stable installation and shows what can be taken over: keys for all four modes, audio offset, mouse, volume, cursor size, background dim and skin. Nothing changes without asking, nothing is written to stable.",
                Keywords = new[] { @"stable", @"import", @"übernehmen", @"offset", @"keys", @"tasten", @"cfg", @"transfer" },
                Action = () => ReadStable(),
            });

            AddPreviewHolder();

            skinSyncEnabled = config.GetBindable<bool>(OsuSetting.BanchosucksSkinSync);

            SettingsButtonV2 checkNow;

            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Skin-Abgleich mit osu!stable / Skin sync from osu!stable",
                    HintText = "Solange das an ist, schaut der Client beim Start und danach alle zehn Minuten in den Skins-Ordner von osu!stable und importiert neue oder geänderte Skins. Es wird nie ein Skin gelöscht und nie während des Spielens importiert; in stable wird nichts geschrieben. / While this is on, the client looks into osu!stable's Skins folder at startup and every ten minutes after and imports new or changed skins. No skin is ever deleted, nothing is imported during gameplay, nothing is written to stable.",
                    Current = skinSyncEnabled,
                })
                {
                    Keywords = new[] { @"skin", @"sync", @"abgleich", @"stable", @"import" },
                },
                checkNow = new SettingsButtonV2
                {
                    Text = "Skins jetzt abgleichen / Check skins now",
                    TooltipText = "Sucht sofort nach neuen oder geänderten Skins im Skins-Ordner von osu!stable. / Looks for new or changed skins in osu!stable's Skins folder right away.",
                    Keywords = new[] { @"skin", @"sync", @"abgleich", @"stable" },
                    Action = () => skinSync?.CheckNow(true),
                },
            });

            // the manual check belongs to the opt-in: without it nothing reads the stable Skins folder
            skinSyncEnabled.BindValueChanged(enabled => checkNow.Enabled.Value = enabled.NewValue && skinSync != null, true);
        }

        /// <summary>
        /// Looks for the osu!stable installation and shows what its config would change.
        /// </summary>
        /// <param name="offerToLocate">Whether the player is asked for the folder when no installation is found.</param>
        public void ReadStable(bool offerToLocate = true)
        {
            Storage? stableStorage = null;

            try
            {
                stableStorage = legacyImportManager?.GetCurrentStableStorage();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: looking for osu!stable failed");
            }

            if (stableStorage != null)
            {
                ReadFrom(stableStorage);
                return;
            }

            // the same question the stable import asks; it needs the running game to open the folder picker
            if (offerToLocate && legacyImportManager != null && dialogs != null && game != null)
            {
                var located = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                dialogs.Push(new StableDirectoryLocationDialog(located));
                located.Task.ContinueWith(task =>
                {
                    if (!task.IsCompletedSuccessfully)
                        return;

                    string path = task.GetResultSafely();

                    Schedule(() =>
                    {
                        if (string.IsNullOrEmpty(path))
                            return;

                        legacyImportManager.UpdateStorage(path);
                        ReadStable(false);
                    });
                });

                return;
            }

            NotifyError(ClickerStrings.Text("No osu!stable installation was found on this PC.", "Auf diesem PC wurde keine osu!stable-Installation gefunden."));
        }

        /// <summary>
        /// Reads the config of the current Windows user from a stable folder.
        /// </summary>
        public void ReadFrom(Storage stableStorage)
        {
            string text;

            try
            {
                string? file = BanchosucksStableConfig.FindConfigFile(stableStorage.GetFiles(".", "osu!.*.cfg"), Environment.UserName);

                if (file == null)
                {
                    NotifyError(ClickerStrings.Text(
                        "The osu!stable folder has no settings file for this Windows user (osu!.<user>.cfg). Start osu!stable once, then try again.",
                        "Im osu!stable-Ordner liegt keine Einstellungsdatei für diesen Windows-Benutzer (osu!.<Benutzer>.cfg). Starte osu!stable einmal und versuche es dann erneut."));
                    return;
                }

                // read-only, and without getting in the way of a running stable
                using (var stream = new FileStream(stableStorage.GetFullPath(file), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length > max_file_size)
                        throw new InvalidDataException("The stable config is implausibly large.");

                    using (var reader = new StreamReader(stream))
                        text = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                // the message of an IO error contains the path (and with it the Windows user); the type is enough for the log
                Logger.Log($"Banchosucks: the osu!stable settings could not be read ({e.GetType().Name}).", LoggingTarget.Runtime, LogLevel.Important);
                NotifyError(ClickerStrings.Text("The osu!stable settings file could not be read.", "Die Einstellungsdatei von osu!stable konnte nicht gelesen werden."));
                return;
            }

            ReadConfigText(text);
        }

        /// <summary>
        /// Maps the text of a stable config and shows the preview.
        /// </summary>
        public void ReadConfigText(string text)
        {
            var stable = BanchosucksStableConfig.Parse(text);

            if (stable.Values.Count == 0)
            {
                NotifyError(ClickerStrings.Text("The osu!stable settings file contains none of the settings that can be taken over.",
                    "Die Einstellungsdatei von osu!stable enthält keine der Einstellungen, die sich übernehmen lassen."));
                return;
            }

            BanchosucksSettingsDocument incoming;
            List<BanchosucksTransferNote> notes;

            try
            {
                incoming = stable.Map(Transfer.Capture(), out notes);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: mapping the osu!stable settings failed");
                NotifyError(ClickerStrings.Text("The osu!stable settings could not be converted (see the log).", "Die osu!stable-Einstellungen konnten nicht umgerechnet werden (siehe Log)."));
                return;
            }

            ShowPreview(ClickerStrings.Text("Settings from osu!stable", "Einstellungen aus osu!stable"), incoming, BanchosucksStableConfig.TitleOf, notes);
        }
    }
}

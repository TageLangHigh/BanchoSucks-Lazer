// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: "Einstellungen sichern". Exports the player's setup into one JSON file in the exports folder,
    /// imports such a file again (with a preview and a backup) and restores the backup of the last import.
    /// What a file may contain is decided by <see cref="BanchosucksSettingsAllowList"/>: no account, no server,
    /// nothing that identifies the PC.
    /// </summary>
    public partial class BanchosucksSettingsBackupSettings : BanchosucksTransferSubsection
    {
        /// <summary>
        /// Larger files are not settings exports (a full one is well below 100 kB).
        /// </summary>
        private const long max_file_size = 4 * 1024 * 1024;

        protected override LocalisableString Header => "Einstellungen sichern / Settings backup";

        /// <summary>
        /// Whether the file manager is opened on the exported file. Off in tests.
        /// </summary>
        public bool RevealExportedFile { get; init; } = true;

        /// <summary>
        /// Name of the file written by the last export, inside the exports folder.
        /// </summary>
        public string? LastExportedFile { get; private set; }

        private FormFileSelector fileSelector = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = "Einstellungen exportieren / Export settings",
                    TooltipText = "Schreibt Einstellungen, Tastenbelegungen aller Modi, Modus-Einstellungen und den Namen des Skins in eine Datei im Ordner „exports“. Konto, Passwort und Server sind nicht enthalten. / Writes settings, key bindings of all modes, mode settings and the name of the skin to a file in the \"exports\" folder. Account, password and server are not included.",
                    Keywords = new[] { @"export", @"backup", @"sichern", @"sicherung", @"settings", @"einstellungen", @"transfer", @"umzug" },
                    Action = Export,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = fileSelector = new FormFileSelector(BanchosucksSettingsDocument.FILE_EXTENSION)
                    {
                        Caption = "Einstellungen importieren / Import settings",
                        HintText = "Eine exportierte Einstellungsdatei wählen oder ins Fenster ziehen. Vor dem Übernehmen wird gezeigt, was sich ändert. / Choose an exported settings file or drop it on the window. What changes is shown before anything is applied.",
                        PlaceholderText = "Datei wählen … / Choose a file …",
                    },
                },
                new SettingsButtonV2
                {
                    Text = "Sicherung wiederherstellen / Restore backup",
                    TooltipText = "Bringt das Setup von vor der letzten Übernahme zurück (ebenfalls mit Vorschau). / Brings back the setup from before the last import (also with a preview).",
                    Keywords = new[] { @"restore", @"undo", @"backup", @"sicherung", @"rückgängig" },
                    Action = RestoreBackup,
                },
            });

            AddPreviewHolder();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            fileSelector.Current.BindValueChanged(file =>
            {
                if (file.NewValue == null)
                    return;

                string path = file.NewValue.FullName;

                // the selector is only a button here; it should not keep showing the last file
                Schedule(() => fileSelector.Current.Value = null);
                ImportFile(path);
            });
        }

        public void Export()
        {
            try
            {
                string filename = LastExportedFile = Transfer.Export();
                var exportStorage = Transfer.ExportStorage;

                Notify(ClickerStrings.Text("Settings exported to {0}. Click to show the file.", "Einstellungen nach {0} exportiert. Klicken, um die Datei zu zeigen.", filename), () =>
                {
                    exportStorage.PresentFileExternally(filename);
                    return true;
                });

                if (RevealExportedFile)
                    exportStorage.PresentFileExternally(filename);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: settings export failed");
                NotifyError(ClickerStrings.Text("The settings could not be exported (see the log).", "Die Einstellungen konnten nicht exportiert werden (siehe Log)."));
            }
        }

        /// <summary>
        /// Reads a settings file and shows what it would change.
        /// </summary>
        public void ImportFile(string path)
        {
            BanchosucksSettingsDocument document;

            try
            {
                var info = new FileInfo(path);

                if (!info.Exists || info.Length > max_file_size)
                    throw new FormatException("Not a settings export (missing or too large).");

                document = BanchosucksSettingsDocument.Parse(File.ReadAllText(path));
            }
            catch (FormatException)
            {
                NotifyError(ClickerStrings.Text("This file is not a settings export of BanchoSucks Lazer.", "Diese Datei ist kein Einstellungs-Export von BanchoSucks Lazer."));
                return;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: settings file could not be read");
                NotifyError(ClickerStrings.Text("The file could not be read.", "Die Datei konnte nicht gelesen werden."));
                return;
            }

            ShowPreview(ClickerStrings.Text("Import: {0}", "Import: {0}", Path.GetFileName(path)), document);
        }

        public void RestoreBackup()
        {
            var backup = Transfer.ReadBackup();

            if (backup == null)
            {
                NotifyError(ClickerStrings.Text("There is no backup yet. One is written whenever settings are imported.",
                    "Es gibt noch keine Sicherung. Sie wird angelegt, sobald Einstellungen übernommen werden."));
                return;
            }

            ShowPreview(ClickerStrings.Text("Backup from before the last import", "Sicherung von vor der letzten Übernahme"), backup);
        }    }
}

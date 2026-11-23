// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Skinning;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: shared part of the settings subsections that bring a setup into the game (settings file, backup,
    /// osu!stable config). It owns the one rule they all follow: show what will change, let the player untick,
    /// ask once, write a backup, then apply.
    /// </summary>
    public abstract partial class BanchosucksTransferSubsection : SettingsSubsection
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        protected RulesetStore Rulesets { get; private set; } = null!;

        [Resolved(CanBeNull = true)]
        private IRulesetConfigCache? rulesetConfigs { get; set; }

        [Resolved(CanBeNull = true)]
        private SkinManager? skins { get; set; }

        [Resolved(CanBeNull = true)]
        private OsuGameBase? gameBase { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private BanchosucksSettingsTransfer? transfer;

        private readonly Container previewHolder = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
        };

        /// <summary>
        /// The preview currently shown, if any.
        /// </summary>
        public BanchosucksChangePreview? CurrentPreview { get; private set; }

        /// <summary>
        /// Counts the applied imports (for tests).
        /// </summary>
        public int AppliedCount { get; private set; }

        protected BanchosucksSettingsTransfer Transfer => transfer ??= new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage)
        {
            Mouse = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault(),
            ClientVersion = gameBase?.Version ?? string.Empty,
            RulesetSettingsLookup = lookupRulesetSettings,
            SkinSelected = skins == null ? null : selectSkin,
        };

        private IBanchosucksTextSettings? lookupRulesetSettings(string shortName)
        {
            try
            {
                var ruleset = Rulesets.GetRuleset(shortName)?.CreateInstance();
                return ruleset == null ? null : rulesetConfigs?.GetConfigFor(ruleset) as IBanchosucksTextSettings;
            }
            catch (Exception)
            {
                // the cache throws for rulesets it does not know
                return null;
            }
        }

        private void selectSkin(Guid id)
        {
            var skin = skins?.Query(s => s.ID == id);

            if (skin != null)
                skins!.CurrentSkinInfo.Value = skin;
        }

        /// <summary>
        /// Adds the place where previews appear; call once, after the subsection's own controls.
        /// </summary>
        protected void AddPreviewHolder() => Add(previewHolder);

        /// <summary>
        /// Shows what <paramref name="incoming"/> would change. Applying happens from the preview, after a confirmation.
        /// </summary>
        /// <param name="heading">What is being imported.</param>
        /// <param name="incoming">The settings to bring in.</param>
        /// <param name="titles">When given, every setting gets its own checkbox with this title (see <see cref="BanchosucksChangeGroups.BySetting"/>).</param>
        /// <param name="extraNotes">Notes to show above the list.</param>
        protected void ShowPreview(LocalisableString heading, BanchosucksSettingsDocument incoming, Func<BanchosucksSettingsChange, (string En, string De)?>? titles = null,
                                   IEnumerable<LocalisableString>? extraNotes = null)
        {
            List<BanchosucksSettingsChange> changes;
            List<BanchosucksTransferNote> notes;

            try
            {
                changes = Transfer.Preview(incoming, out notes);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: settings preview failed");
                NotifyError(ClickerStrings.Text("The settings could not be compared (see the log).", "Die Einstellungen konnten nicht verglichen werden (siehe Log)."));
                return;
            }

            var groups = titles == null
                ? BanchosucksChangeGroups.BySection(changes, Rulesets)
                : BanchosucksChangeGroups.BySetting(changes, Rulesets, titles);

            var noteTexts = (extraNotes ?? Enumerable.Empty<LocalisableString>()).Concat(notes.Select(describe)).ToList();

            previewHolder.Child = CurrentPreview = new BanchosucksChangePreview(heading, groups, noteTexts)
            {
                Cancelled = ClosePreview,
                Accepted = accepted =>
                {
                    if (accepted.Count == 0)
                    {
                        ClosePreview();
                        return;
                    }

                    confirm(ClickerStrings.Text("Apply {0} changes? Your current setup is saved as a backup first.",
                        "{0} Änderungen übernehmen? Dein jetziges Setup wird vorher als Sicherung gespeichert.", accepted.Count), () => apply(incoming, accepted));
                },
            };
        }

        public void ClosePreview()
        {
            previewHolder.Clear();
            CurrentPreview = null;
        }

        private void apply(BanchosucksSettingsDocument incoming, IReadOnlyList<BanchosucksSettingsChange> accepted)
        {
            try
            {
                var result = Transfer.Apply(BanchosucksSettingsDiff.Restrict(incoming, accepted));

                AppliedCount++;
                ClosePreview();
                OnApplied();

                Notify(result.Skipped == 0
                    ? ClickerStrings.Text("{0} settings applied. \"Restore backup\" brings the old setup back.",
                        "{0} Einstellungen übernommen. „Sicherung wiederherstellen“ bringt das alte Setup zurück.", result.Applied)
                    : ClickerStrings.Text("{0} settings applied, {1} could not be applied here. \"Restore backup\" brings the old setup back.",
                        "{0} Einstellungen übernommen, {1} ließen sich hier nicht übernehmen. „Sicherung wiederherstellen“ bringt das alte Setup zurück.", result.Applied, result.Skipped));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: applying settings failed");
                NotifyError(ClickerStrings.Text("The settings could not be applied (see the log).", "Die Einstellungen konnten nicht übernommen werden (siehe Log)."));
            }
        }

        /// <summary>
        /// Called after settings were applied.
        /// </summary>
        protected virtual void OnApplied()
        {
        }

        private void confirm(LocalisableString message, Action action)
        {
            if (dialogOverlay != null)
                dialogOverlay.Push(new ConfirmDialog(message, action));
            else
                action();
        }

        protected void Notify(LocalisableString text, Func<bool>? activated = null)
        {
            var notification = new SimpleNotification { Text = text, Icon = FontAwesome.Solid.Check };

            if (activated != null)
                notification.Activated = activated;

            notifications?.Post(notification);
        }

        protected void NotifyError(LocalisableString text) => notifications?.Post(new SimpleErrorNotification { Text = text });

        private static LocalisableString describe(BanchosucksTransferNote note)
        {
            switch (note.Type)
            {
                case BanchosucksTransferNoteType.SkinMissing:
                    return ClickerStrings.Text("The skin \"{0}\" is not imported on this PC and stays as it is.",
                        "Der Skin „{0}“ ist auf diesem PC nicht importiert und bleibt, wie er ist.", note.Detail);

                case BanchosucksTransferNoteType.NewerFile:
                    return ClickerStrings.Text("The file comes from a newer client; what this version does not know is skipped.",
                        "Die Datei stammt von einem neueren Client; was diese Version nicht kennt, wird übersprungen.");

                case BanchosucksTransferNoteType.EntriesSkipped:
                    return ClickerStrings.Text("{0} entries of the file are unknown here and are skipped.",
                        "{0} Einträge der Datei sind hier unbekannt und werden übersprungen.", note.Detail);

                default:
                    return ClickerStrings.Text("\"{0}\" has a value that cannot be taken over.",
                        "„{0}“ hat einen Wert, der sich nicht übernehmen lässt.", note.Detail);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    /// <summary>
    /// Banchosucks: what the library scan found, with the choice what to remove. Choosing something only opens
    /// <see cref="BanchosucksLibraryCleanupConfirmationDialog"/>; nothing is removed from here.
    /// </summary>
    public partial class BanchosucksLibraryCleanupResultDialog : PopupDialog
    {
        public BanchosucksLibraryCleanupResultDialog(BanchosucksLibraryCleanup.Result result, Action<IReadOnlyList<Guid>, int, int> requestRemoval)
        {
            var duplicates = result.DuplicatesToRemove.Select(s => s.ID).ToList();
            var broken = result.BrokenSets.Select(b => b.Set.ID).ToList();

            HeaderText = ClickerStrings.Text("Library checked", "Bibliothek geprüft");
            Icon = FontAwesome.Solid.Broom;
            BodyText = CreateSummary(result);

            var buttons = new List<PopupDialogButton>();

            if (duplicates.Count > 0)
            {
                buttons.Add(new PopupDialogOkButton
                {
                    Text = ClickerStrings.Text("Remove {0} duplicate(s)...", "{0} Duplikat(e) entfernen ...", duplicates.Count),
                    Action = () => requestRemoval(duplicates, duplicates.Count, 0),
                });
            }

            if (broken.Count > 0)
            {
                buttons.Add(new PopupDialogOkButton
                {
                    Text = ClickerStrings.Text("Remove {0} broken set(s)...", "{0} defekte(s) Set(s) entfernen ...", broken.Count),
                    Action = () => requestRemoval(broken, 0, broken.Count),
                });
            }

            if (duplicates.Count > 0 && broken.Count > 0)
            {
                buttons.Add(new PopupDialogOkButton
                {
                    Text = ClickerStrings.Text("Remove both ({0} sets)...", "Beides entfernen ({0} Sets) ...", duplicates.Count + broken.Count),
                    Action = () => requestRemoval(duplicates.Concat(broken).Distinct().ToList(), duplicates.Count, broken.Count),
                });
            }

            buttons.Add(new PopupDialogCancelButton
            {
                Text = result.AnythingToRemove
                    ? ClickerStrings.Text("Change nothing", "Nichts ändern")
                    : ClickerStrings.Text("Close", "Schließen"),
            });

            Buttons = buttons;
        }

        /// <summary>
        /// The counts of a scan as text, one line per finding.
        /// </summary>
        public static LocalisableString CreateSummary(BanchosucksLibraryCleanup.Result result)
        {
            int duplicates = result.DuplicatesToRemove.Count;
            int duplicateGroups = result.DuplicateGroups.Count(g => g.Remove.Count > 0);

            string en = $"Beatmap sets checked: {result.ScannedSets}\n"
                        + $"Duplicates: {duplicates} surplus copies of {duplicateGroups} set(s). The copy with local scores stays, otherwise the most recently imported one.\n"
                        + $"Broken sets (no difficulty, or .osu / audio file missing for every difficulty): {result.BrokenSets.Count}";
            string de = $"Geprüfte Beatmap-Sets: {result.ScannedSets}\n"
                        + $"Duplikate: {duplicates} überzählige Kopien von {duplicateGroups} Set(s). Die Kopie mit lokalen Scores bleibt, sonst die zuletzt importierte.\n"
                        + $"Defekte Sets (keine Schwierigkeit, oder .osu- / Audiodatei fehlt bei jeder Schwierigkeit): {result.BrokenSets.Count}";

            if (result.BrokenSetsWithScores > 0)
            {
                en += $"\n{result.BrokenSetsWithScores} of the broken sets have local scores. The scores stay in the database and show again when the set is imported again.";
                de += $"\n{result.BrokenSetsWithScores} der defekten Sets haben lokale Scores. Die Scores bleiben in der Datenbank und erscheinen wieder, wenn das Set neu importiert wird.";
            }

            if (result.PartlyDamagedSets.Count > 0)
            {
                en += $"\nPartly damaged sets (some difficulties still playable): {result.PartlyDamagedSets.Count}. These are never removed here.";
                de += $"\nTeilweise beschädigte Sets (einige Schwierigkeiten noch spielbar): {result.PartlyDamagedSets.Count}. Diese werden hier nie entfernt.";
            }

            if (!result.AnythingToRemove)
            {
                en += "\nNothing to clean up.";
                de += "\nNichts aufzuräumen.";
            }

            return ClickerStrings.Text(en, de);
        }
    }

    /// <summary>
    /// Banchosucks: the last question before the clean-up removes beatmap sets. Names how many sets are affected and
    /// needs the confirm button to be held, like every other deletion in the maintenance settings.
    /// </summary>
    public partial class BanchosucksLibraryCleanupConfirmationDialog : DangerousActionDialog
    {
        /// <summary>
        /// The number of sets this dialog asks about.
        /// </summary>
        public int SetCount { get; }

        public BanchosucksLibraryCleanupConfirmationDialog(int duplicates, int broken, int setCount, Action remove)
        {
            SetCount = setCount;

            Icon = FontAwesome.Solid.Trash;
            BodyText = ClickerStrings.Text(
                "Remove {0} beatmap set(s) from the library ({1} duplicate(s), {2} broken)?\n"
                + "Until the game is closed they can be restored in Maintenance > Beatmaps (recently deleted beatmaps).",
                "{0} Beatmap-Set(s) aus der Bibliothek entfernen ({1} Duplikat(e), {2} defekt)?\n"
                + "Bis zum Beenden des Spiels lassen sie sich unter Wartung > Beatmaps wiederherstellen (kürzlich gelöschte Beatmaps).",
                setCount, duplicates, broken);
            DangerousAction = remove;
        }
    }
}

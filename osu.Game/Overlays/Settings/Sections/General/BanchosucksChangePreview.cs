// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: one tickable line of a "what will change" preview and the changes behind it.
    /// </summary>
    public class BanchosucksChangeGroup
    {
        public LocalisableString Title;

        /// <summary>
        /// Old and new values, one change per line.
        /// </summary>
        public string Detail = string.Empty;

        public readonly List<BanchosucksSettingsChange> Changes = new List<BanchosucksSettingsChange>();

        public readonly BindableBool Ticked = new BindableBool(true);
    }

    /// <summary>
    /// Banchosucks: shows what an import (settings file, backup, osu!stable config) would change, one checkbox per
    /// group, and hands back the changes the player left ticked. Nothing is applied in here.
    /// </summary>
    public partial class BanchosucksChangePreview : FillFlowContainer
    {
        public readonly IReadOnlyList<BanchosucksChangeGroup> Groups;

        /// <summary>
        /// Invoked with the changes of all ticked groups when the player presses "apply".
        /// </summary>
        public Action<IReadOnlyList<BanchosucksSettingsChange>>? Accepted;

        public Action? Cancelled;

        private readonly LocalisableString heading;
        private readonly IReadOnlyList<LocalisableString> notes;

        public BanchosucksChangePreview(LocalisableString heading, IReadOnlyList<BanchosucksChangeGroup> groups, IEnumerable<LocalisableString> notes)
        {
            this.heading = heading;
            this.notes = notes.ToList();
            Groups = groups;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 6);
        }

        public IReadOnlyList<BanchosucksSettingsChange> TickedChanges => Groups.Where(g => g.Ticked.Value).SelectMany(g => g.Changes).ToList();

        public void Accept() => Accepted?.Invoke(TickedChanges);

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Padding = SettingsPanel.CONTENT_PADDING,
            };

            content.Add(text(heading, 16, true, Colour4.White));

            foreach (var note in notes)
                content.Add(text(note, 13, false, colours.Yellow));

            if (Groups.Count == 0)
            {
                content.Add(text(ClickerStrings.Text("Nothing to change: everything is already set this way.", "Nichts zu ändern: Alles ist bereits so eingestellt."), 14, false, colours.Gray9));
            }
            else
            {
                content.Add(text(ClickerStrings.Text("Untick what should stay as it is.", "Haken entfernen bei allem, was bleiben soll, wie es ist."), 13, false, colours.Gray9));

                foreach (var group in Groups)
                {
                    content.Add(new OsuCheckbox
                    {
                        LabelText = group.Title,
                        Current = group.Ticked,
                    });

                    if (group.Detail.Length > 0)
                    {
                        var detail = text(group.Detail, 12, false, colours.Gray9);
                        detail.Padding = new MarginPadding { Left = 10, Bottom = 4 };
                        content.Add(detail);
                    }
                }
            }

            Add(content);

            if (Groups.Count > 0)
            {
                Add(new SettingsButtonV2
                {
                    Text = "Angehakte übernehmen / Apply ticked",
                    Action = Accept,
                });
            }

            Add(new SettingsButtonV2
            {
                Text = Groups.Count > 0 ? "Abbrechen / Cancel" : "Schließen / Close",
                BackgroundColour = colours.Gray4,
                Action = () => Cancelled?.Invoke(),
            });
        }

        private static OsuTextFlowContainer text(LocalisableString value, float size, bool bold, Colour4 colour)
        {
            var flow = new OsuTextFlowContainer(t =>
            {
                t.Font = OsuFont.Default.With(size: size, weight: bold ? FontWeight.Bold : FontWeight.Regular);
                t.Colour = colour;
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            flow.AddText(value);
            return flow;
        }
    }

    /// <summary>
    /// Banchosucks: turns a list of changes into the groups a <see cref="BanchosucksChangePreview"/> shows.
    /// </summary>
    public static class BanchosucksChangeGroups
    {
        /// <summary>
        /// How many changes a group lists before it only counts the rest.
        /// </summary>
        private const int max_lines = 8;

        /// <summary>
        /// One group per kind of setting (and per ruleset for keys): for settings files, which can change hundreds of values.
        /// </summary>
        public static List<BanchosucksChangeGroup> BySection(IEnumerable<BanchosucksSettingsChange> changes, RulesetStore? rulesets)
            => build(changes, rulesets, c => c.Section is BanchosucksSettingsSection.KeyBindings or BanchosucksSettingsSection.RulesetSettings
                ? $"{c.Section}:{c.Ruleset}"
                : c.Section.ToString(), null);

        /// <summary>
        /// One group per setting, keys per ruleset: for the osu!stable takeover, where the player decides value by value.
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <param name="rulesets">For the names of rulesets and key actions.</param>
        /// <param name="titles">English and German title of a single setting; null falls back to the setting's name.</param>
        public static List<BanchosucksChangeGroup> BySetting(IEnumerable<BanchosucksSettingsChange> changes, RulesetStore? rulesets, Func<BanchosucksSettingsChange, (string En, string De)?>? titles)
            => build(changes, rulesets, c => c.Section is BanchosucksSettingsSection.KeyBindings
                ? $"{c.Section}:{c.Ruleset}"
                : $"{c.Section}:{c.Ruleset}:{c.Variant}:{c.Key}", titles);

        private static List<BanchosucksChangeGroup> build(IEnumerable<BanchosucksSettingsChange> changes, RulesetStore? rulesets, Func<BanchosucksSettingsChange, string> groupKey,
                                                          Func<BanchosucksSettingsChange, (string En, string De)?>? titles)
        {
            var groups = new List<BanchosucksChangeGroup>();

            foreach (var grouping in changes.GroupBy(groupKey))
            {
                var list = grouping.ToList();
                var first = list[0];

                var group = new BanchosucksChangeGroup();
                group.Changes.AddRange(list);

                var single = list.Count == 1 ? titles?.Invoke(first) : null;

                if (single is (string en, string de))
                {
                    group.Title = ClickerStrings.Text(en, de);
                    group.Detail = $"{display(first.Old)} → {display(first.New)}";
                }
                else
                {
                    var (sectionEn, sectionDe) = sectionTitle(first, rulesets);
                    group.Title = ClickerStrings.Text($"{sectionEn} ({list.Count})", $"{sectionDe} ({list.Count})");

                    var lines = list.Take(max_lines).Select(c => $"{changeName(c, rulesets)}: {display(c.Old)} → {display(c.New)}").ToList();
                    if (list.Count > max_lines)
                        lines.Add($"… +{list.Count - max_lines}");

                    group.Detail = string.Join("\n", lines);
                }

                groups.Add(group);
            }

            return groups;
        }

        private static string display(string value) => value.Length == 0 ? "–" : value;

        private static (string En, string De) sectionTitle(BanchosucksSettingsChange change, RulesetStore? rulesets)
        {
            switch (change.Section)
            {
                case BanchosucksSettingsSection.Game:
                    return ("Game settings", "Spiel-Einstellungen");

                case BanchosucksSettingsSection.Framework:
                    return ("Frame limiter, threading, volume", "Bildrate, Threading, Lautstärke");

                case BanchosucksSettingsSection.Mouse:
                    return ("Mouse (sensitivity, raw input)", "Maus (Empfindlichkeit, Raw Input)");

                case BanchosucksSettingsSection.Skin:
                    return ("Skin", "Skin");

                case BanchosucksSettingsSection.KeyBindings:
                    return change.Ruleset.Length == 0
                        ? ("Keys: global", "Tasten: Allgemein")
                        : ($"Keys: {RulesetName(rulesets, change.Ruleset)}", $"Tasten: {RulesetName(rulesets, change.Ruleset)}");

                case BanchosucksSettingsSection.RulesetSettings:
                    return ($"Mode settings: {RulesetName(rulesets, change.Ruleset)}", $"Modus-Einstellungen: {RulesetName(rulesets, change.Ruleset)}");

                default:
                    return ("Key profiles", "Tasten-Profile");
            }
        }

        private static string changeName(BanchosucksSettingsChange change, RulesetStore? rulesets)
        {
            switch (change.Section)
            {
                case BanchosucksSettingsSection.KeyBindings:
                    string action = int.TryParse(change.Key, out int number) ? ActionName(rulesets, change.Ruleset, change.Variant, number) : change.Key;
                    string variant = VariantName(rulesets, change.Ruleset, change.Variant);
                    return variant.Length == 0 ? action : $"{variant} {action}";

                case BanchosucksSettingsSection.KeyProfiles:
                    return $"{RulesetName(rulesets, change.Ruleset)} {VariantName(rulesets, change.Ruleset, change.Variant)}".TrimEnd();

                default:
                    return change.Key;
            }
        }

        public static string RulesetName(RulesetStore? rulesets, string shortName)
            => rulesets?.GetRuleset(shortName)?.Name ?? shortName;

        /// <summary>
        /// "4K" for an osu!mania variant; empty for rulesets with a single set of bindings.
        /// </summary>
        public static string VariantName(RulesetStore? rulesets, string shortName, int variant)
        {
            if (shortName.Length == 0 || variant == 0)
                return string.Empty;

            try
            {
                return rulesets?.GetRuleset(shortName)?.CreateInstance().GetVariantName(variant).ToString() ?? variant.ToString();
            }
            catch (Exception)
            {
                return variant.ToString();
            }
        }

        /// <summary>
        /// The name of a key action ("LeftButton", "Key3", "ToggleChat"); its number when this client does not know it.
        /// </summary>
        public static string ActionName(RulesetStore? rulesets, string shortName, int variant, int action)
        {
            if (shortName.Length == 0)
                return Enum.IsDefined(typeof(GlobalAction), action) ? ((GlobalAction)action).ToString() : action.ToString();

            try
            {
                var match = rulesets?.GetRuleset(shortName)?.CreateInstance().GetDefaultKeyBindings(variant).FirstOrDefault(b => (int)b.Action == action);
                return match?.Action.ToString() ?? action.ToString();
            }
            catch (Exception)
            {
                return action.ToString();
            }
        }
    }
}

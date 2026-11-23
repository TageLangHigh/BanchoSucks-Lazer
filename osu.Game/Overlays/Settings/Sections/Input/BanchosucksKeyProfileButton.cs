// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    /// <summary>
    /// Banchosucks: "Tasten-Profile" of one ruleset and variant, shown at the top of its key binding list. The button
    /// opens a small list where the player saves the current bindings under a name, switches to a saved profile or
    /// deletes one. Switching asks first and keeps the replaced bindings as the profile "Sicherung / Backup".
    /// </summary>
    public partial class BanchosucksKeyProfileButton : SettingsButtonV2, IHasPopover
    {
        /// <summary>
        /// Invoked after a profile replaced the bindings, so the rows of the subsection can reload.
        /// </summary>
        public Action? ProfileApplied;

        private readonly string ruleset;
        private readonly int variant;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private BanchosucksKeyProfileManager? manager;

        private BanchosucksKeyProfileManager profileManager => manager ??= new BanchosucksKeyProfileManager(realm, storage);

        public BanchosucksKeyProfileButton(RulesetInfo ruleset, int variant)
        {
            this.ruleset = ruleset.ShortName;
            this.variant = variant;

            Text = "Tasten-Profile … / Key profiles …";
            TooltipText = "Die Belegung dieses Modus unter einem Namen speichern, zwischen gespeicherten Profilen wechseln oder Profile löschen. / Save this mode's bindings under a name, switch between saved profiles or delete them.";
            Keywords = new[] { @"profile", @"profil", @"layout", @"belegung", @"preset" };
            Action = this.ShowPopover;
        }

        /// <summary>
        /// The saved profiles of this ruleset and variant.
        /// </summary>
        public IReadOnlyList<BanchosucksKeyProfile> Profiles => profileManager.For(ruleset, variant);

        public Popover GetPopover() => new ProfilePopover(this);

        /// <summary>
        /// Saves the current bindings under a name. An existing profile of that name is only replaced after a confirmation.
        /// </summary>
        /// <returns>Whether the name was usable.</returns>
        public bool SaveCurrent(string? name, Action? saved = null)
        {
            string? cleaned = BanchosucksKeyProfiles.CleanName(name);

            if (cleaned == null || string.Equals(cleaned, BanchosucksKeyProfiles.BACKUP_NAME, StringComparison.OrdinalIgnoreCase))
            {
                notifyError(ClickerStrings.Text("Please enter a name for the profile.", "Bitte einen Namen für das Profil eingeben."));
                return false;
            }

            void save() => guarded(() =>
            {
                profileManager.SaveCurrent(ruleset, variant, cleaned);
                notify(ClickerStrings.Text("Key profile \"{0}\" saved.", "Tasten-Profil „{0}“ gespeichert.", cleaned));
                saved?.Invoke();
            });

            if (Profiles.Any(p => string.Equals(p.Name, cleaned, StringComparison.OrdinalIgnoreCase)))
                confirm(ClickerStrings.Text("Replace the key profile \"{0}\" with the current bindings?", "Das Tasten-Profil „{0}“ mit der jetzigen Belegung überschreiben?", cleaned), save);
            else
                save();

            return true;
        }

        /// <summary>
        /// Switches to a saved profile after telling the player how many keys change and asking once.
        /// </summary>
        public void RequestLoad(string name)
        {
            var profile = Profiles.FirstOrDefault(p => p.Name == name);

            if (profile == null)
                return;

            int changes = 0;
            guarded(() => changes = profileManager.CountChanges(profile));

            if (changes == 0)
            {
                notify(ClickerStrings.Text("The key profile \"{0}\" is already active.", "Das Tasten-Profil „{0}“ ist bereits aktiv.", profile.Name));
                return;
            }

            confirm(ClickerStrings.Text("Switch to the key profile \"{0}\"? {1} keys change. The current bindings are kept as \"{2}\".",
                "Zum Tasten-Profil „{0}“ wechseln? {1} Tasten ändern sich. Die jetzige Belegung bleibt als „{2}“ erhalten.", profile.Name, changes, BanchosucksKeyProfiles.BACKUP_NAME), () => guarded(() =>
            {
                profileManager.Apply(profile);
                ProfileApplied?.Invoke();
                notify(ClickerStrings.Text("Key profile \"{0}\" is active.", "Tasten-Profil „{0}“ ist aktiv.", profile.Name));
            }));
        }

        public void RequestDelete(string name, Action? deleted = null)
        {
            confirm(ClickerStrings.Text("Delete the key profile \"{0}\"? The current bindings stay as they are.", "Das Tasten-Profil „{0}“ löschen? Die jetzige Belegung bleibt, wie sie ist.", name), () => guarded(() =>
            {
                profileManager.Delete(ruleset, variant, name);
                deleted?.Invoke();
            }));
        }

        private void guarded(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: key profile operation failed");
                notifyError(ClickerStrings.Text("The key profile could not be saved or loaded (see the log).", "Das Tasten-Profil konnte nicht gespeichert oder geladen werden (siehe Log)."));
            }
        }

        private void confirm(LocalisableString message, Action action)
        {
            if (dialogOverlay != null)
                dialogOverlay.Push(new ConfirmDialog(message, action));
            else
                action();
        }

        private void notify(LocalisableString text) => notifications?.Post(new SimpleNotification { Text = text, Icon = FontAwesome.Solid.Keyboard });

        private void notifyError(LocalisableString text) => notifications?.Post(new SimpleErrorNotification { Text = text });

        private partial class ProfilePopover : OsuPopover
        {
            private const float width = 380;
            private const float button_width = 84;

            private readonly BanchosucksKeyProfileButton owner;

            private readonly FillFlowContainer list;
            private readonly OsuTextBox nameBox;

            public ProfilePopover(BanchosucksKeyProfileButton owner)
            {
                this.owner = owner;

                Child = new FillFlowContainer
                {
                    Width = width,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = ClickerStrings.Text("Key profiles", "Tasten-Profile"),
                            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                        },
                        list = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                        },
                        new OsuSpriteText
                        {
                            Text = ClickerStrings.Text("Save the current bindings as:", "Jetzige Belegung speichern als:"),
                            Font = OsuFont.Default.With(size: 14),
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 34,
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Right = button_width + 6 },
                                    Child = nameBox = new OsuTextBox
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        LengthLimit = BanchosucksKeyProfiles.MAX_NAME_LENGTH,
                                        PlaceholderText = ClickerStrings.Text("Profile name", "Profilname"),
                                    },
                                },
                                new RoundedButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Size = new Vector2(button_width, 30),
                                    Text = ClickerStrings.Text("Save", "Speichern"),
                                    Action = save,
                                },
                            },
                        },
                    },
                };

                nameBox.OnCommit += (_, _) => save();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                refresh();
            }

            private void save()
            {
                // a confirmation dialog may follow; the popover would only be in its way
                if (owner.SaveCurrent(nameBox.Text))
                    this.HidePopover();
            }

            private void refresh()
            {
                list.Clear();

                var profiles = owner.Profiles;

                if (profiles.Count == 0)
                {
                    list.Add(new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 14))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = ClickerStrings.Text("No profiles saved for this mode yet.", "Für diesen Modus ist noch kein Profil gespeichert."),
                    });
                    return;
                }

                foreach (var profile in profiles)
                {
                    string name = profile.Name;

                    list.Add(new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Right = 2 * (button_width + 6) },
                                // not OsuSpriteText with Truncate, which throws
                                Child = new TruncatingSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Text = name,
                                    Font = OsuFont.Default.With(size: 16),
                                },
                            },
                            new RoundedButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -(button_width + 6),
                                Size = new Vector2(button_width, 26),
                                Text = ClickerStrings.Text("Load", "Laden"),
                                Action = () =>
                                {
                                    this.HidePopover();
                                    owner.RequestLoad(name);
                                },
                            },
                            new DangerousRoundedButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Size = new Vector2(button_width, 26),
                                Text = ClickerStrings.Text("Delete", "Löschen"),
                                Action = () =>
                                {
                                    this.HidePopover();
                                    owner.RequestDelete(name);
                                },
                            },
                        },
                    });
                }
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Online.Multiplayer;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: "Save as template" and "Load template" in the multiplayer room settings, with the result of the last action below.
    /// </summary>
    public partial class BanchosucksLobbyTemplateControls : CompositeDrawable
    {
        private const float popover_width = 420;

        private readonly IBanchosucksLobbyTemplateForm form;

        [Resolved]
        private BanchosucksLobbyTemplateManager manager { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public PopoverButton SaveButton { get; private set; } = null!;
        public PopoverButton LoadButton { get; private set; } = null!;

        private OsuTextFlowContainer status = null!;

        public BanchosucksLobbyTemplateControls(IBanchosucksLobbyTemplateForm form)
        {
            this.form = form;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 7),
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 10),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable?[]
                            {
                                SaveButton = new PopoverButton(() => new SavePopover(manager, form))
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Text = ClickerStrings.Text("Save as template", "Als Vorlage speichern"),
                                },
                                null,
                                LoadButton = new PopoverButton(() => new LoadPopover(manager, form))
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Text = ClickerStrings.Text("Load template", "Vorlage laden"),
                                },
                            }
                        }
                    },
                    status = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            manager.Changed += updateStatus;
            updateStatus();
        }

        protected override void Update()
        {
            base.Update();

            // read every frame like the overlay's own Create / Update button; nothing of the client is bound
            bool mayChangeRoom = client.Room == null || client.IsHost;

            SaveButton.Enabled.Value = !manager.Busy && !manager.Store.ReadOnly;
            LoadButton.Enabled.Value = !manager.Busy && mayChangeRoom && manager.Store.Templates.Count > 0;
        }

        private void updateStatus()
        {
            status.Clear();

            var lines = manager.Status.ToList();

            if (manager.Store.Templates.Count == 0 && lines.Count == 0)
            {
                lines.Add(new BanchosucksLobbyTemplateText.Line(ClickerStrings.Pick(
                    "Saves name, game type, queue mode, auto start, intro skipping and the maps. Never the password.",
                    "Speichert Name, Spielart, Warteschlange, automatischen Start, Intro-Überspringen und die Maps. Nie das Passwort.")));
            }

            foreach (var line in lines)
            {
                status.AddParagraph(line.Text, t =>
                {
                    if (line.Warning)
                        t.Colour = colours.Red1;
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (manager.IsNotNull())
                manager.Changed -= updateStatus;
        }

        public partial class PopoverButton : RoundedButton, IHasPopover
        {
            private readonly System.Func<Popover> createPopover;

            public PopoverButton(System.Func<Popover> createPopover)
            {
                this.createPopover = createPopover;
                Action = this.ShowPopover;
            }

            public Popover GetPopover() => createPopover();
        }

        /// <summary>
        /// Asks for the template's name and the room name pattern.
        /// </summary>
        public partial class SavePopover : OsuPopover
        {
            private readonly BanchosucksLobbyTemplateManager manager;
            private readonly IBanchosucksLobbyTemplateForm form;

            public OsuTextBox NameBox { get; private set; } = null!;
            public OsuTextBox RoomNameBox { get; private set; } = null!;
            public RoundedButton ConfirmButton { get; private set; } = null!;

            private OsuTextFlowContainer hint = null!;
            private string lastHintFor = "\0";

            public SavePopover(BanchosucksLobbyTemplateManager manager, IBanchosucksLobbyTemplateForm form)
            {
                this.manager = manager;
                this.form = form;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new FillFlowContainer
                {
                    Width = popover_width,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = ClickerStrings.Text("Name of the template", "Name der Vorlage"),
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        },
                        NameBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            LengthLimit = BanchosucksLobbyTemplateFile.MAX_NAME_LENGTH,
                            TabbableContentContainer = this,
                        },
                        new OsuSpriteText
                        {
                            Text = ClickerStrings.Text("Room name", "Raumname"),
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        },
                        RoomNameBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            LengthLimit = BanchosucksLobbyTemplateFile.MAX_ROOM_NAME_LENGTH,
                            TabbableContentContainer = this,
                            Text = form.BanchosucksReadSettings().Name,
                        },
                        hint = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        ConfirmButton = new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Text = ClickerStrings.Text("Save", "Speichern"),
                            Action = confirm,
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(NameBox));
            }

            protected override void Update()
            {
                base.Update();

                string name = BanchosucksLobbyTemplateFile.CleanName(NameBox.Text);

                ConfirmButton.Enabled.Value = name.Length > 0;

                if (name == lastHintFor)
                    return;

                lastHintFor = name;

                hint.Clear();
                hint.AddParagraph(ClickerStrings.Pick(
                    "In the room name, {user} becomes your name and {date} today's date when the template is loaded. The password is not saved.",
                    "Im Raumnamen wird beim Laden aus {user} dein Name und aus {date} das heutige Datum. Das Passwort wird nicht gespeichert."));

                if (manager.Store.Find(name) != null)
                    hint.AddParagraph(ClickerStrings.Pick("A template of this name exists and will be replaced.", "Eine Vorlage mit diesem Namen gibt es schon; sie wird ersetzt."));
            }

            private void confirm()
            {
                if (!ConfirmButton.Enabled.Value)
                    return;

                manager.Save(NameBox.Text, RoomNameBox.Text, form.BanchosucksReadSettings());
                Hide();
            }

            public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                // Enter must never reach the room settings behind the popover, where it creates or updates the room
                if (e.Action == GlobalAction.Select && !e.Repeat && State.Value == Visibility.Visible)
                {
                    confirm();
                    return true;
                }

                return base.OnPressed(e);
            }
        }

        /// <summary>
        /// Lists the templates; picking one shows what loading it will do and asks once.
        /// </summary>
        public partial class LoadPopover : OsuPopover
        {
            private readonly BanchosucksLobbyTemplateManager manager;
            private readonly IBanchosucksLobbyTemplateForm form;

            [Resolved]
            private MultiplayerClient client { get; set; } = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            private FillFlowContainer content = null!;

            /// <summary>
            /// The plan being shown for confirmation, once the server answered.
            /// </summary>
            public BanchosucksLobbyTemplatePlan? Plan { get; private set; }

            public RoundedButton? ApplyButton { get; private set; }

            public IEnumerable<TemplateRow> Rows => content.ChildrenOfType<TemplateRow>();

            public LoadPopover(BanchosucksLobbyTemplateManager manager, IBanchosucksLobbyTemplateForm form)
            {
                this.manager = manager;
                this.form = form;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = content = new FillFlowContainer
                {
                    Width = popover_width,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                };

                showList();
            }

            private void showList()
            {
                Plan = null;
                ApplyButton = null;

                content.Clear();
                content.Add(header(ClickerStrings.Pick("Load template", "Vorlage laden")));

                var templates = manager.Store.Templates;

                if (templates.Count == 0)
                {
                    content.Add(paragraph(ClickerStrings.Pick("No templates saved yet.", "Noch keine Vorlagen gespeichert.")));
                    return;
                }

                var rows = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    ChildrenEnumerable = templates.Select(t => new TemplateRow(t, !manager.Store.ReadOnly)
                    {
                        RequestLoad = ShowPreview,
                        RequestDelete = delete,
                    }),
                };

                // a long list scrolls instead of growing out of the screen
                if (templates.Count > 6)
                {
                    content.Add(new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 6 * (TemplateRow.HEIGHT + 4),
                        Child = rows,
                    });
                }
                else
                    content.Add(rows);
            }

            private void delete(BanchosucksLobbyTemplate template)
            {
                manager.Delete(template.Name);
                showList();
            }

            /// <summary>
            /// Asks the server about the template's maps and shows what loading it will change.
            /// </summary>
            public void ShowPreview(BanchosucksLobbyTemplate template)
            {
                Plan = null;
                ApplyButton = null;

                content.Clear();
                content.Add(header(ClickerStrings.Pick($"Load \"{template.Name}\"?", $"„{template.Name}“ laden?")));
                content.Add(paragraph(ClickerStrings.Pick("Checking the maps on the server…", "Die Maps werden auf dem Server geprüft…")));

                manager.Prepare(template, form.BanchosucksReadSettings(), plan =>
                {
                    // the popover may have been closed while the server was asked
                    if (IsDisposed || State.Value != Visibility.Visible)
                        return;

                    showPlan(plan);
                });
            }

            private void showPlan(BanchosucksLobbyTemplatePlan plan)
            {
                Plan = plan;

                content.Clear();
                content.Add(header(ClickerStrings.Pick($"Load \"{plan.Template.Name}\"?", $"„{plan.Template.Name}“ laden?")));

                var text = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                };

                foreach (var line in BanchosucksLobbyTemplateText.Describe(plan, client.Room == null))
                {
                    text.AddParagraph(line.Text, t =>
                    {
                        if (line.Warning)
                            t.Colour = colours.Red1;
                    });
                }

                content.Add(text);

                content.Add(new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 10),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable?[]
                        {
                            ApplyButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.Both,
                                Text = ClickerStrings.Text("Load", "Laden"),
                                // the action first: setting it enables the button
                                Action = Apply,
                                Enabled = { Value = plan.ChangesAnything },
                            },
                            null,
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.Both,
                                Text = ClickerStrings.Text("Back", "Zurück"),
                                BackgroundColour = colours.Gray5,
                                Action = showList,
                            },
                        }
                    }
                });
            }

            /// <summary>
            /// Applies the plan that is being shown.
            /// </summary>
            public void Apply()
            {
                if (Plan == null || !Plan.ChangesAnything)
                    return;

                manager.Apply(Plan, form);
                Hide();
            }

            public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                // Enter must never reach the room settings behind the popover, where it creates or updates the room
                if (e.Action == GlobalAction.Select && !e.Repeat && State.Value == Visibility.Visible)
                {
                    Apply();
                    return true;
                }

                return base.OnPressed(e);
            }

            private static Drawable header(string text) => new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = text,
            };

            private static Drawable paragraph(string text) => new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = text,
            };
        }

        /// <summary>
        /// One saved template in the list: name, summary, "Load" and "Delete" (which asks by turning into "Really?").
        /// </summary>
        public partial class TemplateRow : CompositeDrawable
        {
            public const float HEIGHT = 44;

            public readonly BanchosucksLobbyTemplate Template;

            public System.Action<BanchosucksLobbyTemplate>? RequestLoad;
            public System.Action<BanchosucksLobbyTemplate>? RequestDelete;

            public RoundedButton LoadButton { get; private set; } = null!;
            public RoundedButton DeleteButton { get; private set; } = null!;

            private readonly bool canDelete;
            private bool deleteArmed;

            public TemplateRow(BanchosucksLobbyTemplate template, bool canDelete)
            {
                Template = template;
                this.canDelete = canDelete;

                RelativeSizeAxes = Axes.X;
                Height = HEIGHT;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 80),
                        new Dimension(GridSizeMode.Absolute, 6),
                        new Dimension(GridSizeMode.Absolute, 90),
                    },
                    Content = new[]
                    {
                        new Drawable?[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding { Right = 8 },
                                Children = new Drawable[]
                                {
                                    new TruncatingSpriteText
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = Template.Name,
                                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                                    },
                                    new TruncatingSpriteText
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = BanchosucksLobbyTemplateText.Summary(Template),
                                        Font = OsuFont.GetFont(size: 12),
                                        Colour = colours.Gray9,
                                    },
                                }
                            },
                            LoadButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 32,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = ClickerStrings.Text("Load", "Laden"),
                                Action = () => RequestLoad?.Invoke(Template),
                            },
                            null,
                            DeleteButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 32,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = ClickerStrings.Text("Delete", "Löschen"),
                                BackgroundColour = colours.Red3,
                                // the action first: setting it enables the button
                                Action = delete,
                                Enabled = { Value = canDelete },
                            },
                        }
                    }
                };
            }

            private void delete()
            {
                if (!deleteArmed)
                {
                    deleteArmed = true;
                    DeleteButton.Text = ClickerStrings.Text("Really?", "Wirklich?");
                    return;
                }

                RequestDelete?.Invoke(Template);
            }
        }
    }
}

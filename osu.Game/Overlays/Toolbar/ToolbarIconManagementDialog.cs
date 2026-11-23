// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// Allows the user to choose which of the toolbar's optional icon buttons are displayed, and in which order.
    /// </summary>
    public partial class ToolbarIconManagementDialog : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        /// <summary>
        /// This dialog dims everything it is presented over, including the floating overlay column which holds the
        /// notifications - they would otherwise be the one thing left at full brightness behind it.
        /// </summary>
        public override bool DimFloatingOverlays => true;

        /// <summary>
        /// The toolbar being configured. Used to retrieve each button's icon and description.
        /// Assigned by the game.
        /// </summary>
        public Toolbar? Toolbar { get; init; }

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private MusicController? musicController { get; set; }

        private Bindable<string> layoutConfig = null!;

        private ToolbarButtonOrderList list = null!;

        /// <summary>
        /// Set while the configuration is being written on behalf of a row the user is interacting with.
        /// The list is left alone for those writes, as rebuilding it would dispose the very control in use.
        /// </summary>
        private bool suppressRebuild;

        private IDisposable? duckOperation;

        public ToolbarIconManagementDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.5f, 0.8f);

            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            layoutConfig = config.GetBindable<string>(OsuSetting.ToolbarButtons);

            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colourProvider.Background4,
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Horizontal = 12, Vertical = 10 },
                                    RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
                                    ColumnDimensions =
                                    [
                                        new Dimension(GridSizeMode.AutoSize),
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    ],
                                    Content = new Drawable[][]
                                    {
                                        [
                                            new SpriteIcon
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Size = new Vector2(24),
                                                Icon = FontAwesome.Solid.Cog,
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = ToolbarStrings.ManageToolbarIcons,
                                                Font = OsuFont.Style.Heading2,
                                                Padding = new MarginPadding { Horizontal = 10 },
                                            },
                                            new IconButton
                                            {
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                Icon = FontAwesome.Solid.Times,
                                                Colour = colourProvider.Content1,
                                                Scale = new Vector2(0.8f),
                                                Action = () => State.Value = Visibility.Hidden,
                                            },
                                        ],
                                    },
                                },
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colourProvider.Background4,
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding(10),
                                            Child = list = new ToolbarButtonOrderList
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                ButtonLookup = id => Toolbar?.GetButton(id),
                                                IsHidden = isHidden,
                                                HiddenChanged = setHidden,
                                                OrderChanged = applyOrder,
                                            },
                                        },
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Horizontal = 10, Bottom = 10 },
                                    Child = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 5),
                                        Children = new Drawable[]
                                        {
                                            new SettingsItemV2(new FormCheckBox
                                            {
                                                Caption = ToolbarStrings.AutoHideIcons,
                                                HintText = ToolbarStrings.AutoHideIconsDescription,
                                                Current = config.GetBindable<bool>(OsuSetting.ToolbarAutoHideButtons),
                                            })
                                            {
                                                ShowRevertToDefaultButton = false,
                                            },
                                            new DangerousSettingsButtonV2
                                            {
                                                Text = ToolbarStrings.RestoreDefaultOrder,
                                                Action = restoreDefaultOrder,
                                            },
                                            new DangerousSettingsButtonV2
                                            {
                                                Text = ToolbarStrings.RestoreDefaultSettings,
                                                Action = restoreDefaultSettings,
                                            },
                                        }
                                    }
                                }
                            },
                        }
                    }
                }
            };

            layoutConfig.BindValueChanged(_ =>
            {
                if (!suppressRebuild)
                    rebuild();
            });

            rebuild();
        }

        /// <summary>
        /// Rebuilds the displayed list from the current configuration.
        /// </summary>
        private void rebuild()
        {
            list.SetItems(ToolbarButtonLayout.Parse(layoutConfig.Value).Order);
        }

        /// <summary>
        /// Whether the icon with the given identifier is currently hidden by the user.
        /// </summary>
        private bool isHidden(string id) => ToolbarButtonLayout.Parse(layoutConfig.Value).IsHidden(id);

        /// <summary>
        /// Records the user toggling whether an icon is displayed. The list is not rebuilt, as the row whose
        /// switch was toggled would be disposed mid-interaction.
        /// </summary>
        private void setHidden(string id, bool hidden)
        {
            ToolbarButtonLayout layout = ToolbarButtonLayout.Parse(layoutConfig.Value);
            layout.SetHidden(id, hidden);
            writeLayout(layout, fromRowEdit: true);
        }

        /// <summary>
        /// Records the order the user dragged the rows into. The list is not rebuilt, as it is already displaying
        /// the order being written, and rebuilding would disrupt the row the user has just let go of.
        /// </summary>
        private void applyOrder()
        {
            ToolbarButtonLayout layout = ToolbarButtonLayout.Parse(layoutConfig.Value);
            layout.Order = new List<string>(list.Items);
            writeLayout(layout, fromRowEdit: true);
        }

        private void restoreDefaultOrder()
        {
            ToolbarButtonLayout layout = ToolbarButtonLayout.Parse(layoutConfig.Value);
            layout.Order = new List<string>(ToolbarButtonLayout.DEFAULT_ORDER);
            writeLayout(layout);
        }

        private void restoreDefaultSettings()
        {
            config.SetValue(OsuSetting.ToolbarAutoHideButtons, false);
            writeLayout(ToolbarButtonLayout.CreateDefault());
        }

        /// <summary>
        /// Writes a layout to the configuration, rebuilding the displayed list so that it reflects the change.
        /// </summary>
        /// <param name="layout">The layout to write.</param>
        /// <param name="fromRowEdit">
        /// Whether the change originates from a row the user is interacting with. If so, the list is left
        /// untouched, as rebuilding it would dispose the very control the user is using.
        /// </param>
        private void writeLayout(ToolbarButtonLayout layout, bool fromRowEdit = false)
        {
            suppressRebuild = fromRowEdit;
            layoutConfig.Value = layout.Serialize();
            suppressRebuild = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            duckOperation?.Dispose();
        }

        protected override void PopIn()
        {
            // the buttons only have their icon and description once the toolbar has finished loading,
            // so refresh the list every time this dialog is opened.
            rebuild();

            duckOperation = musicController?.Duck(new DuckParameters
            {
                DuckVolumeTo = 1,
                DuckDuration = 100,
                RestoreDuration = 100,
            });

            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            duckOperation?.Dispose();

            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);
        }
    }
}

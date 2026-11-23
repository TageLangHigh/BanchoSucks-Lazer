// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Rulesets;
using osu.Framework.Input.Bindings;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osuTK.Input;

namespace osu.Game.Overlays.Toolbar
{
    public partial class Toolbar : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        public const float HEIGHT = 40;
        public const float TOOLTIP_HEIGHT = 30;

        /// <summary>
        /// Whether the user hid this <see cref="Toolbar"/> with <see cref="GlobalAction.ToggleToolbar"/>.
        /// In this state, automatic toggles should not occur, respecting the user's preference to have no toolbar.
        /// </summary>
        private bool hiddenByUser;

        public Action OnHome;

        /// <summary>
        /// Invoked when the user asks to open the toolbar icon management dialog (from a context menu).
        /// Assigned by the game once the dialog has been created, as the toolbar is constructed before it.
        /// </summary>
        public Action ShowIconManagement;

        private ToolbarUserButton userButton;
        private ToolbarRulesetSelector rulesetSelector;
        private ToolbarOverflowButton overflowButton;
        private FillFlowContainer rightButtonFlow;
        private Container leftButtonGroup;

        /// <summary>
        /// Every icon button the user is allowed to hide and reorder, keyed by the identifier the button carries.
        /// </summary>
        private readonly Dictionary<string, ToolbarButton> candidateButtons = new Dictionary<string, ToolbarButton>();

        /// <summary>
        /// The buttons to the right of the optional ones, which are always displayed.
        /// </summary>
        private Drawable[] permanentRightButtons = Array.Empty<Drawable>();

        /// <summary>
        /// Buttons which are hidden by the user's configuration but are currently pinned to the toolbar
        /// because they were opened from the hidden icon panel. They go back to being hidden once their
        /// overlay closes again.
        /// </summary>
        private readonly HashSet<string> temporarilyRevealed = new HashSet<string>();

        /// <summary>
        /// Used to ignore the initial hidden state of overlays for <see cref="temporarilyRevealed"/> buttons.
        /// </summary>
        private readonly HashSet<string> sawOverlayVisible = new HashSet<string>();

        /// <summary>
        /// Buttons hidden automatically because there is not enough room to show all rulesets, in configured order.
        /// </summary>
        private readonly List<string> autoHidden = new List<string>();

        private Bindable<string> layoutConfig;
        private Bindable<bool> autoHideConfig;

        /// <summary>
        /// The user's current order/hiding configuration, as last read from <see cref="layoutConfig"/>.
        /// </summary>
        private ToolbarButtonLayout layout = ToolbarButtonLayout.CreateDefault();

        /// <summary>
        /// Forces the automatic hiding pass to run again on the next update.
        /// </summary>
        private bool autoHideInvalidated = true;

        private float lastWidth = -1;
        private float lastLeftWidth = -1;
        private float lastRulesetWidth = -1;
        private float lastPermanentWidth = -1;
        private int lastUserHiddenCount = -1;

        private const double transition_time = 500;

        protected readonly IBindable<OverlayActivation> OverlayActivationMode = new Bindable<OverlayActivation>(OverlayActivation.All);

        // Toolbar and its components need keyboard input even when hidden.
        public override bool PropagateNonPositionalInputSubTree => OverlayActivationMode.Value != OverlayActivation.Disabled;

        public Toolbar()
        {
            RelativeSizeAxes = Axes.X;
            Size = new Vector2(1, HEIGHT);
            AlwaysPresent = true;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // this only needed to be set for the initial LoadComplete/Update, so layout completes and gets buttons in a state they can correctly handle keyboard input for hotkeys.
            AlwaysPresent = false;

            updateAutoHide();
        }

        // the toolbar occupies the entire top of the screen and blocks positional input so that clicks in its empty
        // areas do not reach whatever is behind it. right-clicks are exempt from this, as they need to reach the
        // context menu container which wraps the toolbar in order to display the icon buttons' context menus.
        protected override bool OnMouseDown(MouseDownEvent e) => e.Button != MouseButton.Right && base.OnMouseDown(e);

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame osuGame, OsuConfigManager config)
        {
            ToolbarBackground background;
            HoverInterceptor interceptor;

            // every button carries its own identifier, so it does not have to be tracked alongside it.
            var candidates = new ToolbarButton[]
            {
                // Banchosucks: news, changelog and wiki are osu!-web features our server does not provide
                new ToolbarRankingsButton { ID = ToolbarButtonLayout.RANKINGS },
                new ToolbarBeatmapListingButton { ID = ToolbarButtonLayout.BEATMAP_LISTING },
                new ToolbarChatButton { ID = ToolbarButtonLayout.CHAT },
                new ToolbarSocialButton { ID = ToolbarButtonLayout.SOCIAL },
            };

            foreach (ToolbarButton button in candidates)
            {
                candidateButtons[button.ID!] = button;
                button.ContextMenuItemsProvider = () => createContextMenuItems(button.ID);
            }

            overflowButton = new ToolbarOverflowButton
            {
                CreatePopover = () => new ToolbarOverflowPopover(this),
                ContextMenuItemsProvider = () => new MenuItem[]
                {
                    new OsuMenuItem(ToolbarStrings.ManageToolbarIcons, MenuItemType.Standard, () => ShowIconManagement?.Invoke()),
                },
            };

            permanentRightButtons = new Drawable[]
            {
                new ToolbarMusicButton(),
                userButton = new ToolbarUserButton(),
                new ToolbarClock(),
                // Banchosucks: connection state and response time of our server, right of the clock. Not one of the
                // optional buttons: it is narrow, always there, and counted with the permanent width by the auto-hide.
                new BanchosucksServerStatusIndicator(),
                new ToolbarNotificationButton(),
            };

            var rightButtons = new List<Drawable>();
            rightButtons.AddRange(candidates);
            rightButtons.Add(overflowButton);
            rightButtons.AddRange(permanentRightButtons);

            Children = new Drawable[]
            {
                background = new ToolbarBackground(),
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize)
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            leftButtonGroup = new Container
                            {
                                Name = "Left buttons",
                                RelativeSizeAxes = Axes.Y,
                                AutoSizeAxes = Axes.X,
                                Depth = float.MinValue,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        Colour = OsuColour.Gray(0.1f),
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    new FillFlowContainer
                                    {
                                        Direction = FillDirection.Horizontal,
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Children = new Drawable[]
                                        {
                                            new ToolbarSettingsButton(),
                                            new ToolbarHomeButton
                                            {
                                                Action = () => OnHome?.Invoke()
                                            },
                                        },
                                    },
                                }
                            },
                            new Container
                            {
                                Name = "Ruleset selector",
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new OsuScrollContainer(Direction.Horizontal)
                                    {
                                        ScrollbarVisible = false,
                                        RelativeSizeAxes = Axes.Both,
                                        Masking = false,
                                        Children = new Drawable[]
                                        {
                                            rulesetSelector = new ToolbarRulesetSelector()
                                        }
                                    },
                                    new Box
                                    {
                                        Colour = ColourInfo.GradientHorizontal(OsuColour.Gray(0.1f).Opacity(0), OsuColour.Gray(0.1f)),
                                        Width = 50,
                                        RelativeSizeAxes = Axes.Y,
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                    },
                                }
                            },
                            new Container
                            {
                                Name = "Right buttons",
                                RelativeSizeAxes = Axes.Y,
                                AutoSizeAxes = Axes.X,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        Colour = OsuColour.Gray(0.1f),
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    rightButtonFlow = new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Direction = FillDirection.Horizontal,
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Children = rightButtons.ToArray()
                                    },
                                }
                            },
                        },
                    }
                },
                interceptor = new HoverInterceptor
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            ((IBindable<bool>)background.ShowGradient).BindTo(interceptor.ReceivedHover);

            if (config != null)
            {
                layoutConfig = config.GetBindable<string>(OsuSetting.ToolbarButtons);
                autoHideConfig = config.GetBindable<bool>(OsuSetting.ToolbarAutoHideButtons);

                layoutConfig.BindValueChanged(_ => applyLayout(), true);
                autoHideConfig.BindValueChanged(_ => autoHideInvalidated = true, true);
            }
            else
                applyLayout();

            if (osuGame != null)
                OverlayActivationMode.BindTo(osuGame.OverlayActivationMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rulesetSelector.Current.BindTo(ruleset);

            foreach (ToolbarButton button in candidateButtons.Values)
            {
                if (button is ToolbarOverlayToggleButton toggle)
                    toggle.OverlayState.BindValueChanged(state => overlayStateChanged(button.ID, state));
            }

            // the buttons are only fully loaded (and therefore measurable) at this point.
            autoHideInvalidated = true;
        }

        #region Public API

        // the rest of this file predates nullable annotation, so it is left unannotated as a whole. the lookup
        // below is the one member which genuinely has no result at times, so it is annotated on its own - callers
        // which are annotated (such as the icon management dialog) then have to deal with the absence.
#nullable enable

        /// <summary>
        /// Retrieves the optional icon button with the given stable identifier, or <c>null</c> if there is no such button
        /// (or the toolbar has not finished loading yet).
        /// </summary>
        public ToolbarButton? GetButton(string id) => candidateButtons.GetValueOrDefault(id);

        /// <summary>
        /// The identifiers of the buttons which are currently not shown in the toolbar (either because the user hid
        /// them, or because there is not enough room for them), in their configured order.
        /// </summary>
        public IEnumerable<string> HiddenButtonIds => layout.Order.Where(isEffectivelyHidden);

        /// <summary>
        /// The buttons which are currently not shown in the toolbar, in their configured order.
        /// </summary>
        public IEnumerable<ToolbarButton> HiddenButtons => HiddenButtonIds.Select(GetButton).OfType<ToolbarButton>();

#nullable disable

        /// <summary>
        /// Hides or shows one of the optional buttons, persisting the change to the user's configuration.
        /// </summary>
        public void SetHidden(string id, bool hidden)
        {
            if (layoutConfig == null)
                return;

            ToolbarButtonLayout updated = layout.Clone();
            updated.SetHidden(id, hidden);
            layoutConfig.Value = updated.Serialize();
        }

        /// <summary>
        /// Pins a hidden button to the toolbar temporarily, opening its overlay if it is not already open.
        /// The button returns to being hidden once its overlay is closed again.
        /// </summary>
        public void RevealTemporarily(string id)
        {
            if (!candidateButtons.TryGetValue(id, out ToolbarButton button))
                return;

            temporarilyRevealed.Add(id);

            if (button is ToolbarOverlayToggleButton toggle && toggle.OverlayState.Value == Visibility.Visible)
            {
                // the overlay is already open, so it just needs to stay pinned until it closes again.
                sawOverlayVisible.Add(id);
            }
            else
                button.TriggerClick();

            autoHideInvalidated = true;
            updateCollapsedState();
        }

        #endregion

        #region Layout

        private MenuItem[] createContextMenuItems(string id)
        {
            bool userHidden = layout.Hidden.Contains(id);

            return new MenuItem[]
            {
                new OsuMenuItem(userHidden ? ToolbarStrings.ShowIcon : ToolbarStrings.HideIcon, MenuItemType.Highlighted, () => SetHidden(id, !userHidden)),
                new OsuMenuItem(ToolbarStrings.ManageToolbarIcons, MenuItemType.Standard, () => ShowIconManagement?.Invoke()),
            };
        }

        /// <summary>
        /// Reads the current configuration and applies the resulting order and visibility to the buttons.
        /// </summary>
        private void applyLayout()
        {
            if (rightButtonFlow == null)
                return;

            layout = layoutConfig != null ? ToolbarButtonLayout.Parse(layoutConfig.Value) : ToolbarButtonLayout.CreateDefault();

            int position = 0;

            foreach (string id in layout.Order)
            {
                if (candidateButtons.TryGetValue(id, out ToolbarButton button))
                    rightButtonFlow.SetLayoutPosition(button, position++);
            }

            // the overflow button always sits immediately after the optional buttons, i.e. next to the area they occupy.
            rightButtonFlow.SetLayoutPosition(overflowButton, position++);

            foreach (Drawable permanent in permanentRightButtons)
                rightButtonFlow.SetLayoutPosition(permanent, position++);

            autoHideInvalidated = true;
            updateCollapsedState();
        }

        /// <summary>
        /// Recomputes which buttons to hide automatically, but only when one of the inputs it depends on changed since
        /// the last time it ran. All inputs are measured from the layout, and hiding buttons never changes them
        /// (the ruleset selector is measured at its natural width), so this cannot feed back into itself.
        /// </summary>
        private void updateAutoHide()
        {
            if (rightButtonFlow == null || overflowButton == null)
                return;

            float width = DrawWidth;
            float leftWidth = leftButtonGroup.DrawWidth;
            // the ruleset selector auto-sizes to its contents, so this is the width it would like to occupy.
            float rulesetWidth = rulesetSelector.DrawWidth;

            float permanentWidth = permanentRightButtons.Sum(button => button.DrawWidth);

            int userHiddenCount = layout.Hidden.Count;

            // skip until layout has settled, otherwise everything could be hidden for a frame on startup.
            if (width <= 0 || rulesetWidth <= 0)
                return;

            if (!autoHideInvalidated
                && width == lastWidth
                && leftWidth == lastLeftWidth
                && rulesetWidth == lastRulesetWidth
                && permanentWidth == lastPermanentWidth
                && userHiddenCount == lastUserHiddenCount)
                return;

            lastWidth = width;
            lastLeftWidth = leftWidth;
            lastRulesetWidth = rulesetWidth;
            lastPermanentWidth = permanentWidth;
            lastUserHiddenCount = userHiddenCount;
            autoHideInvalidated = false;

            autoHidden.Clear();

            if (autoHideConfig != null && autoHideConfig.Value)
            {
                // only buttons the user did not hide themselves can be taken away, starting from the left.
                List<string> visible = layout.Order.Where(id => !layout.Hidden.Contains(id)).ToList();

                for (int hideCount = 0; hideCount <= visible.Count; hideCount++)
                {
                    if (requiredWidth(leftWidth, rulesetWidth, permanentWidth, visible.Count - hideCount, layout.Hidden.Count + hideCount) <= width)
                    {
                        autoHidden.AddRange(visible.Take(hideCount));
                        break;
                    }

                    // nothing fits - hide everything which is left.
                    if (hideCount == visible.Count)
                        autoHidden.AddRange(visible);
                }
            }

            updateCollapsedState();
        }

        /// <summary>
        /// The total width the toolbar would need with the given number of optional buttons and hidden buttons displayed.
        /// </summary>
        private static float requiredWidth(float leftWidth, float rulesetWidth, float permanentWidth, int visibleCount, int hiddenCount)
        {
            float width = leftWidth + rulesetWidth + permanentWidth + visibleCount * ToolbarButtonLayout.BUTTON_WIDTH;

            // the overflow button, as well as the room kept free for an icon which is shown temporarily,
            // only start taking up space once something is hidden.
            if (hiddenCount > 0)
                width += ToolbarButtonLayout.BUTTON_WIDTH * 2;

            return width;
        }

        private void updateCollapsedState()
        {
            if (overflowButton == null)
                return;

            foreach (ToolbarButton button in candidateButtons.Values)
                button.Collapsed = isEffectivelyHidden(button.ID);

            // the overflow button is only useful while at least one button is hidden.
            overflowButton.Collapsed = layout.Hidden.Count + autoHidden.Count == 0;
        }

        private bool isEffectivelyHidden(string id)
        {
            if (temporarilyRevealed.Contains(id))
                return false;

            return layout.Hidden.Contains(id) || autoHidden.Contains(id);
        }

        private void overlayStateChanged(string id, ValueChangedEvent<Visibility> state)
        {
            if (!temporarilyRevealed.Contains(id))
                return;

            if (state.NewValue == Visibility.Visible)
            {
                sawOverlayVisible.Add(id);
                return;
            }

            // the overlay has been closed after having been opened from the hidden icon panel,
            // so the button goes back to being hidden.
            if (sawOverlayVisible.Remove(id))
            {
                temporarilyRevealed.Remove(id);
                autoHideInvalidated = true;
                updateCollapsedState();
            }
        }

        #endregion

        public partial class ToolbarBackground : Container
        {
            public Bindable<bool> ShowGradient { get; } = new BindableBool();

            private readonly Box gradientBackground;

            public ToolbarBackground()
            {
                RelativeSizeAxes = Axes.Both;
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = OsuColour.Gray(0.1f),
                    },
                    gradientBackground = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.BottomLeft,
                        Alpha = 0,
                        Height = 80,
                        Colour = ColourInfo.GradientVertical(
                            OsuColour.Gray(0f).Opacity(0.7f), OsuColour.Gray(0).Opacity(0)),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ShowGradient.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                if (ShowGradient.Value)
                    gradientBackground.FadeIn(2500, Easing.OutQuint);
                else
                    gradientBackground.FadeOut(200, Easing.OutQuint);
            }
        }

        /// <summary>
        /// Whenever the mouse cursor is within the bounds of the toolbar, we want the background gradient to show, for toolbar button descriptions to be legible.
        /// Unfortunately we also need to ensure that the toolbar buttons handle hover, to prevent the possibility of multiple descriptions being shown
        /// due to hover events passing through multiple buttons.
        /// This drawable is a workaround, that when placed front-most in the toolbar, allows to see whether hover events have been propagated through it without handling them.
        /// </summary>
        private partial class HoverInterceptor : Drawable
        {
            public IBindable<bool> ReceivedHover => receivedHover;
            private readonly Bindable<bool> receivedHover = new BindableBool();

            protected override bool OnHover(HoverEvent e)
            {
                receivedHover.Value = true;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                receivedHover.Value = false;
                base.OnHoverLost(e);
            }
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state)
        {
            bool blockShow = hiddenByUser || OverlayActivationMode.Value == OverlayActivation.Disabled;

            if (state.NewValue == Visibility.Visible && blockShow)
            {
                State.Value = Visibility.Hidden;
                return;
            }

            base.UpdateState(state);
        }

        protected override void PopIn()
        {
            this.MoveToY(0, transition_time, Easing.OutQuint);
            this.FadeIn(transition_time / 4, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            userButton.StateContainer?.Hide();

            this.MoveToY(-DrawSize.Y, transition_time, Easing.OutQuint);
            this.FadeOut(transition_time, Easing.InQuint);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                return false;

            switch (e.Action)
            {
                case GlobalAction.ToggleToolbar:
                    hiddenByUser = State.Value == Visibility.Visible; // set before toggling to allow the operation to always succeed.
                    ToggleVisibility();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}

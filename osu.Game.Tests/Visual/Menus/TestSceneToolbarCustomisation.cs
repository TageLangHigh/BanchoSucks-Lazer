// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Toolbar;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// Covers hiding, reordering and automatically hiding the toolbar's optional icon buttons.
    /// </summary>
    [TestFixture]
    public partial class TestSceneToolbarCustomisation : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        // the toolbar includes a notification button and a music button, which require these to be present.
        [Cached]
        private readonly NowPlayingOverlay nowPlayingOverlay = new NowPlayingOverlay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = Toolbar.HEIGHT,
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs<INotificationOverlay>(new TestNotificationOverlay());
        }

        private Toolbar toolbar = null!;

        /// <summary>
        /// The dialog examined by the tests which open it. Created by those tests only.
        /// </summary>
        private ToolbarIconManagementDialog dialog = null!;

        /// <summary>
        /// Stand-ins for the overlays the toolbar buttons would normally open. They allow the temporary reveal
        /// lifecycle (and the tooltip metadata the hidden icon panel reads) to be exercised without the real overlays.
        /// </summary>
        private readonly Dictionary<string, TestOverlay> stubOverlays = new Dictionary<string, TestOverlay>();

        /// <summary>
        /// How far in from its left edge a row is clicked when its body is the target. Measured from the edge
        /// rather than from the middle, so that it does not have to assume how wide the dialog ends up.
        /// </summary>
        private const float row_body_click_offset = 60;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            // the toolbar derives its layout from configuration, so leftovers from a previous test must not leak in.
            config.SetValue(OsuSetting.ToolbarButtons, string.Empty);
            config.SetValue(OsuSetting.ToolbarAutoHideButtons, false);
            stubOverlays.Clear();

            Remove(nowPlayingOverlay, false);

            // both of these need to be full-screen *and* ancestors of the toolbar, for context menus and popovers
            // to know how much room there is around the target they are requested for. This mirrors what the game does.
            Children = new Drawable[]
            {
                nowPlayingOverlay,
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = toolbar = new Toolbar
                        {
                            State = { Value = Visibility.Visible },
                        },
                    },
                },
            };
        });

        [Test]
        public void TestHidingButtonCollapsesIt()
        {
            AddStep("hide news button", () => toolbar.SetHidden(ToolbarButtonLayout.RANKINGS, true));

            // the button shrinks into nothing rather than disappearing outright. this is sampled on the step
            // directly following the one which starts the collapse, as a step spans longer than the transition
            // and would otherwise only ever observe its end state.
            AddUntilStep("news button seen mid-collapse", () => button(ToolbarButtonLayout.RANKINGS).DrawWidth,
                () => Is.GreaterThan(0).And.LessThan(ToolbarButtonLayout.BUTTON_WIDTH));

            AddUntilStep("news button collapsed", () => button(ToolbarButtonLayout.RANKINGS).Collapsed);
            AddUntilStep("news button takes up no space", () => button(ToolbarButtonLayout.RANKINGS).DrawWidth, () => Is.LessThan(1));

            AddAssert("overflow button shown", () => !overflowButton().Collapsed);
            AddAssert("only news button reported hidden", () => toolbar.HiddenButtonIds, () => Is.EqualTo(new[] { ToolbarButtonLayout.RANKINGS }));

            // hiding must only affect the toolbar's appearance, so the button has to keep receiving the
            // non-positional input which triggers its hotkey.
            AddAssert("hidden button keeps receiving non-positional input", () =>
                InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Contains(button(ToolbarButtonLayout.RANKINGS)));

            AddStep("show news button again", () => toolbar.SetHidden(ToolbarButtonLayout.RANKINGS, false));

            // ...and grows back out the same way.
            AddUntilStep("news button seen mid-expand", () => button(ToolbarButtonLayout.RANKINGS).DrawWidth,
                () => Is.GreaterThan(0).And.LessThan(ToolbarButtonLayout.BUTTON_WIDTH));

            AddUntilStep("news button restored", () => !button(ToolbarButtonLayout.RANKINGS).Collapsed);
            AddUntilStep("news button takes up space again", () => button(ToolbarButtonLayout.RANKINGS).DrawWidth,
                () => Is.EqualTo(ToolbarButtonLayout.BUTTON_WIDTH).Within(0.5f));

            AddAssert("overflow button hidden", () => overflowButton().Collapsed);
        }

        [Test]
        public void TestTemporarilyRevealedButtonReturnsToHidden()
        {
            AddStep("install overlays", installStubOverlays);
            AddStep("hide wiki button", () => toolbar.SetHidden(ToolbarButtonLayout.CHAT, true));
            AddUntilStep("wiki button collapsed", () => button(ToolbarButtonLayout.CHAT).Collapsed);

            AddStep("reveal wiki button", () => toolbar.RevealTemporarily(ToolbarButtonLayout.CHAT));
            AddAssert("wiki button pinned to the toolbar", () => !button(ToolbarButtonLayout.CHAT).Collapsed);
            AddAssert("wiki overlay opened", () => toggle(ToolbarButtonLayout.CHAT).OverlayState.Value, () => Is.EqualTo(Visibility.Visible));

            AddStep("close wiki overlay", () => stubOverlays[ToolbarButtonLayout.CHAT].Hide());
            AddUntilStep("wiki button hidden again", () => button(ToolbarButtonLayout.CHAT).Collapsed);
            AddAssert("wiki overlay closed", () => toggle(ToolbarButtonLayout.CHAT).OverlayState.Value, () => Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void TestOverflowPopoverListsHiddenIcons()
        {
            AddStep("install overlays", installStubOverlays);
            AddStep("hide wiki button", () => toolbar.SetHidden(ToolbarButtonLayout.CHAT, true));
            AddUntilStep("wiki button collapsed", () => button(ToolbarButtonLayout.CHAT).Collapsed);

            ToolbarOverflowPopover? popover = null;

            AddStep("click overflow button", () => overflowButton().TriggerClick());

            AddUntilStep("popover shown", () =>
            {
                popover = this.ChildrenOfType<ToolbarOverflowPopover>().SingleOrDefault();
                return popover?.State.Value == Visibility.Visible;
            });

            // one row for the hidden icon, plus the always-present entry for the management dialog.
            AddAssert("popover has one row per hidden icon", () => popover!.ChildrenOfType<OsuClickableContainer>().Count(), () => Is.EqualTo(2));
            AddAssert("popover names the hidden icon", () =>
                popover!.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == stubOverlays[ToolbarButtonLayout.CHAT].Title.ToString()));

            AddStep("select the hidden icon", () => popover!.ChildrenOfType<OsuClickableContainer>().First().TriggerClick());
            AddUntilStep("wiki overlay opened", () => toggle(ToolbarButtonLayout.CHAT).OverlayState.Value == Visibility.Visible);
            AddUntilStep("wiki button pinned to the toolbar", () => !button(ToolbarButtonLayout.CHAT).Collapsed);
            AddUntilStep("popover hidden", () => popover!.State.Value == Visibility.Hidden);

            AddStep("close wiki overlay", () => stubOverlays[ToolbarButtonLayout.CHAT].Hide());
            AddUntilStep("wiki button hidden again", () => button(ToolbarButtonLayout.CHAT).Collapsed);
        }

        [Test]
        public void TestContextMenuHidesIcon()
        {
            AddStep("move mouse over wiki button", () => InputManager.MoveMouseTo(button(ToolbarButtonLayout.CHAT)));
            AddUntilStep("wiki button hovered", () => button(ToolbarButtonLayout.CHAT).IsHovered);

            AddStep("right-click wiki button", () => InputManager.Click(MouseButton.Right));
            AddUntilStep("context menu opened", () => contextMenu().State == MenuState.Open);

            AddAssert("context menu offers hiding the icon", () => findMenuItem(ToolbarStrings.HideIcon) != null);
            AddAssert("context menu offers managing icons", () => findMenuItem(ToolbarStrings.ManageToolbarIcons) != null);

            AddStep("select hide", () => findMenuItem(ToolbarStrings.HideIcon)!.Action.Value?.Invoke());
            AddUntilStep("wiki button collapsed", () => button(ToolbarButtonLayout.CHAT).Collapsed);
        }

        [Test]
        public void TestConfiguredOrderIsApplied()
        {
            AddStep("move social button to the front", () =>
            {
                ToolbarButtonLayout layout = ToolbarButtonLayout.CreateDefault();
                layout.Order.Remove(ToolbarButtonLayout.SOCIAL);
                layout.Order.Insert(0, ToolbarButtonLayout.SOCIAL);
                config.SetValue(OsuSetting.ToolbarButtons, layout.Serialize());
            });

            AddUntilStep("social button is left-most", () =>
            {
                float socialX = positionInToolbar(button(ToolbarButtonLayout.SOCIAL));

                return ToolbarButtonLayout.DEFAULT_ORDER
                                          .Where(id => id != ToolbarButtonLayout.SOCIAL)
                                          .All(id => positionInToolbar(button(id)) > socialX);
            });
        }

        [Test]
        public void TestReorderingIsAnimated()
        {
            AddStep("create icon management dialog", createDialog);
            AddUntilStep("dialog loaded", () => dialog.IsLoaded);

            // the dialog can only be shown once it has loaded, as it builds its list while entering.
            AddStep("open icon management", () => dialog.Show());
            AddUntilStep("dialog shown", () => dialog.State.Value, () => Is.EqualTo(Visibility.Visible));

            AddUntilStep("every optional icon listed", () => dialog.ChildrenOfType<ToolbarButtonOrderListItem>().Count(),
                () => Is.EqualTo(ToolbarButtonLayout.DEFAULT_ORDER.Length));

            // the dialog scales up as it enters, which moves its contents. the rows have to be settled in place
            // before the mouse can be aimed at one of their drag handles.
            AddUntilStep("dialog finished entering", () => dialog.Scale.X, () => Is.EqualTo(1f).Within(0.001f));

            float newsStartY = 0;
            float changelogStartY = 0;

            AddStep("record row positions", () =>
            {
                newsStartY = orderRow(ToolbarButtonLayout.RANKINGS).Y;
                changelogStartY = orderRow(ToolbarButtonLayout.BEATMAP_LISTING).Y;
            });

            // the move and the press have to be performed together: a step spans longer than the dialog's enter
            // animation, so a position sampled a step before the press would no longer be over the handle.
            AddStep("grab the news row", () =>
            {
                InputManager.MoveMouseTo(dragHandle(ToolbarButtonLayout.RANKINGS));
                InputManager.PressButton(MouseButton.Left);
            });

            // the row is only picked up once the mouse moves while the button is held, and it is nudged in place
            // so that picking it up does not immediately displace anything.
            AddStep("pick the news row up", () => InputManager.MoveMouseTo(dragHandle(ToolbarButtonLayout.RANKINGS), new Vector2(0, 20)));
            AddUntilStep("news row being dragged", () => orderRow(ToolbarButtonLayout.RANKINGS).IsDragged);

            AddStep("drag below the changelog row", () => InputManager.MoveMouseTo(dragHandle(ToolbarButtonLayout.BEATMAP_LISTING), new Vector2(0, 5)));

            // the row that the dragged one displaced is the one the swap has to be read from, so it is the one
            // which is called out - and it slides into the position it was pushed into instead of jumping there.
            // both are read in a single step, as a step spans longer than the swap animation itself.
            AddUntilStep("displaced row called out mid-slide", () =>
            {
                ToolbarButtonOrderListItem row = orderRow(ToolbarButtonLayout.BEATMAP_LISTING);

                return row.SwapHighlightVisible
                       && row.Y > newsStartY
                       && row.Y < changelogStartY;
            });

            // ...and the call-out outlasts the movement, so that it still reads once the row has settled.
            AddAssert("displaced row still called out once settled", () => orderRow(ToolbarButtonLayout.BEATMAP_LISTING).SwapHighlightVisible);
            AddAssert("displaced row settled into place", () => orderRow(ToolbarButtonLayout.BEATMAP_LISTING).Y, () => Is.EqualTo(newsStartY).Within(0.5f));

            AddStep("end drag", () => InputManager.ReleaseButton(MouseButton.Left));

            AddUntilStep("dragged row took its place", () => orderRow(ToolbarButtonLayout.RANKINGS).Y, () => Is.EqualTo(changelogStartY).Within(0.5f));

            AddAssert("new order persisted", () =>
            {
                List<string> order = ToolbarButtonLayout.Parse(config.Get<string>(OsuSetting.ToolbarButtons)).Order;

                return order.IndexOf(ToolbarButtonLayout.BEATMAP_LISTING) < order.IndexOf(ToolbarButtonLayout.RANKINGS);
            });
        }

        [Test]
        public void TestRestoringDefaultsUpdatesList()
        {
            // unlike a drag or a switch toggle, the restore buttons change the configuration without the list
            // having had any say in it, so the list has to be rebuilt for them - otherwise it keeps displaying
            // the order and hidden states they have just discarded.
            ToolbarButtonLayout reordered = ToolbarButtonLayout.CreateDefault();
            reordered.Order.Remove(ToolbarButtonLayout.RANKINGS);
            reordered.Order.Add(ToolbarButtonLayout.RANKINGS);
            reordered.SetHidden(ToolbarButtonLayout.CHAT, true);

            AddStep("configure a non-default layout", () => config.SetValue(OsuSetting.ToolbarButtons, reordered.Serialize()));

            AddStep("create icon management dialog", createDialog);
            AddUntilStep("dialog loaded", () => dialog.IsLoaded);
            AddStep("open icon management", () => dialog.Show());
            AddUntilStep("dialog shown", () => dialog.State.Value, () => Is.EqualTo(Visibility.Visible));

            AddUntilStep("list shows the configured order", displayedOrder, () => Is.EqualTo(reordered.Order));
            AddUntilStep("list shows the configured hidden state", () => rowReportsHidden(ToolbarButtonLayout.CHAT), () => Is.True);

            AddStep("click restore default order", () => restoreButton(ToolbarStrings.RestoreDefaultOrder).TriggerClick());

            AddUntilStep("list follows the restored order", displayedOrder, () => Is.EqualTo(ToolbarButtonLayout.DEFAULT_ORDER));
            AddAssert("restored order was persisted", () =>
                ToolbarButtonLayout.Parse(config.Get<string>(OsuSetting.ToolbarButtons)).Order, () => Is.EqualTo(ToolbarButtonLayout.DEFAULT_ORDER));
            AddAssert("restoring the order left the hidden icons alone", () => rowReportsHidden(ToolbarButtonLayout.CHAT));

            // the hidden state is only discarded by the second button, and each row reports its own, so it is the
            // one which shows the list having picked that change up as well.
            AddStep("click restore default settings", () => restoreButton(ToolbarStrings.RestoreDefaultSettings).TriggerClick());

            AddUntilStep("list follows the restored hidden state", () => rowReportsHidden(ToolbarButtonLayout.CHAT), () => Is.False);
            AddAssert("restored hidden state was persisted", () =>
                ToolbarButtonLayout.Parse(config.Get<string>(OsuSetting.ToolbarButtons)).Hidden, () => Is.Empty);
        }

        [Test]
        public void TestClickingRowTogglesIcon()
        {
            AddStep("create icon management dialog", createDialog);
            AddUntilStep("dialog loaded", () => dialog.IsLoaded);
            AddStep("open icon management", () => dialog.Show());
            AddUntilStep("dialog shown", () => dialog.State.Value, () => Is.EqualTo(Visibility.Visible));

            // the dialog scales up as it enters, which moves its rows, so they have to settle before the mouse
            // can be aimed at one of them.
            AddUntilStep("dialog finished entering", () => dialog.Scale.X, () => Is.EqualTo(1f).Within(0.001f));

            AddAssert("wiki icon starts out shown", () => !rowReportsHidden(ToolbarButtonLayout.CHAT));

            AddStep("click the wiki row", () => clickRowBody(ToolbarButtonLayout.CHAT));

            AddUntilStep("row presents the icon as hidden", () => rowReportsHidden(ToolbarButtonLayout.CHAT), () => Is.True);
            AddUntilStep("icon hidden on the toolbar", () => button(ToolbarButtonLayout.CHAT).Collapsed);
            AddAssert("hiding the icon was persisted", () =>
                ToolbarButtonLayout.Parse(config.Get<string>(OsuSetting.ToolbarButtons)).IsHidden(ToolbarButtonLayout.CHAT));

            // the row and its switch have to be interchangeable, so the switch is what undoes the click above.
            AddStep("click the wiki row's switch", () => clickRowSwitch(ToolbarButtonLayout.CHAT));

            AddUntilStep("row presents the icon as shown", () => rowReportsHidden(ToolbarButtonLayout.CHAT), () => Is.False);
            AddUntilStep("icon shown on the toolbar", () => !button(ToolbarButtonLayout.CHAT).Collapsed);
            AddAssert("showing the icon was persisted", () =>
                !ToolbarButtonLayout.Parse(config.Get<string>(OsuSetting.ToolbarButtons)).IsHidden(ToolbarButtonLayout.CHAT));
        }

        [Test]
        public void TestAutoHideWhenSpaceIsLimited()
        {
            AddStep("fix toolbar width", () =>
            {
                toolbar.RelativeSizeAxes = Axes.None;
                toolbar.Width = 4000;
            });

            AddUntilStep("toolbar laid out", () => toolbar.ChildrenOfType<ToolbarUserButton>().Single().DrawWidth > 0);

            // automatic hiding is opt-in, so a toolbar which cannot fit everything must not hide anything by default.
            AddStep("shrink toolbar", () => toolbar.Width = 300);
            AddUntilStep("nothing hidden while automatic hiding is off", () => !toolbar.HiddenButtonIds.Any());
            AddAssert("overflow button hidden", () => overflowButton().Collapsed);

            AddStep("widen toolbar", () => toolbar.Width = 4000);
            AddStep("enable automatic hiding", () => config.SetValue(OsuSetting.ToolbarAutoHideButtons, true));
            AddUntilStep("nothing hidden at a comfortable width", () => !toolbar.HiddenButtonIds.Any());

            // Banchosucks: only four optional icons are left (no news, changelog, wiki), and the overflow button reserves two
            // icon widths, so one missing pixel already hides three of them; two widths would hide all four.
            AddStep("narrow toolbar by one pixel", () => toolbar.Width = naturalWidth() - 1);
            AddUntilStep("icons hidden", () => toolbar.HiddenButtonIds.Any());
            AddAssert("not every icon hidden", () => toolbar.HiddenButtonIds.Count(), () => Is.LessThan(ToolbarButtonLayout.DEFAULT_ORDER.Length));
            AddAssert("overflow button shown", () => !overflowButton().Collapsed);

            // automatic hiding always takes icons away from the left, leaving the right-most ones visible.
            AddAssert("hidden icons are the left-most ones", () =>
            {
                List<string> hidden = toolbar.HiddenButtonIds.ToList();
                return hidden.SequenceEqual(ToolbarButtonLayout.DEFAULT_ORDER.Take(hidden.Count));
            });

            AddAssert("automatic hiding is not persisted", () =>
                ToolbarButtonLayout.Parse(config.Get<string>(OsuSetting.ToolbarButtons)).Hidden, () => Is.Empty);

            AddStep("give the toolbar almost no space", () => toolbar.Width = 300);
            AddUntilStep("every icon hidden", () => toolbar.HiddenButtonIds.Count() == ToolbarButtonLayout.DEFAULT_ORDER.Length);
            AddAssert("overflow button still shown", () => !overflowButton().Collapsed);

            AddStep("restore toolbar width", () => toolbar.Width = 4000);
            AddUntilStep("icons shown again", () => !toolbar.HiddenButtonIds.Any());
            AddAssert("overflow button hidden", () => overflowButton().Collapsed);
        }

        [Test]
        public void TestInvalidConfigurationFallsBackToDefaults()
        {
            AddAssert("empty configuration uses the default order", () =>
                ToolbarButtonLayout.Parse(string.Empty).Order, () => Is.EqualTo(ToolbarButtonLayout.DEFAULT_ORDER));

            AddAssert("malformed configuration uses the default order", () =>
                ToolbarButtonLayout.Parse(@"{ this is not json").Order, () => Is.EqualTo(ToolbarButtonLayout.DEFAULT_ORDER));

            AddAssert("unknown identifiers are dropped and missing ones appended", () =>
            {
                ToolbarButtonLayout layout = ToolbarButtonLayout.Parse(@"{""Order"":[""not_a_button"",""social""],""Hidden"":[""also_not_a_button""]}");

                return layout.Order.Count == ToolbarButtonLayout.DEFAULT_ORDER.Length
                       && layout.Order[0] == ToolbarButtonLayout.SOCIAL
                       && !layout.Order.Contains("not_a_button")
                       && layout.Hidden.Count == 0;
            });

            AddStep("write malformed configuration", () => config.SetValue(OsuSetting.ToolbarButtons, @"{{{"));
            // the lookup is read directly here rather than through the helper, as this is the one place where the
            // toolbar still being able to hand out its buttons is the thing being asserted, rather than assumed.
            AddUntilStep("toolbar still usable", () => toolbar.GetButton(ToolbarButtonLayout.RANKINGS) != null && !toolbar.HiddenButtonIds.Any());
        }

        /// <summary>
        /// The toolbar's button for the given identifier. Every identifier in the layout is backed by a button once
        /// the toolbar has loaded, so a missing one fails here - naming the identifier - instead of at whichever
        /// assertion happened to read the button first.
        /// </summary>
        private ToolbarButton button(string id)
            => toolbar.GetButton(id) ?? throw new AssertionException($@"The toolbar has no button with the identifier {id}.");

        private ToolbarOverlayToggleButton toggle(string id) => (ToolbarOverlayToggleButton)button(id);

        private void createDialog()
        {
            dialog = new ToolbarIconManagementDialog { Toolbar = toolbar };
            Add(dialog);
        }

        private ToolbarButtonOrderListItem orderRow(string id) => dialog.ChildrenOfType<ToolbarButtonOrderListItem>().Single(row => row.Model == id);

        /// <summary>
        /// The identifiers of the rows in the order they are displayed. Read from the rows' positions rather than
        /// from the list's own items, so that a list which was never rebuilt is not mistaken for one which was.
        /// </summary>
        private List<string> displayedOrder()
            => dialog.ChildrenOfType<ToolbarButtonOrderListItem>()
                     .OrderBy(row => row.Y)
                     .Select(row => row.Model)
                     .ToList();

        /// <summary>
        /// Whether the row for the given icon is currently presenting that icon as hidden. Read from the row's
        /// switch, which is the control saying so, rather than from the configuration the row was built from, so
        /// that a row which never picked a change up is not mistaken for one which did.
        /// </summary>
        private bool rowReportsHidden(string id) => !rowSwitch(id).Current.Value;

        private SwitchButton rowSwitch(string id) => orderRow(id).ChildrenOfType<SwitchButton>().Single();

        private SettingsButtonV2 restoreButton(LocalisableString text)
            => dialog.ChildrenOfType<SettingsButtonV2>().Single(button => button.Text.ToString() == text.ToString());

        private Drawable dragHandle(string id) => orderRow(id).ChildrenOfType<OsuRearrangeableListItem<string>.PlaylistItemHandle>().Single();

        /// <summary>
        /// Clicks the body of the row for the given icon, which is the part of it that is neither its drag handle
        /// nor its switch.
        /// </summary>
        private void clickRowBody(string id)
        {
            ToolbarButtonOrderListItem row = orderRow(id);

            // aimed a little way in from the row's left edge: far enough right to clear the drag handle, and far
            // enough left that the switch, which sits against the right edge, cannot be hit by mistake - a click
            // landing on the switch would toggle the very thing this is meant to test just as well.
            InputManager.MoveMouseTo(row, new Vector2(row_body_click_offset - row.DrawWidth / 2, 0));
            InputManager.Click(MouseButton.Left);
        }

        /// <summary>
        /// Clicks the switch of the row for the given icon.
        /// </summary>
        private void clickRowSwitch(string id)
        {
            InputManager.MoveMouseTo(rowSwitch(id));
            InputManager.Click(MouseButton.Left);
        }

        private ToolbarOverflowButton overflowButton() => toolbar.ChildrenOfType<ToolbarOverflowButton>().Single();

        private OsuContextMenu contextMenu() => this.ChildrenOfType<OsuContextMenu>().Single();

        private OsuMenuItem? findMenuItem(LocalisableString text)
            => contextMenu().Items.OfType<OsuMenuItem>().FirstOrDefault(item => item.Text.Value.ToString() == text.ToString());

        private float positionInToolbar(Drawable drawable) => drawable.ToSpaceOfOtherDrawable(Vector2.Zero, toolbar).X;

        /// <summary>
        /// The width the toolbar needs to show everything: the permanently visible controls at their natural
        /// sizes, plus every optional icon. Measured from the layout rather than hardcoded, so the test does not
        /// have to assume anything about the size of avatars, clocks or rulesets.
        /// </summary>
        private float naturalWidth()
            => toolbar.ChildrenOfType<ToolbarSettingsButton>().Single().DrawWidth
               + toolbar.ChildrenOfType<ToolbarHomeButton>().Single().DrawWidth
               + toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().DrawWidth
               + toolbar.ChildrenOfType<ToolbarMusicButton>().Single().DrawWidth
               + toolbar.ChildrenOfType<ToolbarUserButton>().Single().DrawWidth
               + toolbar.ChildrenOfType<ToolbarClock>().Single().DrawWidth
               + toolbar.ChildrenOfType<ToolbarNotificationButton>().Single().DrawWidth
               + ToolbarButtonLayout.DEFAULT_ORDER.Length * ToolbarButtonLayout.BUTTON_WIDTH;

        private void installStubOverlays()
        {
            foreach (string id in ToolbarButtonLayout.DEFAULT_ORDER)
            {
                if (stubOverlays.ContainsKey(id))
                    continue;

                var overlay = new TestOverlay
                {
                    Title = $@"test {id} title",
                    Description = $@"test {id} description",
                    Icon = FontAwesome.Solid.QuestionCircle,
                };

                stubOverlays[id] = overlay;
                Add(overlay);

                toggle(id).StateContainer = overlay;
            }
        }

        /// <summary>
        /// A minimal stand-in for one of the overlays a toolbar button toggles, exposing enough for the button
        /// to pick up a name, description and icon the same way it would from a real overlay.
        /// </summary>
        private partial class TestOverlay : OverlayContainer, INamedOverlayComponent
        {
            public IconUsage Icon { get; init; } = FontAwesome.Solid.QuestionCircle;

            public LocalisableString Title { get; init; }

            public LocalisableString Description { get; init; }

            public TestOverlay()
            {
                RelativeSizeAxes = Axes.Both;
                Size = new Vector2(0.25f);
            }

            protected override void PopIn() => this.FadeIn(50, Easing.OutQuint);

            protected override void PopOut() => this.FadeOut(50, Easing.OutQuint);
        }

        private class TestNotificationOverlay : INotificationOverlay
        {
            public void Post(Notification notification)
            {
            }

            public void Hide()
            {
            }

            public IBindable<int> UnreadCount { get; } = new Bindable<int>();

            public IEnumerable<Notification> AllNotifications => Enumerable.Empty<Notification>();
        }
    }
}

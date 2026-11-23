// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Toolbar;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// Covers the icon management dialog dimming the floating overlay column which holds the notifications.
    /// Everything outside the screen layer is normally left undimmed, so this only works because the dialog
    /// opts into it - see <see cref="ToolbarIconManagementDialog.DimFloatingOverlays"/>.
    /// </summary>
    [TestFixture]
    public partial class TestSceneToolbarIconManagementDimming : OsuGameTestScene
    {
        [Test]
        public void TestNotificationsAreDimmedWhileManagingIcons()
        {
            ConfirmAtMainMenu();

            // the game loads its components one after another, so the notification overlay only arrives some time
            // after the main menu it is displayed over.
            AddUntilStep("notifications loaded", () => notificationsOverlay() != null);

            AddUntilStep("notifications remain undimmed", floatingOverlaysColour, () => Is.EqualTo(Color4.White));

            AddStep("open icon management", () => Game.Toolbar.ShowIconManagement?.Invoke());
            AddUntilStep("dialog shown", () => managementDialog().State.Value, () => Is.EqualTo(Visibility.Visible));

            // a notification panel sitting at full brightness is exactly what the dialog is meant to put behind it.
            AddUntilStep("notifications dimmed", floatingOverlaysColour, () => Is.EqualTo(OsuColour.Gray(0.5f)));

            AddStep("close icon management", () => managementDialog().Hide());
            AddUntilStep("dialog hidden", () => managementDialog().State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddUntilStep("notifications undimmed again", floatingOverlaysColour, () => Is.EqualTo(Color4.White));
        }

        private NotificationOverlay? notificationsOverlay() => Game.ChildrenOfType<NotificationOverlay>().SingleOrDefault();

        private ToolbarIconManagementDialog managementDialog()
            => Game.ChildrenOfType<ToolbarIconManagementDialog>().Single();

        /// <summary>
        /// The colour of the floating overlay column which holds the notifications. It is faded by the game while
        /// a blocking overlay which opted into <see cref="OsuFocusedOverlayContainer.DimFloatingOverlays"/> is visible.
        /// </summary>
        private Color4 floatingOverlaysColour()
        {
            Container? floatingOverlays = notificationsOverlay()?.Parent as Container;

            // the column is not in the tree until the notification overlay itself has been loaded,
            // so it is reported as undimmed rather than failing the step that is waiting for it.
            return floatingOverlays?.Colour ?? Color4.White;
        }
    }
}

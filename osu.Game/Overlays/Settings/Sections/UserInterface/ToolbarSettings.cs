// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Overlays.Toolbar;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class ToolbarSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ToolbarStrings.ToolbarSettingsHeader;

        [Resolved(CanBeNull = true)]
        private ToolbarIconManagementDialog? dialog { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new SettingsButtonV2
            {
                Text = ToolbarStrings.ManageToolbarIcons,
                TooltipText = ToolbarStrings.ManageToolbarIconsDescription,
                Keywords = [@"toolbar", @"icons", @"hide", @"order", @"reorder"],
                Action = () => dialog?.Show(),
            });
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.General;
using osu.Game.Updater;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class GeneralSection : SettingsSection
    {
        public override LocalisableString Header => CommonStrings.General;

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Settings
        };

        [BackgroundDependencyLoader]
        private void load(UpdateManager? updateManager)
        {
            Add(new QuickActionSettings());
            Add(new BanchosucksPresetSettings());
            // Banchosucks: settings export / import with backup, next to the presets
            Add(new BanchosucksSettingsBackupSettings());
            // Banchosucks: take over settings from an osu!stable installation (desktop only, like the stable import)
            if (RuntimeInfo.IsDesktop)
                Add(new BanchosucksStableSettings());
            // Banchosucks: the player's own daily goal and the way into the personal statistics
            Add(new BanchosucksDailyGoalSettings());
            Add(new LanguageSettings());
            if (updateManager?.CanCheckForUpdate == true)
                Add(new UpdateSettings());
            if (RuntimeInfo.IsDesktop)
                Add(new InstallationSettings());
        }
    }
}

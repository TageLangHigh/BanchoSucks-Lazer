// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: switches for the comfort and streaming features.
    /// </summary>
    public partial class BanchosucksComfortSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Komfort und Streaming / Comfort and streaming";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Nicht stören während einer Map / Do not disturb during a map",
                    HintText = "Meldungen (Chat, Freunde, fertige Downloads) warten, bis die Map vorbei ist, und erscheinen dann als eine Zusammenfassung. Fehler und wichtige Meldungen kommen wie bisher. / Notifications (chat, friends, finished downloads) wait until the map is over and then show as one summary. Errors and critical notifications behave as before.",
                    Current = config.GetBindable<bool>(OsuSetting.BanchosucksDoNotDisturb),
                })
                {
                    Keywords = new[] { @"notifications", @"meldungen", @"benachrichtigungen", @"dnd", @"nicht stören", @"toast" },
                },
            });
        }
    }
}

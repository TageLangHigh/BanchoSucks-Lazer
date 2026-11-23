// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Banchosucks.Stats;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: the player's own daily goal (minutes played and / or maps passed per day, 0 = off). Progress and streak are shown on the main menu by <see cref="BanchosucksDailyGoalDisplay"/>.
    /// </summary>
    public partial class BanchosucksDailyGoalSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Tagesziel / Daily goal";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = "Minuten pro Tag / Minutes per day",
                    HintText = "0 = aus. Zählt aktives Spielen ohne Breaks und Pausen. Fortschritt und Serie stehen im Hauptmenü oben links. / 0 = off. Counts active play without breaks and pauses. Progress and streak are shown top left on the main menu.",
                    Current = config.GetBindable<int>(OsuSetting.BanchosucksDailyGoalMinutes),
                    KeyboardStep = 5,
                    LabelFormat = formatMinutes,
                })
                {
                    Keywords = new[] { @"goal", @"ziel", @"tagesziel", @"daily", @"streak", @"serie", @"minutes", @"minuten" },
                },
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = "Bestandene Maps pro Tag / Maps passed per day",
                    HintText = "0 = aus. Sind beide Werte gesetzt, müssen beide erreicht werden. Replays, Autoplay und das Testspiel im Editor zählen nicht. / 0 = off. With both values set, both have to be reached. Replays, autoplay and the editor's test play do not count.",
                    Current = config.GetBindable<int>(OsuSetting.BanchosucksDailyGoalMaps),
                    KeyboardStep = 1,
                    LabelFormat = formatMaps,
                })
                {
                    Keywords = new[] { @"goal", @"ziel", @"tagesziel", @"daily", @"streak", @"serie", @"maps" },
                },
            });
        }

        private static LocalisableString formatMinutes(int minutes) => minutes <= 0 ? "Aus / Off" : $"{minutes} min";

        private static LocalisableString formatMaps(int maps) => maps <= 0 ? "Aus / Off" : $"{maps}";
    }
}

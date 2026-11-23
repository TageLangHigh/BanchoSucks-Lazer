// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class MainMenuSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.MainMenuHeader;

        // the range is only constrained once the seasonal backgrounds are known; see updatePinnedIndexRange().
        private readonly BindableInt pinnedIndex = new BindableInt
        {
            MinValue = 0,
        };

        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds = null!;
        private Bindable<bool> pinnedSeasonal = null!;
        private Bindable<SeasonalBackgroundMode> seasonalMode = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, SessionStatics sessionStatics)
        {
            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);
            pinnedSeasonal = config.GetBindable<bool>(OsuSetting.PinnedSeasonalBackground);
            seasonalMode = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode);

            config.BindWith(OsuSetting.PinnedSeasonalBackgroundIndex, pinnedIndex);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ShowMenuTips,
                    Current = config.GetBindable<bool>(OsuSetting.MenuTips)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.InterfaceVoices,
                    Current = config.GetBindable<bool>(OsuSetting.MenuVoice)
                })
                {
                    Keywords = new[] { "intro", "welcome" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.OsuMusicTheme,
                    Current = config.GetBindable<bool>(OsuSetting.MenuMusic)
                })
                {
                    Keywords = new[] { "intro", "welcome" },
                },
                new SettingsItemV2(new FormEnumDropdown<BackgroundSource>
                {
                    Caption = UserInterfaceStrings.BackgroundSource,
                    Current = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource),
                }),
                new SettingsItemV2(new FormEnumDropdown<SeasonalBackgroundMode>
                {
                    Caption = UserInterfaceStrings.SeasonalBackgrounds,
                    Current = seasonalMode,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.PinnedSeasonalBackground,
                    Current = pinnedSeasonal,
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = UserInterfaceStrings.PinnedSeasonalBackgroundIndex,
                    Current = pinnedIndex,
                    KeyboardStep = 1,
                    LabelFormat = i => $"#{i + 1}",
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            seasonalBackgrounds.BindValueChanged(_ => updatePinnedIndexRange(), true);

            pinnedSeasonal.BindValueChanged(pinned =>
            {
                // a pin only replaces the rotation, so it requires seasonal backgrounds to be enabled at all.
                // this matches what selecting a pinned background in the background editor does, so that both
                // entry points leave the configuration in the same state after a single action.
                if (pinned.NewValue)
                    seasonalMode.Value = SeasonalBackgroundMode.Always;
            });
        }

        private void updatePinnedIndexRange()
        {
            // the list is fetched asynchronously, so no range can be applied until it arrives. narrowing the
            // range to zero here would clamp (and persist) a previously saved index before the fetch completes.
            if (seasonalBackgrounds.Value?.Backgrounds is not { Count: > 0 } backgrounds)
                return;

            // note that this also clamps the current value to the new maximum; the index is kept as-is when no
            // seasonal backgrounds are available at all, so that an out-of-season list does not discard it.
            pinnedIndex.MaxValue = backgrounds.Count - 1;
        }
    }
}

// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class ResultsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.ResultsHeader;

        private Bindable<bool> legacyResults = null!;
        private readonly Bindable<ResultsScreenStyle> style = new Bindable<ResultsScreenStyle>();
        private bool syncingStyle;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            // The original Torii configuration key is kept, so the setting stays compatible with g0v0.
            legacyResults = config.GetBindable<bool>(OsuSetting.ToriiStableResults);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<ResultsScreenStyle>
                {
                    Caption = UserInterfaceStrings.ResultsScreenStyle,
                    HintText = UserInterfaceStrings.ResultsScreenStyleDescription,
                    Current = style,
                })
                {
                    Keywords = new[] { @"results", @"ranking", @"score", @"legacy", @"stable", @"layout", @"skin" },
                },
            };

            legacyResults.BindValueChanged(_ => syncStyle(), true);
            style.BindValueChanged(e =>
            {
                if (syncingStyle)
                    return;

                syncingStyle = true;
                legacyResults.Value = e.NewValue == ResultsScreenStyle.Legacy;
                syncingStyle = false;
            });
        }

        private void syncStyle()
        {
            if (syncingStyle)
                return;

            syncingStyle = true;
            style.Value = legacyResults.Value ? ResultsScreenStyle.Legacy : ResultsScreenStyle.Default;
            syncingStyle = false;
        }
    }
}

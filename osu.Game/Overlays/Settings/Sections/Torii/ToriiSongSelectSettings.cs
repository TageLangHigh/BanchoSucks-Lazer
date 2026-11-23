// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Torii
{
    /// <summary>
    /// Appearance options for the song select interface.
    /// </summary>
    public partial class ToriiSongSelectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.SongSelectAppearanceHeader;

        // el toggle stable (que ademas trae el footer legacy bundleado). cuando esta prendido, el footer
        // legacy va si o si, asi que el toggle de abajo queda forzado prendido y deshabilitado.
        // se asignan en load() con el bindable del config DIRECTO (no new + BindTo a un temporal): las
        // bindings de osu!framework son weak-ref y un bound-copy temporal sin guardar se lo come el GC,
        // y ahi el toggle dejaba de reaccionar (quedaba grisado aunque apagaras el stable).
        private Bindable<bool> stableSongSelect = null!;

        // la preferencia REAL del usuario para el footer legacy standalone (lo que se guarda en config).
        private Bindable<bool> footerPref = null!;

        // lo que ve el checkbox: refleja la pref cuando stable esta apagado, o queda checked+disabled
        // cuando stable esta prendido. el writeback a footerPref solo persiste clicks REALES del usuario.
        private readonly BindableBool footerDisplay = new BindableBool();

        // suppress flag (mismo patron que PotatoModeToggleAndRestart / ToriiAuraSettings): cuando
        // syncDisplay() escribe footerDisplay.Value programaticamente, el evento de cambio se dispara
        // SINCRONO. sin esta bandera, el writeback de abajo correria con Disabled todavia en false
        // (lo seteamos despues) y pisaria footerPref. con la bandera solo guardamos clicks del usuario.
        private bool syncingDisplay;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            stableSongSelect = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin);
            footerPref = config.GetBindable<bool>(OsuSetting.ToriiLegacySongSelectFooter);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.LegacySongSelect,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin),
                    HintText = UserInterfaceStrings.LegacySongSelectDescription,
                })
                {
                    Keywords = new[] { @"footer", @"skin", @"song", @"select", @"legacy", @"bottom", @"buttons", @"torii", @"stable" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.UseInterFont,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFont),
                    HintText = UserInterfaceStrings.UseInterFontDescription,
                })
                {
                    Keywords = new[] { @"font", @"inter", @"typeface", @"stable", @"legacy", @"song", @"select", @"torii" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.LegacySongSelectFooter,
                    Current = footerDisplay,
                    HintText = UserInterfaceStrings.LegacySongSelectFooterDescription,
                })
                {
                    Keywords = new[] { @"footer", @"legacy", @"stable", @"bottom", @"buttons", @"song", @"select", @"torii" },
                },
            };

            stableSongSelect.BindValueChanged(_ => syncDisplay(), true);
            footerPref.BindValueChanged(_ => syncDisplay());

            // writeback: solo persistimos cuando el cambio vino de un click real del usuario
            // (no durante syncDisplay, y solo posible cuando el checkbox no esta disabled).
            footerDisplay.BindValueChanged(e =>
            {
                if (!syncingDisplay)
                    footerPref.Value = e.NewValue;
            });
        }

        private void syncDisplay()
        {
            syncingDisplay = true;

            bool stableOn = stableSongSelect.Value;

            // limpiamos Disabled antes de tocar Value asi nunca escribimos sobre un bindable bloqueado.
            footerDisplay.Disabled = false;
            footerDisplay.Value = stableOn || footerPref.Value;
            // con stable prendido el footer va si o si: checked y bloqueado.
            footerDisplay.Disabled = stableOn;

            syncingDisplay = false;
        }
    }
}

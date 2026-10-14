// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: one-click presets. Each button asks once, applies <see cref="BanchosucksPresets"/> and reports back.
    /// </summary>
    public partial class BanchosucksPresetSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Voreinstellungen / Presets";

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig, OsuConfigManager config, GameHost host, AudioManager audio, SkinManager skins,
                          IDialogOverlay? dialogOverlay, INotificationOverlay? notifications)
        {
            AddRange(new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = "Flüssig / Smooth",
                    TooltipText = "Frame-Limiter 2x, Multithreading, WASAPI, Raw Input; Storyboard, Video, Hit-Lighting und Blur aus. / Frame limiter 2x, multithreading, WASAPI, raw input; storyboard, video, hit lighting and blur off.",
                    Keywords = new[] { @"performance", @"stutter", @"ruckler", @"fps", @"latency", @"preset", @"voreinstellung" },
                    Action = () => confirm(
                        "Voreinstellung „Flüssig“ übernehmen? Grafik- und Eingabe-Einstellungen werden überschrieben. / Apply the \"Smooth\" preset? Graphics and input settings will be overwritten.",
                        () =>
                        {
                            BanchosucksPresets.ApplySmooth(frameworkConfig, config, audio, host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault());
                            done("Voreinstellung „Flüssig“ übernommen. / \"Smooth\" preset applied.");
                        }),
                },
                new SettingsButtonV2
                {
                    Text = "Stable-Gefühl / Stable feel",
                    TooltipText = "Classic-Skin, Classic-Mod (bei uns mit pp), Punkte wie in Stable, Beatmap-Skins und -Hitsounds, Key-Overlay. / Classic skin, Classic mod (ranked here), stable-style score, beatmap skins and hitsounds, key overlay.",
                    Keywords = new[] { @"classic", @"stable", @"legacy", @"preset", @"voreinstellung" },
                    Action = () => confirm(
                        "Voreinstellung „Stable-Gefühl“ übernehmen? Skin, Punkteanzeige und Mods werden geändert. / Apply the \"Stable feel\" preset? Skin, score display and mods will change.",
                        () =>
                        {
                            BanchosucksPresets.ApplyStableFeel(config);
                            // the config only seeds the skin at startup, the running game switches through the manager
                            skins.SetSkinFromConfiguration(config.Get<string>(OsuSetting.Skin));
                            game?.BanchosucksSelectClassicMod();
                            done("Voreinstellung „Stable-Gefühl“ übernommen, Classic-Mod ist ausgewählt. / \"Stable feel\" preset applied, the Classic mod is selected.");
                        }),
                },
            });

            void confirm(LocalisableString message, Action apply)
            {
                if (dialogOverlay != null)
                    dialogOverlay.Push(new ConfirmDialog(message, apply));
                else
                    apply();
            }

            void done(LocalisableString text) => notifications?.Post(new SimpleNotification { Text = text, Icon = FontAwesome.Solid.Check });
        }
    }
}

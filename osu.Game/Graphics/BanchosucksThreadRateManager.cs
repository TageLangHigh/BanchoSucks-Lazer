// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Graphics
{
    /// <summary>
    /// Banchosucks: applies <see cref="OsuSetting.BanchosucksInputRate"/> and <see cref="OsuSetting.BanchosucksAudioRate"/>.
    /// </summary>
    /// <remarks>
    /// osu!framework reads input on the input (main) thread at a fixed 1000 Hz and caps the
    /// update thread, which processes that input, at 1000 Hz as well; the audio thread also
    /// runs at 1000 Hz. The input and update rates are reset by the framework whenever the
    /// frame limiter, the display mode or the threading mode changes, so both settings are
    /// re-applied once a second. For input, the update thread is kept at least as fast as
    /// the input thread. The draw rate (frame limiter) and the audio sample rate are never
    /// touched. Going back to the default hands control back to the framework.
    /// </remarks>
    public partial class BanchosucksThreadRateManager : Component
    {
        private const double default_hz = 1000;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly Bindable<BanchosucksThreadRate> inputRate = new Bindable<BanchosucksThreadRate>();
        private readonly Bindable<BanchosucksThreadRate> audioRate = new Bindable<BanchosucksThreadRate>();

        private bool overridingInput;
        private bool overridingAudio;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.BanchosucksInputRate, inputRate);
            config.BindWith(OsuSetting.BanchosucksAudioRate, audioRate);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputRate.BindValueChanged(rate =>
            {
                Logger.Log($"Banchosucks input rate: {(int)rate.NewValue} Hz");
                apply();
            }, true);
            audioRate.BindValueChanged(rate =>
            {
                Logger.Log($"Banchosucks audio thread rate: {(int)rate.NewValue} Hz");
                apply();
            }, true);

            // the framework resets the thread rates on frame limiter / display / threading changes
            Scheduler.AddDelayed(apply, 1000, true);
        }

        private void apply()
        {
            applyInput();
            applyAudio();
        }

        private void applyInput()
        {
            double target = (int)inputRate.Value;

            if (target <= default_hz)
            {
                if (!overridingInput)
                    return;

                overridingInput = false;
                host.InputThread.ActiveHz = default_hz;
                // re-setting the framework's own update limit restores its computed rates
                host.MaximumUpdateHz = host.MaximumUpdateHz;
                return;
            }

            overridingInput = true;

            if (host.InputThread.ActiveHz != target)
                host.InputThread.ActiveHz = target;

            if (host.UpdateThread.ActiveHz < target)
                host.UpdateThread.ActiveHz = target;
        }

        private void applyAudio()
        {
            double target = (int)audioRate.Value;

            if (target <= default_hz)
            {
                if (!overridingAudio)
                    return;

                overridingAudio = false;
                host.AudioThread.ActiveHz = default_hz;
                return;
            }

            overridingAudio = true;

            if (host.AudioThread.ActiveHz != target)
                host.AudioThread.ActiveHz = target;
        }
    }
}

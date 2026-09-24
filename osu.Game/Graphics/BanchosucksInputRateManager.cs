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
    /// Banchosucks: applies <see cref="OsuSetting.BanchosucksInputRate"/>.
    /// </summary>
    /// <remarks>
    /// osu!framework reads input on the input (main) thread at a fixed 1000 Hz and caps the
    /// update thread, which processes that input, at 1000 Hz as well. Both rates are public
    /// but the framework resets them whenever the frame limiter, the display mode or the
    /// threading mode changes. This component raises the input thread to the chosen rate
    /// and makes sure the update thread runs at least that fast, and re-applies both once
    /// a second so a reset by the framework never sticks. The draw rate (frame limiter)
    /// is left alone. Going back to the default hands control back to the framework.
    /// </remarks>
    public partial class BanchosucksInputRateManager : Component
    {
        private const double default_hz = 1000;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly Bindable<BanchosucksInputRate> inputRate = new Bindable<BanchosucksInputRate>();

        private bool overriding;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.BanchosucksInputRate, inputRate);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputRate.BindValueChanged(rate =>
            {
                Logger.Log($"Banchosucks input rate: {(int)rate.NewValue} Hz");
                apply();
            }, true);

            // the framework resets the thread rates on frame limiter / display / threading changes
            Scheduler.AddDelayed(apply, 1000, true);
        }

        private void apply()
        {
            double target = (int)inputRate.Value;

            if (target <= default_hz)
            {
                if (!overriding)
                    return;

                overriding = false;
                host.InputThread.ActiveHz = default_hz;
                // re-setting the framework's own update limit restores its computed rates
                host.MaximumUpdateHz = host.MaximumUpdateHz;
                return;
            }

            overriding = true;

            if (host.InputThread.ActiveHz != target)
                host.InputThread.ActiveHz = target;

            if (host.UpdateThread.ActiveHz < target)
                host.UpdateThread.ActiveHz = target;
        }
    }
}

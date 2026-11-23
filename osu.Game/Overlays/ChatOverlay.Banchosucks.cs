// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;

namespace osu.Game.Overlays
{
    /// <summary>
    /// Banchosucks: streamer mode keeps the chat overlay closed, whoever asks for it (key, toolbar, link, notification).
    /// </summary>
    public partial class ChatOverlay
    {
        /// <summary>
        /// Raised when streamer mode refused to open the chat.
        /// </summary>
        public event Action? BanchosucksBlockedByStreamerMode;

        protected override void UpdateState(ValueChangedEvent<Visibility> state)
        {
            if (state.NewValue == Visibility.Visible && config.Get<bool>(OsuSetting.BanchosucksStreamerMode))
            {
                State.Value = Visibility.Hidden;
                BanchosucksBlockedByStreamerMode?.Invoke();
                return;
            }

            base.UpdateState(state);
        }
    }
}

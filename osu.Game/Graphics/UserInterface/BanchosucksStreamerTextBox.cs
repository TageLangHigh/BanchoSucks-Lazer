// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// Banchosucks: a text box that shows dots instead of its text while streamer mode is on
    /// (the account name or e-mail address in the login form). Typing works as usual.
    /// </summary>
    public partial class BanchosucksStreamerTextBox : OsuTextBox
    {
        /// <summary>
        /// Whether the text is currently shown as dots.
        /// </summary>
        public bool Masked { get; private set; }

        private Bindable<bool>? streamerMode;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager? config)
        {
            streamerMode = config?.GetBindable<bool>(OsuSetting.BanchosucksStreamerMode);
            Masked = streamerMode?.Value == true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // text set before the box was loaded may already have been drawn in clear
            if (Masked)
                redrawCharacters();

            streamerMode?.BindValueChanged(mode =>
            {
                Masked = mode.NewValue;
                redrawCharacters();
            });
        }

        protected override Drawable GetDrawableCharacter(char c) => Masked
            ? new FallingDownContainer
            {
                AutoSizeAxes = Axes.Both,
                Child = new OsuPasswordTextBox.PasswordMaskChar(FontSize),
            }
            : base.GetDrawableCharacter(c);

        protected override bool AllowUniqueCharacterSamples => !Masked;

        private void redrawCharacters()
        {
            string text = Text;

            if (string.IsNullOrEmpty(text))
                return;

            // the characters are only drawn when they are inserted
            Text = string.Empty;
            Text = text;
        }
    }
}

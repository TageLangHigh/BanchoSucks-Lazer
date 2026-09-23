// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class FooterPopoverButton : OsuButton
    {
        public IconUsage Icon { get; init; }
        public Color4? TextColour { get; init; }

        public FooterPopoverButton()
        {
            Size = new Vector2(265, 50);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            SpriteText.Colour = TextColour ?? Color4.White;
            Content.CornerRadius = 10;

            Add(new SpriteIcon
            {
                Anchor = Framework.Graphics.Anchor.CentreLeft,
                Origin = Framework.Graphics.Anchor.CentreLeft,
                Size = new Vector2(17),
                X = 15,
                Icon = Icon,
                Colour = TextColour ?? Color4.White,
            });
        }

        protected override SpriteText CreateText() => new OsuSpriteText
        {
            Depth = -1,
            Origin = Framework.Graphics.Anchor.CentreLeft,
            Anchor = Framework.Graphics.Anchor.CentreLeft,
            X = 40
        };
    }
}

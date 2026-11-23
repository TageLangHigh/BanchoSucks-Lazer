// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Share;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Banchosucks: makes a picture of the shown profile (<see cref="BanchosucksProfileCard"/>), saved to
    /// <c>screenshots/cards</c> and copied to the clipboard by <see cref="BanchosucksCardExporter"/>.
    /// Uses only what the overlay has loaded: the user with the statistics of the shown ruleset and the first
    /// entry of the "best performance" list (<see cref="BanchosucksProfileBestScores"/>).
    /// </summary>
    public partial class BanchosucksProfileCardButton : ProfileHeaderButton
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        public override LocalisableString TooltipText => ClickerStrings.Text("save profile card (image)", "Profil-Karte speichern (Bild)");

        [Resolved]
        private BanchosucksCardExporter? exporter { get; set; }

        public BanchosucksProfileCardButton()
        {
            Content.Alpha = 0;

            Child = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Icon = FontAwesome.Solid.IdCard,
                FillMode = FillMode.Fit,
                Size = new Vector2(50, 14)
            };

            Action = create;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            User.BindValueChanged(_ => updateState(), true);
        }

        /// <summary>
        /// Whether the button is offered for the shown profile (for tests).
        /// </summary>
        public bool Available { get; private set; }

        private void updateState()
        {
            Available = exporter != null && User.Value != null;
            Content.Alpha = Available ? 1 : 0;
        }

        /// <summary>
        /// What the card for the shown profile says, or null when no profile is shown.
        /// </summary>
        public BanchosucksProfileCardData? CreateData()
        {
            var profile = User.Value;

            if (profile == null)
                return null;

            return BanchosucksProfileCardData.From(
                profile.User,
                profile.Ruleset,
                BanchosucksProfileBestScores.Get(profile.User.Id, profile.Ruleset.ShortName),
                ClickerStrings.IsGerman);
        }

        private void create()
        {
            if (!Available || exporter == null || exporter.Busy)
                return;

            var data = CreateData();

            if (data == null)
                return;

            exporter.Export(new BanchosucksProfileCard(data), data.FileName, ("Profile card", "Profil-Karte"));
        }
    }
}

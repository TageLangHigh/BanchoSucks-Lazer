// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Key profiles in the key binding panel (F47): the button above the bindings of a ruleset variant.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBanchosucksKeyProfiles : OsuTestScene
    {
        private const string ruleset_name = "mania";
        private const int variant = 4;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Cached(typeof(IDialogOverlay))]
        private readonly DialogOverlay dialogOverlay = new DialogOverlay();

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private readonly PopoverContainer content;

        private VariantBindingsSubsection subsection = null!;
        private BanchosucksKeyProfileButton button = null!;

        public TestSceneBanchosucksKeyProfiles()
        {
            Children = new Drawable[]
            {
                content = new PopoverContainer { RelativeSizeAxes = Axes.Both },
                dialogOverlay,
            };
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("default keys, no profiles", () =>
            {
                setKeys(InputKey.D, InputKey.F, InputKey.J, InputKey.K);
                storage.GetStorageForDirectory(BanchosucksKeyProfiles.DIRECTORY).Delete(BanchosucksKeyProfiles.FILENAME);
            });

            AddStep("create subsection", () =>
            {
                content.Child = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 500,
                    Child = subsection = new VariantBindingsSubsection(rulesets.GetRuleset(ruleset_name)!, variant),
                };
            });

            AddUntilStep("wait for load", () => subsection.IsLoaded);
            AddStep("find button", () => button = subsection.ChildrenOfType<BanchosucksKeyProfileButton>().Single());
            AddAssert("button is above the rows", () => button.ScreenSpaceDrawQuad.TopLeft.Y < subsection.ChildrenOfType<KeyBindingRow>().First().ScreenSpaceDrawQuad.TopLeft.Y);
        }

        [Test]
        public void TestSaveSwitchDelete()
        {
            AddStep("save as Mitte", () => button.SaveCurrent("Mitte"));
            AddAssert("profile saved", () => button.Profiles.Select(p => p.Name), () => Is.EqualTo(new[] { "Mitte" }));

            AddStep("rebind in the database", () => setKeys(InputKey.A, InputKey.S, InputKey.K, InputKey.L));
            AddStep("save as Außen", () => button.SaveCurrent("Außen"));
            AddAssert("two profiles", () => button.Profiles.Select(p => p.Name), () => Is.EqualTo(new[] { "Außen", "Mitte" }));

            AddStep("switch to Mitte", () => button.RequestLoad("Mitte"));
            AddUntilStep("asks first", () => dialogOverlay.CurrentDialog != null);
            AddAssert("nothing switched yet", () => keys(), () => Is.EqualTo(new[] { InputKey.A, InputKey.S, InputKey.K, InputKey.L }));

            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("database switched", () => keys().SequenceEqual(new[] { InputKey.D, InputKey.F, InputKey.J, InputKey.K }));
            AddAssert("rows show the profile", () => rowKeys(), () => Is.EqualTo(new[] { InputKey.D, InputKey.F, InputKey.J, InputKey.K }));
            AddAssert("backup profile exists", () => button.Profiles.Last().Name, () => Is.EqualTo(BanchosucksKeyProfiles.BACKUP_NAME));

            AddUntilStep("dialog gone", () => dialogOverlay.CurrentDialog == null);
            AddStep("switch to the backup", () => button.RequestLoad(BanchosucksKeyProfiles.BACKUP_NAME));
            AddUntilStep("asks first", () => dialogOverlay.CurrentDialog != null);
            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("old keys are back", () => keys().SequenceEqual(new[] { InputKey.A, InputKey.S, InputKey.K, InputKey.L }));

            AddUntilStep("dialog gone", () => dialogOverlay.CurrentDialog == null);
            AddStep("delete Mitte", () => button.RequestDelete("Mitte"));
            AddUntilStep("asks first", () => dialogOverlay.CurrentDialog != null);
            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("profile gone", () => button.Profiles.All(p => p.Name != "Mitte"));
            AddAssert("keys untouched", () => keys(), () => Is.EqualTo(new[] { InputKey.A, InputKey.S, InputKey.K, InputKey.L }));
        }

        [Test]
        public void TestOverwriteAsksAndActiveProfileDoesNot()
        {
            AddStep("save as Eins", () => button.SaveCurrent("Eins"));
            AddStep("switch to Eins", () => button.RequestLoad("Eins"));
            AddAssert("already active, no question", () => dialogOverlay.CurrentDialog == null);

            AddStep("rebind in the database", () => setKeys(InputKey.Q, InputKey.W, InputKey.O, InputKey.P));
            AddStep("save as Eins again", () => button.SaveCurrent("eins"));
            AddUntilStep("asks before replacing", () => dialogOverlay.CurrentDialog != null);
            AddAssert("old profile still stored", () => button.Profiles.Single().Bindings.First().Keys.First(), () => Is.EqualTo("D"));

            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("profile replaced", () => button.Profiles.Single().Bindings.First().Keys.First() == "Q");

            AddAssert("empty name refused", () => !button.SaveCurrent("   "));
            AddAssert("reserved name refused", () => !button.SaveCurrent(BanchosucksKeyProfiles.BACKUP_NAME));
        }

        [Test]
        public void TestPopoverListsProfiles()
        {
            AddStep("save two profiles", () =>
            {
                button.SaveCurrent("Mitte");
                button.SaveCurrent("Ein sehr langer Profilname der abgeschnitten wird");
            });

            AddStep("open the list", () => button.TriggerClick());
            AddUntilStep("popover shown", () => this.ChildrenOfType<OsuPopover>().Any(p => p.IsLoaded));
            AddUntilStep("both profiles listed", () =>
            {
                string[] texts = this.ChildrenOfType<OsuPopover>().Single().ChildrenOfType<TruncatingSpriteText>().Select(t => t.Text.ToString()).ToArray();
                return texts.Contains("Mitte") && texts.Contains(BanchosucksKeyProfiles.CleanName("Ein sehr langer Profilname der abgeschnitten wird"));
            });
            AddAssert("load and delete per profile", () => this.ChildrenOfType<OsuPopover>().Single().ChildrenOfType<RoundedButton>().Count() == 2 * 2 + 1);
        }

        private List<RealmKeyBinding> rows(Realms.Realm r)
            => r.All<RealmKeyBinding>().AsEnumerable().Where(b => b.RulesetName == ruleset_name && b.Variant == variant)
                // the first slot of every key (osu!mania has a second, empty one)
                .GroupBy(b => b.ActionInt).OrderBy(g => g.Key).Select(g => g.First()).ToList();

        private InputKey[] keys() => realm.Run(r => rows(r).Select(b => b.KeyCombination.Keys.Single()).ToArray());

        private InputKey[] rowKeys() => subsection.ChildrenOfType<KeyBindingRow>()
                                                  .OrderBy(row => (int)row.Action)
                                                  .Select(row => row.KeyBindings.First().KeyCombination.Keys.Single()).ToArray();

        private void setKeys(params InputKey[] newKeys) => realm.Write(r =>
        {
            var bindings = rows(r);

            for (int i = 0; i < newKeys.Length; i++)
                bindings[i].KeyCombination = new KeyCombination(newKeys[i]);
        });
    }
}

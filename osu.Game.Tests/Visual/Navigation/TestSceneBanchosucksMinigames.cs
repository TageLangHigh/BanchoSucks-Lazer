// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Banchosucks;
using osu.Game.Screens.Menu;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneBanchosucksMinigames : OsuGameTestScene
    {
        private Storage clickerStorage => Game.Dependencies.Get<Storage>().GetStorageForDirectory("banchosucks");

        [Test]
        public void TestClickerFromMainMenu()
        {
            AddStep("delete old save", () => clickerStorage.Delete("osu-clicker.json"));

            AddStep("open play menu", () =>
            {
                InputManager.Key(Key.P);
                InputManager.Key(Key.P);
            });
            AddStep("press G", () => InputManager.Key(Key.G));
            AddUntilStep("minigames open", () => Game.ScreenStack.CurrentScreen is MinigamesScreen screen && screen.IsLoaded && screen.Alpha == 1);
            AddUntilStep("main menu logo gone", () => Game.ChildrenOfType<OsuLogo>().All(l => l.Alpha == 0));

            AddStep("click clicker tile", () =>
            {
                InputManager.MoveMouseTo(((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuClickableContainer>().First());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("clicker open", () => Game.ScreenStack.CurrentScreen is OsuClickerScreen screen && screen.IsLoaded);

            AddRepeatStep("press Z", () => InputManager.Key(Key.Z), 20);
            AddStep("click circle", () =>
            {
                InputManager.MoveMouseTo(clicker.ChildrenOfType<Drawable>().First(d => d.GetType().Name == "ClickerCircle"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("21 PP", () => hasText("21 PP"));
            AddAssert("default osu! keys shown", () => hasTextContaining("Tasten Z / X"));
            AddAssert("tapping speed measured", () => hasTextContaining(" BPM · Tasten"));

            AddStep("buy cursor trail", () =>
            {
                InputManager.MoveMouseTo(clicker.ChildrenOfType<RoundedButton>().First(b => b.Enabled.Value));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("owns one cursor trail", () => hasTextStarting("1× · "));

            AddStep("exit clicker", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back on minigames", () => Game.ScreenStack.CurrentScreen is MinigamesScreen);
            AddAssert("save written", () => clickerStorage.Exists("osu-clicker.json"));

            AddStep("open clicker again", () => ((MinigamesScreen)Game.ScreenStack.CurrentScreen).Push(new OsuClickerScreen()));
            AddUntilStep("clicker open", () => Game.ScreenStack.CurrentScreen is OsuClickerScreen screen && screen.IsLoaded);
            AddAssert("cursor trail restored", () => hasTextStarting("1× · "));
        }

        [Test]
        public void TestCustomTapKey()
        {
            AddStep("delete old save", () => clickerStorage.Delete("osu-clicker.json"));
            AddStep("bind osu! left button to A", () => setOsuLeftButton(InputKey.A));

            AddStep("open clicker", () => Game.ScreenStack.Push(new OsuClickerScreen()));
            AddUntilStep("clicker open", () => Game.ScreenStack.CurrentScreen is OsuClickerScreen screen && screen.IsLoaded);
            AddUntilStep("key A shown", () => hasTextContaining("Tasten A / X") || hasTextContaining("Tasten X / A"));

            AddRepeatStep("press A", () => InputManager.Key(Key.A), 12);
            AddUntilStep("12 PP", () => hasText("12 PP"));

            AddStep("press Z", () => InputManager.Key(Key.Z));
            AddAssert("Z is not bound any more", () => hasText("12 PP"));

            AddStep("bind osu! left button to Z again", () => setOsuLeftButton(InputKey.Z));
            AddUntilStep("key Z shown", () => hasTextContaining("Tasten Z / X") || hasTextContaining("Tasten X / Z"));
            AddStep("press Z", () => InputManager.Key(Key.Z));
            AddUntilStep("13 PP", () => hasText("13 PP"));
        }

        private void setOsuLeftButton(InputKey key) => Game.Dependencies.Get<RealmAccess>().Write(r =>
        {
            var binding = r.All<RealmKeyBinding>().First(b => b.RulesetName == "osu" && b.Variant == 0 && b.ActionInt == 0);
            binding.KeyCombination = new KeyCombination(key);
        });

        [Test]
        public void TestMenuOpacity()
        {
            AddStep("set menu opacity 50%", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksMenuOpacity, 0.5f));
            AddAssert("menu is half transparent", () => ((Drawable)Game.ScreenStack.CurrentScreen).Colour.TopLeft.Linear.A, () => Is.EqualTo(0.5f).Within(0.01f));
            AddStep("reset menu opacity", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksMenuOpacity, 1f));
            AddAssert("menu is opaque", () => ((Drawable)Game.ScreenStack.CurrentScreen).Colour.TopLeft.Linear.A, () => Is.EqualTo(1f).Within(0.01f));
        }

        private OsuClickerScreen clicker => (OsuClickerScreen)Game.ScreenStack.CurrentScreen;

        private bool hasText(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == text);

        private bool hasTextContaining(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains(text, System.StringComparison.Ordinal));

        private bool hasTextStarting(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().StartsWith(text, System.StringComparison.Ordinal));
    }
}

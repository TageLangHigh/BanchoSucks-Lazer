// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Banchosucks;
using osu.Game.Screens.Banchosucks.Clicker;
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
            AddStep("reset clicker", resetClicker);

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
            AddUntilStep("20 PP", () => hasText("20 PP"));

            // input lock: while a keyboard stream is going, mouse clicks are ignored
            AddStep("press Z and click the circle at once", () =>
            {
                InputManager.Key(Key.Z);
                InputManager.MoveMouseTo(clicker.ChildrenOfType<Drawable>().First(d => d.GetType().Name == "ClickerCircle"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("21 PP", () => hasText("21 PP"));
            AddAssert("mouse click ignored during the keyboard stream", () => !hasText("22 PP"));

            // the hidden stats tab also holds a "0 BPM" record text, so only the visible counter counts
            AddUntilStep("stream paused", () => hasVisibleText("0 BPM"));
            AddStep("click circle", () =>
            {
                InputManager.MoveMouseTo(clicker.ChildrenOfType<Drawable>().First(d => d.GetType().Name == "ClickerCircle"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("22 PP", () => hasText("22 PP"));
            AddAssert("default osu! keys shown", () => hasTextContaining("keys Z / X"));
            AddAssert("tapping speed measured", () => hasTextContaining(" BPM · keys"));

            AddStep("buy cursor trail", () =>
            {
                InputManager.MoveMouseTo(clicker.ChildrenOfType<RoundedButton>().First(b => b.Enabled.Value));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("owns one cursor trail", () => hasTextStarting("1× · "));

            AddStep("exit clicker", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("back on minigames", () => Game.ScreenStack.CurrentScreen is MinigamesScreen);
            AddAssert("save written", () => clickerStorage.Exists(ClickerEngine.SAVE_FILE));

            AddStep("open clicker again", () => ((MinigamesScreen)Game.ScreenStack.CurrentScreen).Push(new OsuClickerScreen()));
            AddUntilStep("clicker open", () => Game.ScreenStack.CurrentScreen is OsuClickerScreen screen && screen.IsLoaded);
            AddAssert("cursor trail restored", () => hasTextStarting("1× · "));
        }

        [Test]
        public void TestCustomTapKey()
        {
            AddStep("reset clicker", resetClicker);
            AddStep("bind osu! left button to A", () => setOsuLeftButton(InputKey.A));

            openClicker();
            AddUntilStep("key A shown", () => hasTextContaining("keys A / X") || hasTextContaining("keys X / A"));

            AddRepeatStep("press A", () => InputManager.Key(Key.A), 12);
            AddUntilStep("12 PP", () => hasText("12 PP"));

            AddStep("press Z", () => InputManager.Key(Key.Z));
            AddAssert("Z is not bound any more", () => hasText("12 PP"));

            AddStep("bind osu! left button to Z again", () => setOsuLeftButton(InputKey.Z));
            AddUntilStep("key Z shown", () => hasTextContaining("keys Z / X") || hasTextContaining("keys X / Z"));
            AddStep("press Z", () => InputManager.Key(Key.Z));
            AddUntilStep("13 PP", () => hasText("13 PP"));
        }

        private void setOsuLeftButton(InputKey key) => Game.Dependencies.Get<RealmAccess>().Write(r =>
        {
            var binding = r.All<RealmKeyBinding>().First(b => b.RulesetName == "osu" && b.Variant == 0 && b.ActionInt == 0);
            binding.KeyCombination = new KeyCombination(key);
        });

        [Test]
        public void TestLeaderboardAndSubmission()
        {
            ClickerSubmission? submitted = null;
            string? lastSort = null;

            AddStep("reset clicker", resetClicker);
            AddStep("fake clicker server", () => ((DummyAPIAccess)API).HandleRequest = request =>
            {
                switch (request)
                {
                    case SubmitClickerScoreRequest submit:
                        submitted = submit.Submission;
                        submit.TriggerSuccess(new ClickerSubmitResponse
                        {
                            Accepted = true,
                            RankPp = 2,
                            RankBpm = 1,
                            RankPrestige = 3,
                            ModifierRaw = new ClickerModifierResponse
                            {
                                Id = "taiko_week",
                                ProducerMult = { ["taiko"] = 2 },
                                Name = new ClickerLocalisedNameResponse { En = "Taiko week: drums x2", De = "Taiko-Woche: Trommeln x2" },
                                EndsAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
                            },
                        });
                        return true;

                    case GetClickerLeaderboardRequest board:
                        lastSort = board.Sort;
                        board.TriggerSuccess(new ClickerLeaderboardResponse
                        {
                            Sort = board.Sort,
                            Total = 2,
                            Entries =
                            {
                                new ClickerLeaderboardEntry { Rank = 1, UserId = 1001, Username = "Hodenlos", CountryCode = "DE", TotalEarned = 123_456_789, BestBpm = 230, Buildings = 80, Rebirths = 3, Prestige = 7, Medals = 12 },
                                new ClickerLeaderboardEntry { Rank = 2, UserId = API.LocalUser.Value.OnlineID, Username = API.LocalUser.Value.Username, CountryCode = "DE", TotalEarned = 42, BestBpm = 180 },
                            },
                        });
                        return true;
                }

                return false;
            });

            openClicker();
            AddRepeatStep("press Z", () => InputManager.Key(Key.Z), 15);

            clickTab("Leaderboard");
            AddUntilStep("other player listed", () => hasText("Hodenlos"));
            AddUntilStep("value formatted", () => hasText("123.46M PP"));
            AddAssert("progress submitted", () => submitted != null && submitted.TotalEarned >= 15 && submitted.Clicks == 15);
            AddAssert("submission carries the BPM epoch and season", () => submitted != null && submitted.BpmEpoch == 3 && submitted.Season == 2);
            AddUntilStep("own ranks shown", () => hasTextContaining("PP #2 · BPM #1 · Prestige #3"));
            AddUntilStep("weekly rule banner shown", () => hasVisibleText("This week: Taiko week: drums x2"));

            AddStep("sort by BPM", () => clickSortTab("Tapping BPM"));
            AddUntilStep("BPM values shown", () => hasText("230 BPM"));

            AddStep("sort by prestige", () => clickSortTab("Prestige"));
            AddUntilStep("prestige requested", () => lastSort == "prestige");
            AddUntilStep("prestige values shown", () => hasText("7 prestige"));

            AddStep("sort by medals", () => clickSortTab("Medals"));
            AddUntilStep("medals requested", () => lastSort == "medals");
            AddUntilStep("medal counts shown", () => hasText("12 medals"));

            clickTab("Shop");
            AddUntilStep("shop visible again", () => clicker.ChildrenOfType<RoundedButton>().Any(b => b.IsPresent));
            AddStep("clean up request handler", () => ((DummyAPIAccess)API).HandleRequest = null);
        }

        [Test]
        public void TestPanelTabs()
        {
            AddStep("reset clicker", resetClicker);
            openClicker();

            clickTab("Prestige");
            AddUntilStep("prestige panel shown", () => panel<ClickerPrestigePanel>().Alpha == 1 && hasVisibleText("Rebirths: 0"));
            AddAssert("tree header shown", () => hasVisibleTextContaining("Prestige tree · 0 / 34 nodes"));
            AddAssert("rebirth needs PP first", () => clicker.ChildrenOfType<RoundedButton>().Any(b => b.Text.ToString().StartsWith("Rebirth", StringComparison.Ordinal) && !b.Enabled.Value));

            clickTab("Medals");
            AddUntilStep("medals panel shown", () => panel<ClickerMedalsPanel>().Alpha == 1 && hasVisibleText("0 / 38 medals · +0 % production"));
            AddAssert("all medals listed", () => clicker.ChildrenOfType<ClickerMedalTile>().Count() == 38);

            clickTab("Stats");
            AddUntilStep("stats panel shown", () => panel<ClickerStatsPanel>().Alpha == 1 && hasVisibleText("Lifetime PP"));
            AddAssert("receipt section shown", () => hasVisibleText("Nothing earned while you were away."));

            clickTab("Shop");
            AddUntilStep("shop shown again", () => panel<ClickerPrestigePanel>().Alpha == 0 && hasVisibleText("Buildings"));
        }

        [Test]
        public void TestShopStarsAndAbilities()
        {
            AddStep("reset clicker", resetClicker);
            AddStep("rich player with ten cursor trails", () =>
            {
                engine.State.Points = 1_000_000;
                engine.State.Producers["cursor"] = 10;
                engine.Economy.Recalculate();
            });
            openClicker();

            AddUntilStep("star row shown", () => shopRow("Star 1: x2 for Cursor trail") != null);
            AddStep("buy the star", () => clickShopButton("Star 1: x2 for Cursor trail"));
            AddUntilStep("star bought", () => engine.State.Stars.GetValueOrDefault("cursor") == 1);
            AddUntilStep("star row gone until 25 cursor trails", () => shopRow("Star 1: x2 for Cursor trail") == null);

            AddStep("scroll to Kiai Time", () => scrollIntoView(shopRow("Kiai Time")!));
            AddStep("unlock Kiai Time", () => clickShopButton("Kiai Time"));
            AddUntilStep("kiai unlocked", () => engine.State.Abilities.Contains("kiai"));
            AddUntilStep("play area button ready", () => abilityButton("kiai").Enabled.Value);
            AddStep("activate via the play area", () =>
            {
                InputManager.MoveMouseTo(abilityButton("kiai"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("kiai active", () => engine.Economy.AbilityActive("kiai", engine.Now));
            AddAssert("play area button cannot be pressed again", () => !abilityButton("kiai").Enabled.Value);

            AddStep("scroll to the top", () => clicker.ChildrenOfType<OsuScrollContainer>().First().ScrollToStart(false));
            AddStep("choose x10", () =>
            {
                InputManager.MoveMouseTo(clicker.ChildrenOfType<ClickerPillButton>().First(p => p.Label.ToString() == "x10"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("bulk price shown", () => hasTextStarting("x10 · "));
            AddStep("press 1", () => InputManager.Key(Key.Number1));
            AddUntilStep("ten more cursor trails", () => engine.State.Producers["cursor"] == 20);

            AddStep("scroll to the stance selector", () => scrollIntoView(clicker.ChildrenOfType<ClickerStanceSelector>().Single()));
            AddAssert("stances still locked", () => hasVisibleText("Unlocks at 50 buildings (20 / 50)."));
        }

        [Test]
        public void TestPrestigeRebirth()
        {
            AddStep("reset clicker", resetClicker);
            AddStep("lifetime PP worth ten prestige points", () =>
            {
                engine.State.TotalEarned = 1.1e11;
                engine.State.Points = 500;
                engine.State.Producers["cursor"] = 3;
                engine.Economy.Recalculate();
            });
            openClicker();

            clickTab("Prestige");
            AddUntilStep("rebirth offers ten points", () => rebirthButton() is { } button && button.Enabled.Value && button.Text.ToString() == "Rebirth: +10 prestige points");
            AddStep("click rebirth", () =>
            {
                InputManager.MoveMouseTo(rebirthButton()!);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("new account dialog shown", () => dialogOverlay.CurrentDialog is ClickerConfirmDialog);
            AddStep("hold to confirm", () => dialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("reborn", () => engine.State.Rebirths == 1 && engine.State.PrestigeClaimed == 10);
            AddAssert("run reset", () => engine.State.Points == 0 && engine.State.Producers.GetValueOrDefault("cursor") == 0);
            AddAssert("lifetime PP kept", () => engine.State.TotalEarned >= 1.1e11);
            AddUntilStep("panel updated", () => hasVisibleText("Rebirths: 1"));
        }

        [Test]
        public void TestPrestigeTreeAndRespec()
        {
            AddStep("reset clicker", resetClicker);
            AddStep("twenty prestige points", () =>
            {
                engine.State.PrestigeClaimed = 20;
                engine.State.Rebirths = 1;
                engine.Economy.Recalculate();
            });
            openClicker();

            clickTab("Prestige");
            AddUntilStep("no nodes yet", () => hasVisibleTextContaining("0 / 34 nodes · 20 points to spend"));
            AddAssert("aim_2 locked behind aim_1", () => !engine.Economy.CanBuyNode(engine.Balance.Node("aim_2")!));

            AddStep("scroll to aim_1", () => scrollIntoView(node("aim_1")));
            AddStep("buy aim_1", () =>
            {
                InputManager.MoveMouseTo(node("aim_1"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("node owned", () => engine.State.Tree.Contains("aim_1"));
            AddUntilStep("tree header counts it", () => hasVisibleTextContaining("1 / 34 nodes · 19 points to spend"));
            AddAssert("aim_2 buyable now", () => engine.Economy.CanBuyNode(engine.Balance.Node("aim_2")!));

            AddStep("scroll to respec", () => scrollIntoView(respecButton()));
            AddStep("click respec", () =>
            {
                InputManager.MoveMouseTo(respecButton());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("respec dialog shown", () => dialogOverlay.CurrentDialog is ClickerConfirmDialog);
            AddStep("hold to confirm", () => dialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("tree cleared", () => engine.State.Tree.Count == 0);
            AddAssert("a tenth burned", () => engine.State.PrestigeBurned == 2 && engine.Economy.PrestigeAvailable == 18);
        }

        [Test]
        public void TestMedalsTab()
        {
            AddStep("reset clicker", resetClicker);
            AddStep("99 clicks so far", () => engine.State.Clicks = 99);
            openClicker();

            AddStep("press Z", () => InputManager.Key(Key.Z));
            AddUntilStep("first steps medal earned", () => engine.State.Medals.Contains("clicks_100"));

            clickTab("Medals");
            AddUntilStep("header counts one medal", () => hasVisibleText("1 / 38 medals · +2 % production"));
            AddUntilStep("tile earned", () => medalTile("clicks_100").Earned);
            AddAssert("next tile locked with progress", () => !medalTile("clicks_1k").Earned && medalTile("clicks_1k").ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "100 / 1,000"));
        }

        [Test]
        public void TestVolumePopover()
        {
            double previous = 1;

            AddStep("reset clicker", resetClicker);
            openClicker();

            AddAssert("popover hidden", () => popover().State.Value == Visibility.Hidden);
            AddStep("click the speaker", () =>
            {
                InputManager.MoveMouseTo(clicker.ChildrenOfType<IconButton>().First());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("popover shown", () => popover().State.Value == Visibility.Visible);
            AddAssert("three sliders", () => popover().ChildrenOfType<RoundedSliderBar<double>>().Count() == 3);

            AddStep("set music to 30 %", () =>
            {
                previous = audio.VolumeTrack.Value;
                popover().ChildrenOfType<RoundedSliderBar<double>>().ElementAt(1).Current.Value = 0.3;
            });
            AddAssert("track volume follows", () => audio.VolumeTrack.Value, () => Is.EqualTo(0.3).Within(0.01));
            AddStep("restore volume", () => audio.VolumeTrack.Value = previous);
        }

        [Test]
        public void TestSliderEvent()
        {
            double before = 0;

            AddStep("reset clicker", resetClicker);
            openClicker();

            AddStep("spawn a slider", () => clicker.SpawnSlider());
            AddUntilStep("slider shown", () => clicker.ChildrenOfType<ClickerSliderEvent>().Any());
            AddStep("remember points", () => before = engine.State.Points);
            AddStep("follow it perfectly", () => clicker.ChildrenOfType<ClickerSliderEvent>().Single().CompleteForTests(1));
            AddAssert("reward paid", () => engine.State.Points > before && engine.State.SlidersDone == 1);
            AddAssert("accuracy shown", () => hasTextContaining("(100 %)"));
            AddAssert("slider medal earned", () => engine.State.Medals.Contains("slider_perfect"));
            AddUntilStep("slider gone", () => !clicker.ChildrenOfType<ClickerSliderEvent>().Any());
        }

        [Test]
        public void TestExpedition()
        {
            AddStep("reset clicker", resetClicker);
            AddStep("tournament host with four taiko drums", () =>
            {
                engine.State.PrestigeClaimed = 6;
                engine.State.Tree.Add("rank_1");
                engine.State.Tree.Add("rank_2");
                engine.State.Producers["taiko"] = 4;
                engine.Economy.Recalculate();
            });
            openClicker();

            AddUntilStep("expedition section shown", () => clicker.ChildrenOfType<ClickerExpeditionSection>().Any() && hasVisibleText("Expeditions"));
            AddStep("scroll to expeditions", () => scrollIntoView(clicker.ChildrenOfType<ClickerExpeditionSection>().Single()));
            AddStep("pick the taiko drums", () =>
            {
                InputManager.MoveMouseTo(pill("Taiko drum (4)"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("send button ready", () => sendButton() is { } button && button.Enabled.Value && button.Text.ToString() == "Send 2 × Taiko drum for 30 min");
            AddStep("send them", () =>
            {
                InputManager.MoveMouseTo(sendButton()!);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("expedition running", () => engine.State.Expeditions.Count == 1 && engine.State.Expeditions[0].Count == 2);
            AddUntilStep("countdown shown", () => hasTextContaining("2 × Taiko drum · back in"));
            AddAssert("only two drums produce", () => engine.Economy.ProducerPerSecond("taiko"), () => Is.EqualTo(2 * engine.Economy.DisplayRate("taiko")).Within(1e-6));

            AddStep("time travel", () => engine.State.Expeditions[0].EndsAt = engine.Now - 1);
            AddUntilStep("expedition returned", () => engine.State.Expeditions.Count == 0);
            AddUntilStep("notice shown", () => hasTextContaining("Expedition back"));
        }

        [Test]
        public void TestMenuOpacity()
        {
            AddStep("set menu opacity 50%", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksMenuOpacity, 0.5f));
            AddAssert("menu is half transparent", () => ((Drawable)Game.ScreenStack.CurrentScreen).Colour.TopLeft.Linear.A, () => Is.EqualTo(0.5f).Within(0.01f));
            AddStep("reset menu opacity", () => Game.LocalConfig.SetValue(OsuSetting.BanchosucksMenuOpacity, 1f));
            AddAssert("menu is opaque", () => ((Drawable)Game.ScreenStack.CurrentScreen).Colour.TopLeft.Linear.A, () => Is.EqualTo(1f).Within(0.01f));
        }

        // ------------------------------------------------------------------ helpers

        private OsuClickerScreen clicker => (OsuClickerScreen)Game.ScreenStack.CurrentScreen;

        private ClickerEngine engine => Game.Dependencies.Get<ClickerEngine>();

        private IDialogOverlay dialogOverlay => Game.Dependencies.Get<IDialogOverlay>();

        private AudioManager audio => Game.Dependencies.Get<AudioManager>();

        private void resetClicker()
        {
            // the texts under test are English; the server this runs on has a German locale
            Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "en");
            clickerStorage.Delete(ClickerEngine.SAVE_FILE);
            clickerStorage.Delete(ClickerEngine.BACKUP_FILE);
            engine.ResetForTests();
        }

        private void openClicker()
        {
            AddStep("open clicker", () => Game.ScreenStack.Push(new OsuClickerScreen()));
            AddUntilStep("clicker open", () => Game.ScreenStack.CurrentScreen is OsuClickerScreen screen && screen.IsLoaded);
        }

        /// <summary>
        /// Clicks one of the side panel tabs (the first tab button with that label; the leaderboard's sort tabs come later in the tree).
        /// </summary>
        private void clickTab(string label) => AddStep($"open {label} tab", () =>
        {
            InputManager.MoveMouseTo(clicker.ChildrenOfType<ClickerTabButton>().First(t => t.ChildrenOfType<OsuSpriteText>().Any(s => s.Text.ToString() == label)));
            InputManager.Click(MouseButton.Left);
        });

        private void clickSortTab(string label)
        {
            var board = clicker.ChildrenOfType<ClickerLeaderboardPanel>().Single();
            InputManager.MoveMouseTo(board.ChildrenOfType<ClickerTabButton>().First(t => t.ChildrenOfType<OsuSpriteText>().Any(s => s.Text.ToString() == label)));
            InputManager.Click(MouseButton.Left);
        }

        private T panel<T>() where T : Drawable => clicker.ChildrenOfType<T>().Single();

        private ClickerShopRow? shopRow(string titlePrefix) => clicker.ChildrenOfType<ClickerShopRow>().FirstOrDefault(r => r.Title.ToString().StartsWith(titlePrefix, StringComparison.Ordinal));

        private void clickShopButton(string titlePrefix)
        {
            var row = shopRow(titlePrefix) ?? throw new InvalidOperationException($"no shop row \"{titlePrefix}\"");
            scrollIntoView(row);
            InputManager.MoveMouseTo(row.ChildrenOfType<RoundedButton>().Single());
            InputManager.Click(MouseButton.Left);
        }

        private static void scrollIntoView(Drawable drawable) => drawable.FindClosestParent<OsuScrollContainer>()!.ScrollIntoView(drawable, false);

        private ClickerAbilityButton abilityButton(string id) => clicker.ChildrenOfType<ClickerAbilityButton>().First(b => b.Ability.Id == id);

        private RoundedButton? rebirthButton() => clicker.ChildrenOfType<RoundedButton>().FirstOrDefault(b => b.Text.ToString().StartsWith("Rebirth", StringComparison.Ordinal));

        private RoundedButton respecButton() => clicker.ChildrenOfType<RoundedButton>().First(b => b.Text.ToString().StartsWith("Respec", StringComparison.Ordinal));

        private RoundedButton? sendButton() => clicker.ChildrenOfType<ClickerExpeditionSection>().Single().ChildrenOfType<RoundedButton>().FirstOrDefault();

        private ClickerTreeNodeBox node(string id) => clicker.ChildrenOfType<ClickerTreeNodeBox>().First(n => n.Node.Id == id);

        private ClickerMedalTile medalTile(string id) => clicker.ChildrenOfType<ClickerMedalTile>().First(t => t.Medal.Id == id);

        private ClickerPillButton pill(string label) => clicker.ChildrenOfType<ClickerPillButton>().First(p => p.Label.ToString() == label);

        private ClickerVolumePopover popover() => clicker.ChildrenOfType<ClickerVolumePopover>().Single();

        private bool hasText(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == text);

        private bool hasTextContaining(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains(text, StringComparison.Ordinal));

        private bool hasTextStarting(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().StartsWith(text, StringComparison.Ordinal));

        /// <summary>
        /// Like <see cref="hasText"/>, but the text and all of its parents have to be visible (hidden tabs do not count).
        /// </summary>
        private bool hasVisibleText(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == text && isVisible(t));

        private bool hasVisibleTextContaining(string text) => ((Drawable)Game.ScreenStack.CurrentScreen).ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains(text, StringComparison.Ordinal) && isVisible(t));

        private static bool isVisible(Drawable drawable)
        {
            for (Drawable? current = drawable; current != null; current = current.Parent)
            {
                if (current.Alpha <= 0)
                    return false;
            }

            return true;
        }
    }
}

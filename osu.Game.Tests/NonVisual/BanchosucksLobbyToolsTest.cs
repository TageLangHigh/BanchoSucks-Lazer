// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Lobby;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksLobbyToolsTest
    {
        private const BanchosucksLobbyTeam red = BanchosucksLobbyTeam.Red;
        private const BanchosucksLobbyTeam blue = BanchosucksLobbyTeam.Blue;
        private const BanchosucksBanPickAction ban = BanchosucksBanPickAction.Ban;
        private const BanchosucksBanPickAction pick = BanchosucksBanPickAction.Pick;

        private static List<BanchosucksLobbyPlayer> players(int count) => Enumerable.Range(1, count).Select(i => new BanchosucksLobbyPlayer(i, $"P{i}")).ToList();

        private static string[] maps(int count) => Enumerable.Range(1, count).Select(i => $"Map {i}").ToArray();

        [TearDown]
        public void TearDown() => ClickerStrings.IsGerman = false;

        #region Random

        [Test]
        public void TestRandomMatchesTheReferenceValues()
        {
            // worked out with a separate implementation of FNV-1a + SplitMix64; these must never change,
            // or a seed a host announced no longer gives the teams it gave
            var random = new BanchosucksLobbyRandom("banchosucks");

            Assert.That(new[] { random.NextUInt64(), random.NextUInt64(), random.NextUInt64() },
                Is.EqualTo(new[] { 9762020395394626520UL, 9230275443258663925UL, 9121288417063767391UL }));

            // a seed with a letter outside ASCII goes in as UTF-8
            var umlaut = new BanchosucksLobbyRandom("Würfel 7");

            Assert.That(Enumerable.Range(0, 8).Select(_ => umlaut.Next(100)), Is.EqualTo(new[] { 5, 93, 84, 84, 76, 58, 84, 60 }));
        }

        [Test]
        public void TestRandomStaysInRange()
        {
            var random = new BanchosucksLobbyRandom("range");
            var seen = new HashSet<int>();

            for (int i = 0; i < 2000; i++)
            {
                int value = random.Next(7);

                Assert.That(value, Is.InRange(0, 6));
                seen.Add(value);
            }

            Assert.That(seen, Has.Count.EqualTo(7));
            Assert.That(() => random.Next(0), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(BanchosucksLobbyRandom.NewSeed(), Does.Match("^[0-9]{6}$"));
        }

        #endregion

        #region Teams

        [Test]
        public void TestDrawMatchesTheReferenceDraws()
        {
            var first = BanchosucksLobbyDraw.DrawTeams(players(7), "123456");

            Assert.That(first.Seed, Is.EqualTo("123456"));
            Assert.That(first.Red.Select(p => p.UserId), Is.EqualTo(new[] { 1, 3, 7 }));
            Assert.That(first.Blue.Select(p => p.UserId), Is.EqualTo(new[] { 4, 5, 2, 6 }));

            var second = BanchosucksLobbyDraw.DrawTeams(players(7), "finale");

            Assert.That(second.Red.Select(p => p.UserId), Is.EqualTo(new[] { 6, 3, 2, 5 }));
            Assert.That(second.Blue.Select(p => p.UserId), Is.EqualTo(new[] { 4, 1, 7 }));
        }

        [Test]
        public void TestDrawIsFairAndRepeatable()
        {
            for (int count = 2; count <= 16; count++)
            {
                var everyone = players(count);
                var result = BanchosucksLobbyDraw.DrawTeams(everyone, $"seed {count}");

                Assert.That(result.Red.Concat(result.Blue).Select(p => p.UserId), Is.EquivalentTo(everyone.Select(p => p.UserId)), "everybody is in exactly one team");
                Assert.That(System.Math.Abs(result.Red.Count - result.Blue.Count), Is.LessThanOrEqualTo(1));

                // the order the players joined in does not matter, only who is there and the seed
                var reversed = BanchosucksLobbyDraw.DrawTeams(Enumerable.Reverse(everyone), $"seed {count}");

                Assert.That(reversed.Red, Is.EqualTo(result.Red));
                Assert.That(reversed.Blue, Is.EqualTo(result.Blue));

                foreach (var player in everyone)
                    Assert.That(result.TeamOf(player.UserId), Is.EqualTo(result.Red.Contains(player) ? red : blue));
            }

            Assert.That(BanchosucksLobbyDraw.DrawTeams(players(4), "x").TeamOf(99), Is.Null);
        }

        [Test]
        public void TestSeedDecidesTheTeamsAndWhoGetsTheOddPlayer()
        {
            var everyone = players(5);
            var splits = new HashSet<string>();
            int redBigger = 0, blueBigger = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                var result = BanchosucksLobbyDraw.DrawTeams(everyone, seed.ToString());

                splits.Add(string.Join(",", result.Red.Select(p => p.UserId).OrderBy(id => id)));

                if (result.Red.Count > result.Blue.Count)
                    redBigger++;
                else
                    blueBigger++;
            }

            Assert.That(splits.Count, Is.GreaterThan(10), "different seeds give different teams");
            Assert.That(redBigger, Is.GreaterThan(50), "neither colour always gets the fifth player");
            Assert.That(blueBigger, Is.GreaterThan(50));
        }

        [Test]
        public void TestDrawTakesEveryPlayerOnce()
        {
            var doubled = players(4).Concat(new[] { new BanchosucksLobbyPlayer(2, "P2 again") });
            var result = BanchosucksLobbyDraw.DrawTeams(doubled, "dup");

            Assert.That(result.Red.Count + result.Blue.Count, Is.EqualTo(4));

            var nobody = BanchosucksLobbyDraw.DrawTeams(Enumerable.Empty<BanchosucksLobbyPlayer>(), "empty");

            Assert.That(nobody.Red, Is.Empty);
            Assert.That(nobody.Blue, Is.Empty);
        }

        #endregion

        #region Roll

        [Test]
        public void TestRollAndHighest()
        {
            var numbers = new Queue<int>(new[] { 17, 100, 100, 250 });
            var rolls = BanchosucksLobbyDraw.Roll(players(4), (min, max) =>
            {
                Assert.That((min, max), Is.EqualTo((1, 100)));
                return numbers.Dequeue();
            });

            // a source that misbehaves cannot produce a roll outside 1 to 100
            Assert.That(rolls.Select(r => r.Roll), Is.EqualTo(new[] { 17, 100, 100, 100 }));
            Assert.That(rolls.Select(r => r.Player.UserId), Is.EqualTo(new[] { 1, 2, 3, 4 }));

            Assert.That(BanchosucksLobbyDraw.Highest(rolls).Select(p => p.UserId), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(BanchosucksLobbyDraw.Highest(rolls.Take(2).ToList()).Single().UserId, Is.EqualTo(2));
            Assert.That(BanchosucksLobbyDraw.Highest(new List<(BanchosucksLobbyPlayer, int)>()), Is.Empty);
        }

        #endregion

        #region Ban / pick board

        [Test]
        public void TestTurnOrderWithOneBanEach()
        {
            var board = new BanchosucksBanPickBoard(maps(7), 1, red);
            var turns = new List<(BanchosucksLobbyTeam, BanchosucksBanPickAction)>();

            for (int map = 0; board.NextTurn is var (team, action); map++)
            {
                turns.Add((team, action));
                Assert.That(board.Decider, Is.Null);
                Assert.That(board.TryApply(map, out var entry));
                Assert.That(entry, Is.EqualTo(new BanchosucksBanPickEntry(map, team, action)));
            }

            // red bans first, so blue picks first; the teams take turns until one map is left
            Assert.That(turns, Is.EqualTo(new[]
            {
                (red, ban), (blue, ban),
                (blue, pick), (red, pick), (blue, pick), (red, pick),
            }));

            Assert.That(board.Decider, Is.EqualTo(6));
            Assert.That(board.FreeMaps, Is.EqualTo(new[] { 6 }));
            Assert.That(board.PicksOf(blue), Is.EqualTo(new[] { 2, 4 }));
            Assert.That(board.PicksOf(red), Is.EqualTo(new[] { 3, 5 }));
            Assert.That(board.TryApply(6, out _), Is.False, "the decider belongs to nobody");
        }

        [Test]
        public void TestTwoBansEachAndBlueFirst()
        {
            var board = new BanchosucksBanPickBoard(maps(8), 2, blue);
            var turns = new List<(BanchosucksLobbyTeam, BanchosucksBanPickAction)>();

            for (int map = 0; board.NextTurn is var (team, action); map++)
            {
                turns.Add((team, action));
                board.TryApply(map, out _);
            }

            Assert.That(turns, Is.EqualTo(new[]
            {
                (blue, ban), (red, ban), (blue, ban), (red, ban),
                (red, pick), (blue, pick), (red, pick),
            }));
            Assert.That(board.Decider, Is.EqualTo(7));
        }

        [Test]
        public void TestWithoutBansTheFirstTeamPicksFirst()
        {
            var board = new BanchosucksBanPickBoard(maps(3), 0, red);

            Assert.That(board.NextTurn, Is.EqualTo((red, pick)));
            Assert.That(board.TryApply(1, out _));
            Assert.That(board.NextTurn, Is.EqualTo((blue, pick)));
            Assert.That(board.TryApply(0, out _));
            Assert.That(board.NextTurn, Is.Null);
            Assert.That(board.Decider, Is.EqualTo(2));
        }

        [Test]
        public void TestBansAreLoweredSoADeciderRemains()
        {
            Assert.That(new BanchosucksBanPickBoard(maps(7), 9, red).BansPerTeam, Is.EqualTo(3), "seven maps: three bans each leave one");
            Assert.That(new BanchosucksBanPickBoard(maps(20), 9, red).BansPerTeam, Is.EqualTo(BanchosucksBanPickBoard.MAX_BANS_PER_TEAM));
            Assert.That(new BanchosucksBanPickBoard(maps(3), 2, red).BansPerTeam, Is.EqualTo(1));
            Assert.That(new BanchosucksBanPickBoard(maps(2), 2, red).BansPerTeam, Is.Zero);
            Assert.That(new BanchosucksBanPickBoard(maps(5), -3, red).BansPerTeam, Is.Zero);

            // three maps, one ban each: after the bans the last map is the decider right away
            var small = new BanchosucksBanPickBoard(maps(3), 1, red);

            small.TryApply(0, out _);
            small.TryApply(1, out _);

            Assert.That(small.NextTurn, Is.Null);
            Assert.That(small.Decider, Is.EqualTo(2));

            var single = new BanchosucksBanPickBoard(maps(1), 1, red);

            Assert.That(single.NextTurn, Is.Null);
            Assert.That(single.Decider, Is.Null, "one map is no board");

            Assert.That(new BanchosucksBanPickBoard(maps(0), 1, red).NextTurn, Is.Null);
        }

        [Test]
        public void TestTakenAndUnknownMapsAreRefused()
        {
            var board = new BanchosucksBanPickBoard(maps(5), 1, red);

            Assert.That(board.TryApply(-1, out _), Is.False);
            Assert.That(board.TryApply(5, out _), Is.False);
            Assert.That(board.TryApply(2, out _));
            Assert.That(board.TryApply(2, out _), Is.False);

            Assert.That(board.History, Has.Count.EqualTo(1));
            Assert.That(board.NextTurn, Is.EqualTo((blue, ban)), "a refused step does not use up the turn");
            Assert.That(board.EntryOf(2), Is.EqualTo(new BanchosucksBanPickEntry(2, red, ban)));
            Assert.That(board.EntryOf(3), Is.Null);
        }

        [Test]
        public void TestUndoGivesTheTurnBack()
        {
            var board = new BanchosucksBanPickBoard(maps(5), 1, red);

            Assert.That(board.Undo(), Is.Null);

            board.TryApply(0, out _);
            board.TryApply(1, out _);
            board.TryApply(2, out _);

            Assert.That(board.NextTurn, Is.EqualTo((red, pick)));

            Assert.That(board.Undo(), Is.EqualTo(new BanchosucksBanPickEntry(2, blue, pick)));
            Assert.That(board.NextTurn, Is.EqualTo((blue, pick)));
            Assert.That(board.EntryOf(2), Is.Null);

            Assert.That(board.Undo(), Is.EqualTo(new BanchosucksBanPickEntry(1, blue, ban)));
            Assert.That(board.NextTurn, Is.EqualTo((blue, ban)));

            // the map that was taken back can be taken again, also by the other action
            Assert.That(board.TryApply(2, out var entry));
            Assert.That(entry, Is.EqualTo(new BanchosucksBanPickEntry(2, blue, ban)));
        }

        #endregion

        #region Texts

        [Test]
        public void TestTeamDrawAnnouncement()
        {
            var result = BanchosucksLobbyDraw.DrawTeams(players(7), "123456");

            string message = BanchosucksLobbyToolsText.TeamDraw(result, false).Single();

            Assert.That(message, Is.EqualTo("Team draw, 7 players, seed 123456: Team Red: P1, P3, P7. Team Blue: P4, P5, P2, P6."));

            Assert.That(BanchosucksLobbyToolsText.TeamDraw(result, true).Single(), Does.EndWith("Please switch to your team yourselves."));

            ClickerStrings.IsGerman = true;

            Assert.That(BanchosucksLobbyToolsText.TeamDraw(result, true).Single(),
                Is.EqualTo("Team-Auslosung, 7 Spieler, Seed 123456: Team Rot: P1, P3, P7. Team Blau: P4, P5, P2, P6. Bitte wechselt selbst in euer Team."));
        }

        [Test]
        public void TestLongAnnouncementsAreSplitBetweenNames()
        {
            var crowd = Enumerable.Range(1, 16).Select(i => new BanchosucksLobbyPlayer(i, $"A rather long player name number {i:00}")).ToList();
            var messages = BanchosucksLobbyToolsText.TeamDraw(BanchosucksLobbyDraw.DrawTeams(crowd, "long"), true);

            Assert.That(messages.Count, Is.GreaterThan(1));
            Assert.That(messages, Has.All.Length.LessThanOrEqualTo(BanchosucksLobbyToolsText.MAX_MESSAGE_LENGTH));

            string joined = string.Join(" ", messages);

            foreach (var player in crowd)
                Assert.That(joined, Does.Contain(player.Name), "no name is cut in two or lost");

            // a single part that is too long on its own is shortened rather than sent whole
            Assert.That(BanchosucksLobbyToolsText.Pack(new[] { new string('x', 1000) }).Single().Length, Is.EqualTo(BanchosucksLobbyToolsText.MAX_MESSAGE_LENGTH));
            Assert.That(BanchosucksLobbyToolsText.Pack(System.Array.Empty<string>()), Is.Empty);
        }

        [Test]
        public void TestRollAnnouncement()
        {
            var two = players(2);

            Assert.That(BanchosucksLobbyToolsText.Roll(new[] { (two[0], 57), (two[1], 12) }).Single(), Is.EqualTo("Roll 1-100: P1 57, P2 12. Highest: P1."));
            Assert.That(BanchosucksLobbyToolsText.Roll(new[] { (two[0], 40), (two[1], 40) }).Single(), Is.EqualTo("Roll 1-100: P1 40, P2 40. Tie between P1, P2."));
            Assert.That(BanchosucksLobbyToolsText.Roll(new[] { (two[0], 3) }).Single(), Is.EqualTo("Roll 1-100: P1 3."));

            ClickerStrings.IsGerman = true;

            Assert.That(BanchosucksLobbyToolsText.Roll(new[] { (two[0], 57), (two[1], 12) }).Single(), Is.EqualTo("Würfeln 1-100: P1 57, P2 12. Höchster Wurf: P1."));
        }

        [Test]
        public void TestBoardTexts()
        {
            var board = new BanchosucksBanPickBoard(maps(4), 1, red);

            Assert.That(BanchosucksLobbyToolsText.NextTurn(board), Is.EqualTo("Next: Team Red bans"));
            Assert.That(BanchosucksLobbyToolsText.Decider(board), Is.Null);

            board.TryApply(0, out var first);
            Assert.That(BanchosucksLobbyToolsText.BanPick(board, first), Is.EqualTo("Team Red bans: Map 1"));
            Assert.That(BanchosucksLobbyToolsText.State(board, 0), Is.EqualTo("banned by Team Red"));

            board.TryApply(1, out _);
            board.TryApply(3, out var third);

            Assert.That(BanchosucksLobbyToolsText.BanPick(board, third), Is.EqualTo("Team Blue picks: Map 4"));
            Assert.That(BanchosucksLobbyToolsText.State(board, 3), Is.EqualTo("pick 1 of Team Blue"));
            Assert.That(BanchosucksLobbyToolsText.Decider(board), Is.EqualTo("Decider: Map 3"));
            Assert.That(BanchosucksLobbyToolsText.NextTurn(board), Is.EqualTo("Decider: Map 3"));
            Assert.That(BanchosucksLobbyToolsText.State(board, 2), Is.EqualTo("decider"));

            var undone = board.Undo()!.Value;

            Assert.That(BanchosucksLobbyToolsText.Undone(board, undone), Is.EqualTo("Taken back: Team Blue's pick of Map 4"));
            Assert.That(BanchosucksLobbyToolsText.State(board, 2), Is.Empty);

            ClickerStrings.IsGerman = true;

            Assert.That(BanchosucksLobbyToolsText.NextTurn(board), Is.EqualTo("Als Nächstes: Team Blau pickt"));
            Assert.That(BanchosucksLobbyToolsText.BanPick(board, first), Is.EqualTo("Team Rot bannt: Map 1"));
        }

        #endregion
    }
}

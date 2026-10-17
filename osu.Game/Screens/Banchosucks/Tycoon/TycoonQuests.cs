// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    internal enum TycoonRewardKind
    {
        /// <summary>Seconds of the current income, paid at once (at least 20).</summary>
        Cash,
        Inflow,
        Donation,
        Ruf,
    }

    /// <summary>
    /// One quest of the tycoon: a goal read from the state and a reward. Quests come in a fixed order; the save
    /// remembers only the index of the next one (<see cref="TycoonState.Quest"/>), so they are client-side guidance
    /// and the server bounds the rewards like any other income.
    /// </summary>
    internal class TycoonQuest
    {
        public readonly string Key;
        public readonly string TitleEn;
        public readonly string TitleDe;
        public readonly Func<TycoonState, double> Progress;
        public readonly double Target;
        public readonly TycoonRewardKind Reward;

        /// <summary>Seconds of income, the boost factor, or reputation points, depending on <see cref="Reward"/>.</summary>
        public readonly double Amount;

        /// <summary>How long a boost lasts.</summary>
        public readonly double Seconds;

        public TycoonQuest(string key, string titleEn, string titleDe, Func<TycoonState, double> progress, double target, TycoonRewardKind reward, double amount, double seconds = 0)
        {
            Key = key;
            TitleEn = titleEn;
            TitleDe = titleDe;
            Progress = progress;
            Target = target;
            Reward = reward;
            Amount = amount;
            Seconds = seconds;
        }

        public string Title => ClickerStrings.Pick(TitleEn, TitleDe);

        public bool IsComplete(TycoonState state) => Progress(state) >= Target;

        public string RewardText => Reward switch
        {
            TycoonRewardKind.Cash => ClickerStrings.Pick($"{Amount:0} s of income", $"{Amount:0} s Einnahmen"),
            TycoonRewardKind.Inflow => ClickerStrings.Pick($"inflow {ClickerFormat.Multiplier(Amount)} for {Seconds:0} s", $"Zulauf {ClickerFormat.Multiplier(Amount)} für {Seconds:0} s"),
            TycoonRewardKind.Donation => ClickerStrings.Pick($"donations {ClickerFormat.Multiplier(Amount)} for {Seconds:0} s", $"Spenden {ClickerFormat.Multiplier(Amount)} für {Seconds:0} s"),
            _ => ClickerStrings.Pick($"reputation +{Amount:0}", $"Ruf +{Amount:0}"),
        };
    }

    /// <summary>
    /// Banchosucks: the quest line of the Private Server Tycoon, from the first ten players to the second season.
    /// </summary>
    internal static class TycoonQuests
    {
        public static readonly TycoonQuest[] All =
        {
            new TycoonQuest("players10", "Reach 10 players online", "Erreiche 10 Spieler online", s => s.Players, 10, TycoonRewardKind.Cash, 30),
            new TycoonQuest("laptop", "Buy the old laptop", "Kauf den alten Laptop", s => s.Hardware, 1, TycoonRewardKind.Inflow, 2, 60),
            new TycoonQuest("discord3", "Run three Discord servers", "Betreib drei Discord-Server", s => s.MarketingCount("discord"), 3, TycoonRewardKind.Cash, 30),
            new TycoonQuest("earn200", "Earn 200 donations this season", "Verdiene 200 Spenden in dieser Saison", s => s.RunTotal, 200, TycoonRewardKind.Donation, 1.5, 60),
            new TycoonQuest("reddit", "Post on Reddit", "Poste auf Reddit", s => s.MarketingCount("reddit"), 1, TycoonRewardKind.Cash, 30),
            new TycoonQuest("website", "Launch the website", "Bring die Website online", s => s.Has("website") ? 1 : 0, 1, TycoonRewardKind.Inflow, 1.5, 90),
            new TycoonQuest("players50", "Reach 50 players online", "Erreiche 50 Spieler online", s => s.Players, 50, TycoonRewardKind.Cash, 45),
            new TycoonQuest("minipc", "Move to the Mini PC", "Zieh auf den Mini-PC um", s => s.Hardware, 2, TycoonRewardKind.Donation, 1.5, 90),
            new TycoonQuest("team", "Hire your first team member", "Stell das erste Teammitglied ein", s => TycoonRules.StaffTotal(s), 1, TycoonRewardKind.Cash, 45),
            new TycoonQuest("youtube", "Upload a YouTube video", "Lade ein YouTube-Video hoch", s => s.MarketingCount("youtube"), 1, TycoonRewardKind.Inflow, 2, 60),
            new TycoonQuest("players500", "Reach 500 players online", "Erreiche 500 Spieler online", s => s.Players, 500, TycoonRewardKind.Cash, 60),
            new TycoonQuest("deco3", "Put three things on the plot", "Stell drei Dinge auf den Platz", s => s.Layout.Count, 3, TycoonRewardKind.Donation, 1.5, 120),
            new TycoonQuest("vps", "Rent the VPS", "Miete den VPS", s => s.Hardware, 3, TycoonRewardKind.Cash, 60),
            new TycoonQuest("features5", "Have five features live", "Hab fünf Features live", s => s.Features.Count, 5, TycoonRewardKind.Inflow, 2, 90),
            new TycoonQuest("players5000", "Reach 5,000 players online", "Erreiche 5.000 Spieler online", s => s.Players, 5000, TycoonRewardKind.Cash, 90),
            new TycoonQuest("cluster", "Build the server cluster", "Bau den Server-Cluster", s => s.Hardware, 6, TycoonRewardKind.Donation, 2, 120),
            new TycoonQuest("season2", "Start a second season", "Starte eine zweite Saison", s => s.Prestige, 1, TycoonRewardKind.Cash, 120),
            new TycoonQuest("players50000", "Reach 50,000 players online", "Erreiche 50.000 Spieler online", s => s.Players, 50_000, TycoonRewardKind.Cash, 120),
        };

        public static TycoonQuest? Current(TycoonState state) => state.Quest >= 0 && state.Quest < All.Length ? All[state.Quest] : null;

        /// <summary>Pays <paramref name="seconds"/> of the current income at once (at least 20); also used by the supply drops.</summary>
        public static LocalisableString GrantCash(TycoonState state, double seconds)
        {
            double amount = Math.Max(20, TycoonRules.DonationRate(state) * seconds);
            state.Donations += amount;
            state.RunTotal += amount;
            state.LifetimeTotal += amount;
            return ClickerStrings.Text("+{0} donations", "+{0} Spenden", ClickerFormat.Number(amount));
        }

        /// <summary>Hands out the reward; returns the line for the log.</summary>
        public static LocalisableString Apply(TycoonQuest quest, TycoonState state, TycoonBoosts boosts)
        {
            switch (quest.Reward)
            {
                case TycoonRewardKind.Cash:
                    return GrantCash(state, quest.Amount);

                case TycoonRewardKind.Inflow:
                    boosts.SetInflow(quest.Amount, quest.Seconds);
                    return ClickerStrings.Text("Player inflow {0} for {1} seconds", "Zulauf {0} für {1} Sekunden", ClickerFormat.Multiplier(quest.Amount), quest.Seconds);

                case TycoonRewardKind.Donation:
                    boosts.SetDonation(quest.Amount, quest.Seconds);
                    return ClickerStrings.Text("Donations {0} for {1} seconds", "Spenden {0} für {1} Sekunden", ClickerFormat.Multiplier(quest.Amount), quest.Seconds);

                default:
                    state.Ruf = Math.Min(100, state.Ruf + quest.Amount);
                    return ClickerStrings.Text("Reputation +{0}", "Ruf +{0}", quest.Amount);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Banchosucks.Clicker
{
    /// <summary>
    /// Banchosucks: every number the osu! Clicker derives from a <see cref="ClickerState"/> and the balance file.
    /// Pure calculations, no side effects; the server plugin (<c>economy.py</c>) mirrors this class line by line and
    /// both are checked against the same fixture (<c>clicker-balance-fixture.json</c>).
    /// </summary>
    public class ClickerEconomy
    {
        public readonly ClickerBalance Balance;
        public readonly ClickerState State;

        /// <summary>
        /// The weekly server rule in force, if the client knows one.
        /// </summary>
        public ClickerModifier? Modifier;

        public ClickerEconomy(ClickerBalance balance, ClickerState state, ClickerModifier? modifier = null)
        {
            Balance = balance;
            State = state;
            Modifier = modifier;
            Recalculate();
        }

        // --------------------------------------------------------------- derived from the tree

        public double CostGrowth { get; private set; }
        public double CostDiscount { get; private set; }
        public double StarDiscount { get; private set; }
        public int[] StarThresholds { get; private set; } = Array.Empty<int>();
        public double OfflineRate { get; private set; }
        public double OfflineCapSeconds { get; private set; }
        public double PrestigeRate { get; private set; }
        public int ExpeditionSlots { get; private set; }
        public double ExpeditionChance { get; private set; }
        public double AbilityBoost { get; private set; }
        public double EventBoost { get; private set; }
        public bool Wysi { get; private set; }
        public HashSet<string> KeptUpgrades { get; private set; } = new HashSet<string>();
        public Dictionary<string, int> HeadStart { get; private set; } = new Dictionary<string, int>();
        public bool KeepAbilities { get; private set; }

        // --------------------------------------------------------------- multipliers

        public double ProductionMultiplier { get; private set; }
        public double ClickBase { get; private set; }
        public double ClickShare { get; private set; }
        public double StanceClickMult { get; private set; }

        /// <summary>
        /// PP per second without time-limited effects (abilities, warm-up).
        /// </summary>
        public double BasePerSecond { get; private set; }

        public int Buildings { get; private set; }
        public int TotalStars { get; private set; }

        private readonly Dictionary<string, double> producerRates = new Dictionary<string, double>();

        /// <summary>
        /// Recomputes everything that depends only on the state (call after purchases, rebirths, tree changes).
        /// </summary>
        public void Recalculate()
        {
            var b = Balance;
            var s = State;

            CostGrowth = b.CostGrowth;
            CostDiscount = 1;
            StarDiscount = 1;
            OfflineRate = b.OfflineRate;
            OfflineCapSeconds = b.OfflineCapSeconds;
            PrestigeRate = b.PrestigeRate;
            ExpeditionSlots = 0;
            ExpeditionChance = b.Expedition.SuccessChance;
            AbilityBoost = 1;
            EventBoost = 1;
            Wysi = false;
            KeptUpgrades = new HashSet<string>();
            HeadStart = new Dictionary<string, int>();
            KeepAbilities = false;
            bool extraStar = false;

            double productionAdd = 0;
            double productionMult = 1;
            double clickMult = 1;
            double clickShare = 0;

            foreach (string id in s.Tree)
            {
                var node = b.Node(id);
                if (node == null)
                    continue;

                switch (node.Effect)
                {
                    case "production_add": productionAdd += node.Value; break;
                    case "production_mult": productionMult *= node.Value; break;
                    case "click_mult": clickMult *= node.Value; break;
                    case "click_share": clickShare += node.Value; break;
                    case "cost_discount": CostDiscount *= 1 - node.Value; break;
                    case "cost_growth": CostGrowth = Math.Min(CostGrowth, node.Value); break;
                    case "star_discount": StarDiscount = Math.Min(StarDiscount, node.Value); break;
                    case "offline_rate": OfflineRate = Math.Max(OfflineRate, node.Value); break;
                    case "offline_cap": OfflineCapSeconds = Math.Max(OfflineCapSeconds, node.Value); break;
                    case "prestige_rate": PrestigeRate = Math.Max(PrestigeRate, node.Value); break;
                    case "expeditions":
                        ExpeditionSlots = Math.Max(ExpeditionSlots, (int)node.Value);
                        if (node.Value >= 2) ExpeditionChance = b.Expedition.ImprovedChance;
                        break;
                    case "ability_boost": AbilityBoost = Math.Max(AbilityBoost, node.Value); break;
                    case "event_boost": EventBoost = Math.Max(EventBoost, node.Value); break;
                    case "star_extra": extraStar = true; break;
                    case "wysi": Wysi = true; break;
                    case "keep_upgrades":
                        foreach (string kept in node.Keeps ?? Array.Empty<string>())
                            KeptUpgrades.Add(kept);
                        break;
                    case "head_start":
                        foreach (var (producer, count) in node.HeadStart ?? new Dictionary<string, int>())
                            HeadStart[producer] = Math.Max(HeadStart.GetValueOrDefault(producer), count);
                        break;
                    case "keep_abilities": KeepAbilities = true; break;
                }
            }

            StarThresholds = extraStar ? b.StarThresholds.Append(b.StarExtraThreshold).ToArray() : b.StarThresholds;

            foreach (string id in s.Upgrades)
            {
                var upgrade = b.Upgrade(id);
                if (upgrade == null)
                    continue;

                productionMult *= upgrade.Production;
                clickMult *= upgrade.Click;
                clickShare += upgrade.ClickShare;
            }

            var stance = b.Stance(s.Stance) ?? b.Stances.FirstOrDefault();
            double stanceProduction = stance?.ProductionMult ?? 1;
            StanceClickMult = stance?.ClickMult ?? 1;

            double wysi = Wysi ? b.Node("wysi")?.Value ?? 1 : 1;
            double modifierProduction = Modifier?.ProductionMult ?? 1;

            ProductionMultiplier = productionMult
                                   * (1 + productionAdd)
                                   * PrestigeMultiplier(Balance, PrestigeEffective, PrestigeRate)
                                   * (1 + s.Medals.Count * b.MedalRate)
                                   * stanceProduction
                                   * wysi
                                   * modifierProduction;
            ClickBase = clickMult;
            ClickShare = clickShare;

            Buildings = 0;
            TotalStars = 0;
            producerRates.Clear();
            double total = 0;

            foreach (var producer in b.Producers)
            {
                int count = s.Producers.GetValueOrDefault(producer.Id);
                Buildings += count;
                int stars = s.Stars.GetValueOrDefault(producer.Id);
                TotalStars += stars;
                int away = s.Expeditions.Where(e => e.Producer == producer.Id).Sum(e => e.Count);
                double rate = SingleRate(producer.Id);
                double perSecond = Math.Max(count - away, 0) * rate;
                producerRates[producer.Id] = perSecond;
                total += perSecond;
            }

            // prestige buildings: bought with prestige points, survive rebirths, no stars or synergies
            foreach (var producer in b.PrestigeProducers)
            {
                double perSecond = s.PrestigeProducers.GetValueOrDefault(producer.Id) * producer.PerSecond;
                producerRates[producer.Id] = perSecond;
                total += perSecond;
            }

            BasePerSecond = total * ProductionMultiplier;
        }

        /// <summary>
        /// PP per second of one building of the type, before the global production multiplier: stars, synergies and the weekly rule.
        /// </summary>
        public double SingleRate(string producerId)
        {
            var producer = Balance.Producer(producerId);
            if (producer == null)
                return 0;

            int stars = State.Stars.GetValueOrDefault(producerId);
            double synergy = 0;

            foreach (string id in State.Synergies)
            {
                var pair = Balance.Synergy(id);
                if (pair == null || pair.Target != producerId)
                    continue;

                synergy += Math.Min(State.Producers.GetValueOrDefault(pair.Source) * Balance.SynergyPerSource, Balance.SynergyCap);
            }

            double modifier = Modifier?.ProducerMult.GetValueOrDefault(producerId, 1) ?? 1;
            return producer.PerSecond * Math.Pow(2, stars) * (1 + synergy) * modifier;
        }

        /// <summary>
        /// PP per second one building of the type adds right now, with every multiplier applied (for the shop).
        /// </summary>
        public double DisplayRate(string producerId) => SingleRate(producerId) * ProductionMultiplier;

        public double ProducerPerSecond(string producerId) => producerRates.GetValueOrDefault(producerId) * ProductionMultiplier;

        // --------------------------------------------------------------- time-limited effects

        public bool AbilityActive(string id, long now) => State.AbilityActiveUntil.GetValueOrDefault(id) > now;

        public bool AbilityReady(string id, long now) => State.Abilities.Contains(id) && State.AbilityReadyAt.GetValueOrDefault(id) <= now && !AbilityActive(id, now);

        public bool WarmedUp(long now) => State.WarmedUpUntil > now;

        public double TimeMultiplier(long now)
        {
            double mult = 1;

            foreach (var ability in Balance.Abilities)
            {
                if (AbilityActive(ability.Id, now))
                    mult *= ability.ProductionMult * (ability.ProductionMult > 1 ? AbilityBoost : 1);
            }

            if (WarmedUp(now))
                mult *= Balance.Encore.WarmupMult;

            return mult;
        }

        public double KiaiMultiplier(long now)
        {
            double mult = 1;

            foreach (var ability in Balance.Abilities)
            {
                if (AbilityActive(ability.Id, now))
                    mult *= ability.ClickMult * (ability.ClickMult > 1 ? AbilityBoost : 1);
            }

            return mult;
        }

        public double PerSecond(long now) => BasePerSecond * TimeMultiplier(now);

        public double ClickValue(long now)
        {
            double wysi = Wysi ? Balance.Node("wysi")?.Value ?? 1 : 1;
            return (ClickBase + PerSecond(now) * ClickShare) * StanceClickMult * (Modifier?.ClickMult ?? 1) * wysi * KiaiMultiplier(now);
        }

        // --------------------------------------------------------------- prices

        public int Count(string producerId) => State.Producers.GetValueOrDefault(producerId);

        public bool ProducerUnlocked(ClickerProducer producer) => producer.RequiresNode == null || State.Tree.Contains(producer.RequiresNode);

        /// <summary>
        /// Price of the next building when <paramref name="owned"/> are owned already.
        /// </summary>
        public double ProducerCost(ClickerProducer producer, int owned) => Math.Ceiling(producer.BaseCost * Math.Pow(CostGrowth, owned) * CostDiscount);

        public double ProducerCost(ClickerProducer producer) => ProducerCost(producer, Count(producer.Id));

        public double BulkCost(ClickerProducer producer, int amount)
        {
            double total = 0;
            int owned = Count(producer.Id);
            for (int i = 0; i < amount; i++)
                total += ProducerCost(producer, owned + i);
            return total;
        }

        public int MaxAffordable(ClickerProducer producer, double points, int limit = 1000)
        {
            int owned = Count(producer.Id);
            int amount = 0;
            double total = 0;

            while (amount < limit)
            {
                double next = ProducerCost(producer, owned + amount);
                if (total + next > points)
                    break;
                total += next;
                amount++;
            }

            return amount;
        }

        /// <summary>
        /// PP spent on every building above the head start, mirrored on the server for the spend check.
        /// </summary>
        public double SpentOnProducers()
        {
            double spent = 0;

            foreach (var producer in Balance.Producers)
            {
                int free = HeadStart.GetValueOrDefault(producer.Id);
                for (int k = free; k < Count(producer.Id); k++)
                    spent += ProducerCost(producer, k);
            }

            return spent;
        }

        public int NextStarIndex(string producerId) => State.Stars.GetValueOrDefault(producerId);

        public int? NextStarThreshold(string producerId)
        {
            int index = NextStarIndex(producerId);
            return index < StarThresholds.Length ? StarThresholds[index] : null;
        }

        public double StarCost(ClickerProducer producer, int index) => Math.Ceiling(producer.BaseCost * Balance.StarCostBaseMult * Math.Pow(Balance.StarCostGrowth, index) * StarDiscount);

        public bool StarAvailable(ClickerProducer producer)
        {
            int? threshold = NextStarThreshold(producer.Id);
            return threshold != null && Count(producer.Id) >= threshold;
        }

        public double SpentOnStars()
        {
            double spent = 0;
            foreach (var producer in Balance.Producers)
            {
                for (int k = 0; k < State.Stars.GetValueOrDefault(producer.Id); k++)
                    spent += StarCost(producer, k);
            }

            return spent;
        }

        /// <summary>
        /// PP spent on everything owned in the run; the server checks that lifetime PP covers it.
        /// </summary>
        public double SpentTotal()
        {
            double spent = SpentOnProducers() + SpentOnStars();

            foreach (string id in State.Upgrades)
            {
                var upgrade = Balance.Upgrade(id);
                if (upgrade != null && !KeptUpgrades.Contains(id))
                    spent += upgrade.Cost;
            }

            foreach (string id in State.Synergies)
            {
                var synergy = Balance.Synergy(id);
                if (synergy != null)
                    spent += SynergyCost(synergy);
            }

            foreach (string id in State.Abilities)
            {
                var ability = Balance.Ability(id);
                if (ability != null && !KeepAbilities)
                    spent += ability.UnlockCost;
            }

            return spent;
        }

        public double SynergyCost(ClickerSynergy synergy) => (Balance.Producer(synergy.Target)?.BaseCost ?? 0) * Balance.SynergyCostMult;

        public bool SynergyAvailable(ClickerSynergy synergy) =>
            Count(synergy.Source) >= Balance.SynergyMinCount && Count(synergy.Target) >= Balance.SynergyMinCount;

        public bool StanceUnlocked => Buildings >= Balance.StanceUnlockBuildings || KeepAbilities;

        // --------------------------------------------------------------- prestige

        /// <summary>
        /// Permanent production bonus of the prestige points: linear up to <see cref="ClickerBalance.PrestigeSoftcap"/>,
        /// then growing with the square root (mirror of <c>economy.prestige_multiplier</c>). Without the soft cap the
        /// bonus fed on itself: two players finished the whole tree within a day of season 2.
        /// </summary>
        public static double PrestigeMultiplier(ClickerBalance balance, int points, double rate)
        {
            double softcap = balance.PrestigeSoftcap;
            if (softcap <= 0 || points <= softcap)
                return 1 + points * rate;

            return (1 + softcap * rate) * Math.Sqrt(points / softcap);
        }

        /// <summary>
        /// The production bonus the prestige points give right now (for the prestige tab).
        /// </summary>
        public double PrestigeBonus => PrestigeMultiplier(Balance, PrestigeEffective, PrestigeRate);

        public static int PrestigeTotalFor(double lifetime, double divisor) => lifetime <= 0 ? 0 : (int)Math.Floor(Math.Cbrt(lifetime / divisor));

        public int PrestigeTotal => PrestigeTotalFor(State.TotalEarned, Balance.PrestigeDivisor);

        /// <summary>
        /// Points a rebirth would hand out right now.
        /// </summary>
        public int PrestigePending => Math.Max(PrestigeTotal - State.PrestigeClaimed, 0);

        public int PrestigeEffective => Math.Max(State.PrestigeClaimed - State.PrestigeBurned, 0);

        public int PrestigeProducerCount(string id) => State.PrestigeProducers.GetValueOrDefault(id);

        /// <summary>
        /// Price in prestige points of the next prestige building when <paramref name="owned"/> are owned already.
        /// </summary>
        public int PrestigeProducerCost(ClickerPrestigeProducer producer, int owned) => (int)Math.Ceiling(producer.Cost * Math.Pow(Balance.PrestigeProducerGrowth, owned));

        public int PrestigeProducerCost(ClickerPrestigeProducer producer) => PrestigeProducerCost(producer, PrestigeProducerCount(producer.Id));

        public bool PrestigeProducersUnlocked => State.Rebirths >= Balance.PrestigeProducersUnlockRebirths;

        public int PrestigeSpentOnBuildings
        {
            get
            {
                int spent = 0;
                foreach (var producer in Balance.PrestigeProducers)
                {
                    for (int k = 0; k < PrestigeProducerCount(producer.Id); k++)
                        spent += PrestigeProducerCost(producer, k);
                }

                return spent;
            }
        }

        public int PrestigeSpent => State.Tree.Sum(id => Balance.Node(id)?.Cost ?? 0) + PrestigeSpentOnBuildings;

        public int PrestigeAvailable => PrestigeEffective - PrestigeSpent;

        public int RespecCost => (int)Math.Ceiling(PrestigeEffective * Balance.RespecShare);

        /// <summary>
        /// Lifetime PP needed for the next prestige point.
        /// </summary>
        public double NextPrestigeAt => Math.Pow(PrestigeTotal + 1, 3) * Balance.PrestigeDivisor;

        public bool NodeOwned(string id) => State.Tree.Contains(id);

        public bool NodeRequirementsMet(ClickerTreeNode node)
        {
            if (node.Requires != null)
                return node.Requires.All(NodeOwned);

            if (node.Tier <= 1)
                return true;

            var previous = Balance.Tree.FirstOrDefault(n => n.Branch == node.Branch && n.Tier == node.Tier - 1);
            return previous == null || NodeOwned(previous.Id);
        }

        public bool CanBuyNode(ClickerTreeNode node) => !NodeOwned(node.Id) && NodeRequirementsMet(node) && PrestigeAvailable >= node.Cost;

        // --------------------------------------------------------------- medals

        public int MedalProgressValue(ClickerMedal medal)
        {
            switch (medal.Kind)
            {
                case "clicks": return (int)Math.Min(State.Clicks, int.MaxValue);
                case "buildings": return Buildings;
                case "lifetime": return State.TotalEarned >= medal.Value ? 1 : 0;
                case "bpm": return (int)State.BestBpm;
                case "combo": return State.BestCombo;
                case "rebirths": return State.Rebirths;
                case "upgrades": return State.Upgrades.Count;
                case "stars": return TotalStars;
                case "tree": return State.Tree.Count;
                case "tree_node": return medal.Node != null && State.Tree.Contains(medal.Node) ? 1 : 0;
                case "encore": return State.EncoreMaps;
                case "daily": return State.DailyDays;
                case "expeditions": return State.ExpeditionsDone;
                case "sliders": return State.SliderBest >= 0.95 ? 1 : 0;
                default: return 0;
            }
        }

        public bool MedalEarned(ClickerMedal medal)
        {
            switch (medal.Kind)
            {
                case "lifetime": return State.TotalEarned >= medal.Value;
                case "tree_node": return medal.Node != null && State.Tree.Contains(medal.Node);
                case "sliders": return State.SliderBest >= 0.95;
                default: return MedalProgressValue(medal) >= medal.Value;
            }
        }

        /// <summary>
        /// Medals the state qualifies for but does not hold yet.
        /// </summary>
        public IEnumerable<ClickerMedal> NewMedals() => Balance.Medals.Where(m => !State.Medals.Contains(m.Id) && MedalEarned(m));

        // --------------------------------------------------------------- rewards of events

        public double SpinnerReward(long now, bool kiai)
        {
            double reward = Math.Max(ClickValue(now) * Balance.Spinner.ClickMult, PerSecond(now) * Balance.Spinner.ProductionSeconds) * EventBoost;
            return kiai ? reward * Balance.Spinner.KiaiMult : reward;
        }

        public double SliderReward(long now, double accuracy)
        {
            accuracy = Math.Clamp(accuracy, 0, 1);
            return Math.Max(ClickValue(now) * Balance.Slider.ClickMult, PerSecond(now) * Balance.Slider.ProductionSeconds) * accuracy * EventBoost;
        }

        public double EncoreReward(long now) => PerSecond(now) * Balance.Encore.ProductionSeconds + ClickValue(now) * Balance.Encore.Clicks;

        public double DailyReward(long now) => PerSecond(now) * Balance.Daily.ProductionSeconds;

        public double ExpeditionReward(ClickerExpedition expedition) =>
            expedition.Count * expedition.Rate * ProductionMultiplier * (expedition.EndsAt - expedition.StartedAt) * Balance.Expedition.RewardMult;

        public double OfflineEarnings(double seconds) => Math.Min(Math.Max(seconds, 0), OfflineCapSeconds) * BasePerSecond * OfflineRate;
    }
}

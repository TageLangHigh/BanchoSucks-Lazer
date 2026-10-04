// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Screens.Banchosucks.Clicker
{
    /// <summary>
    /// Banchosucks: the osu! Clicker itself, alive for the whole session so buildings keep producing while the
    /// player is in the menu, plays maps or edits. <see cref="OsuClickerScreen"/> only shows and controls it.
    /// </summary>
    /// <remarks>
    /// Rules: the state has exactly one writer (this component, on the update thread); production per frame is
    /// capped at one second, a longer gap (sleep, hibernate, stall) is credited as offline time; the save file is
    /// written atomically with a backup; the server gets a snapshot every minute while the screen is open and
    /// every five minutes otherwise, never in the middle of gameplay.
    /// </remarks>
    public partial class ClickerEngine : Component
    {
        public const string SAVE_FILE = "osu-clicker.json";
        public const string BACKUP_FILE = "osu-clicker.bak.json";

        private const double save_interval = 30_000;
        private const double submit_interval_open = 60_000;
        private const double submit_interval_background = 300_000;
        private const double first_submit_delay = 10_000;
        private const double frame_gap_offline_s = 5;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        // OsuGame is the play-state source (cached as OsuGame only); absent in plain test scenes
        [Resolved(CanBeNull = true)]
        private OsuGame? osuGame { get; set; }

        public ClickerBalance Balance { get; } = ClickerBalance.Instance;

        public ClickerState State { get; private set; } = new ClickerState();

        public ClickerEconomy Economy { get; private set; } = null!;

        public readonly TapBpmMeter BpmMeter = new TapBpmMeter();

        /// <summary>
        /// Raised after anything structural changed (purchase, rebirth, tree, medal, server rule) so the screen rebuilds its lists.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// A short text to show the player (medal, expedition back, encore, server notice).
        /// </summary>
        public event Action<LocalisableString>? Notice;

        /// <summary>
        /// Raised on every accepted tap with the PP it earned.
        /// </summary>
        public event Action<double>? Tapped;

        /// <summary>
        /// Ranks the server returned with the last accepted submission.
        /// </summary>
        public event Action<ClickerSubmitResponse>? Submitted;

        /// <summary>
        /// Whether the clicker screen is on top; drives the submit cadence and the receipt.
        /// </summary>
        public bool ScreenOpen { get; set; }

        /// <summary>
        /// Set once when a save from an older season was discarded, so the screen can explain the reset.
        /// </summary>
        public bool SeasonResetNoticePending { get; set; }

        /// <summary>
        /// Medals earned while the screen was closed, shown on the next visit.
        /// </summary>
        public readonly List<string> UnseenMedals = new List<string>();

        public int Combo { get; private set; }
        public double LastTapTime { get; private set; } = double.MinValue;
        public TapSource StreamOwner { get; private set; } = TapSource.None;

        /// <summary>
        /// Last response of the server, for the leaderboard panel (ranks, daily streak, weekly rule).
        /// </summary>
        public ClickerSubmitResponse? LastResponse { get; private set; }

        public ClickerModifier? Modifier => Economy.Modifier;

        public long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public double PerSecond => Economy.PerSecond(Now);
        public double ClickValue => Economy.ClickValue(Now);

        private Storage saveStorage = null!;
        private readonly Random random = new Random();
        private IBindable<Language> language = null!;
        private IBindable<APIState> apiState = null!;
        private IBindable<LocalUserPlayingState>? playingState;

        private bool dirty;
        private double lastSave = double.MinValue;
        private double lastSubmit = double.MinValue;
        private double startTime;
        private double lastMedalCheck;
        private double lastSubmittedTotal = -1;
        private SubmitClickerScoreRequest? submitRequest;
        private bool cloudChecked;
        private bool submitPending;
        private int clampNotices;

        public enum TapSource
        {
            None,
            Keyboard,
            Mouse,
        }

#if CLICKER_DEBUG
        /// <summary>
        /// Test builds only: debug tools are compiled in and the engine never submits or restores from the server.
        /// </summary>
        public const bool DEBUG_TOOLS = true;

        public void DebugAddPoints(double amount)
        {
            credit(amount, null);
            changed();
        }

        public void DebugAddLifetime(double amount)
        {
            State.TotalEarned += amount;
            changed();
        }

        public void DebugAddPrestige(int points)
        {
            State.PrestigeClaimed += points;
            changed();
        }

        public void DebugAddRebirth()
        {
            State.Rebirths++;
            changed();
        }

        public void DebugAddClicks(long clicks)
        {
            State.Clicks += clicks;
            changed();
        }

        public void DebugSetBestBpm(double bpm)
        {
            State.BestBpm = bpm;
            BpmMeter.Best = bpm;
            changed();
        }

        /// <summary>
        /// Pretends the given number of hours passed: offline earnings, cooldowns over, expeditions back.
        /// </summary>
        public void DebugTimeSkip(double hours)
        {
            double earned = Economy.OfflineEarnings(hours * 3600);
            credit(earned, ledger => ledger.Offline += earned);
            State.AbilityReadyAt.Clear();
            State.AbilityActiveUntil.Clear();
            foreach (var expedition in State.Expeditions)
                expedition.EndsAt = Math.Min(expedition.EndsAt, Now);
            returnExpeditions(Now);
            changed();
        }

        public void DebugEncore() => OnScoreSubmitted(new ScoreInfo { Passed = true });

        public void DebugReset() => ResetForTests();
#else
        public const bool DEBUG_TOOLS = false;
#endif

        [BackgroundDependencyLoader]
        private void load()
        {
            saveStorage = storage.GetStorageForDirectory("banchosucks");
            BpmMeter.MaxBpm = Balance.MaxBpm;
            loadState();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            language = game.CurrentLanguage.GetBoundCopy();
            language.BindValueChanged(l => ClickerStrings.IsGerman = l.NewValue == Language.de, true);

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(s =>
            {
                if (s.NewValue == APIState.Online)
                    Schedule(restoreFromCloud);
            }, true);

            playingState = (osuGame as ILocalUserPlayInfo)?.PlayingState.GetBoundCopy();
            playingState?.BindValueChanged(s =>
            {
                // deferred file and network work runs as soon as gameplay is over
                if (s.NewValue == LocalUserPlayingState.NotPlaying && submitPending)
                    SubmitNow();
            });

            startTime = Time.Current;
            lastMedalCheck = Time.Current;
            State.Ledger.Since = State.Ledger.Since == 0 ? Now : State.Ledger.Since;
        }

        private bool playing => playingState?.Value != null && playingState.Value != LocalUserPlayingState.NotPlaying;

        protected override void Update()
        {
            base.Update();

            long now = Now;
            double seconds = Time.Elapsed / 1000;

            if (seconds > frame_gap_offline_s)
            {
                // the game was asleep or stalled: this is offline time, not production at full rate
                double earned = Economy.OfflineEarnings(seconds);
                credit(earned, ledger => ledger.Offline += earned);
            }
            else if (seconds > 0)
            {
                double earned = Economy.PerSecond(now) * seconds;
                if (!ScreenOpen)
                    credit(earned, ledger =>
                    {
                        if (playing)
                            ledger.Maps += earned;
                        else
                            ledger.Client += earned;
                    });
                else
                    credit(earned, null);
            }

            // combo breaks after a second without a tap
            if (Combo > 0 && Time.Current - LastTapTime > Balance.ComboTimeoutMs)
            {
                Combo = 0;
                ComboBroken?.Invoke();
            }

            // a BPM record is stored once the stream is over
            if (BpmMeter.TapsPerSecond(Time.Current) == 0 && Math.Round(BpmMeter.Best) > State.BestBpm)
            {
                State.BestBpm = Math.Round(BpmMeter.Best);
                dirty = true;
                Notice?.Invoke(ClickerStrings.Text("New BPM record: {0} BPM!", "Neuer BPM-Rekord: {0} BPM!", State.BestBpm));
                checkMedals();
            }

            if (Time.Current - lastMedalCheck > 1000)
            {
                lastMedalCheck = Time.Current;
                returnExpeditions(now);
                checkMedals();
            }

            if (dirty && Time.Current - lastSave > save_interval && !playing)
                Save();

            double interval = ScreenOpen ? submit_interval_open : submit_interval_background;
            if (Time.Current - startTime > first_submit_delay && Time.Current - lastSubmit > interval)
                SubmitNow();
        }

        public event Action? ComboBroken;

        private void credit(double earned, Action<ClickerLedger>? ledger)
        {
            if (earned <= 0)
                return;

            State.Points += earned;
            State.RunEarned += earned;
            State.TotalEarned += earned;
            ledger?.Invoke(State.Ledger);
            dirty = true;
        }

        // ------------------------------------------------------------------ tapping

        /// <summary>
        /// Counts a tap. Returns false when it was ignored: the other input kind owns the current stream, or the
        /// stream would exceed the BPM cap.
        /// </summary>
        public bool Tap(TapSource source)
        {
            if (StreamOwner != TapSource.None && source != StreamOwner && Time.Current - LastTapTime <= TapBpmMeter.IDLE_MS)
                return false;

            if (!BpmMeter.Tap(Time.Current))
                return false;

            StreamOwner = source;
            LastTapTime = Time.Current;

            double value = ClickValue;
            State.Clicks++;
            Combo++;
            State.BestCombo = Math.Max(State.BestCombo, Combo);
            credit(value, null);
            dirty = true;
            Tapped?.Invoke(value);
            return true;
        }

        // ------------------------------------------------------------------ purchases

        public bool BuyProducer(ClickerProducer producer, int amount = 1)
        {
            if (!Economy.ProducerUnlocked(producer) || amount < 1)
                return false;

            double price = Economy.BulkCost(producer, amount);
            if (State.Points < price)
                return false;

            State.Points -= price;
            State.Producers[producer.Id] = Economy.Count(producer.Id) + amount;
            changed();
            return true;
        }

        public bool BuyStar(ClickerProducer producer)
        {
            if (!Economy.StarAvailable(producer))
                return false;

            int index = Economy.NextStarIndex(producer.Id);
            double price = Economy.StarCost(producer, index);
            if (State.Points < price)
                return false;

            State.Points -= price;
            State.Stars[producer.Id] = index + 1;
            changed();
            return true;
        }

        public bool BuyUpgrade(ClickerUpgrade upgrade)
        {
            if (State.Upgrades.Contains(upgrade.Id) || State.Points < upgrade.Cost)
                return false;

            State.Points -= upgrade.Cost;
            State.Upgrades.Add(upgrade.Id);
            changed();
            return true;
        }

        public bool BuySynergy(ClickerSynergy synergy)
        {
            double price = Economy.SynergyCost(synergy);
            if (State.Synergies.Contains(synergy.Id) || !Economy.SynergyAvailable(synergy) || State.Points < price)
                return false;

            State.Points -= price;
            State.Synergies.Add(synergy.Id);
            changed();
            return true;
        }

        public bool UnlockAbility(ClickerAbility ability)
        {
            if (State.Abilities.Contains(ability.Id) || State.Points < ability.UnlockCost)
                return false;

            State.Points -= ability.UnlockCost;
            State.Abilities.Add(ability.Id);
            changed();
            return true;
        }

        public bool ActivateAbility(ClickerAbility ability)
        {
            long now = Now;
            if (!Economy.AbilityReady(ability.Id, now))
                return false;

            State.AbilityActiveUntil[ability.Id] = now + (long)ability.DurationSeconds;
            State.AbilityReadyAt[ability.Id] = now + (long)ability.CooldownSeconds;
            dirty = true;
            Changed?.Invoke();
            return true;
        }

        public bool SetStance(ClickerStance stance)
        {
            if (!Economy.StanceUnlocked && stance.Id != "none")
                return false;

            State.Stance = stance.Id;
            changed();
            return true;
        }

        /// <summary>
        /// Whether anything in the shop is affordable right now (the minigames tile pulses then).
        /// </summary>
        public bool AnythingAffordable()
        {
            double points = State.Points;
            return Balance.Producers.Any(p => Economy.ProducerUnlocked(p) && (points >= Economy.ProducerCost(p) || (Economy.StarAvailable(p) && points >= Economy.StarCost(p, Economy.NextStarIndex(p.Id)))))
                   || Balance.Upgrades.Any(u => !State.Upgrades.Contains(u.Id) && points >= u.Cost)
                   || Balance.Synergies.Any(s => !State.Synergies.Contains(s.Id) && Economy.SynergyAvailable(s) && points >= Economy.SynergyCost(s))
                   || Balance.Abilities.Any(a => !State.Abilities.Contains(a.Id) && points >= a.UnlockCost);
        }

        // ------------------------------------------------------------------ prestige

        /// <summary>
        /// Starts a new run: keeps lifetime PP, records, prestige, tree and medals; resets the run.
        /// </summary>
        public bool Rebirth()
        {
            int gained = Economy.PrestigePending;
            if (gained < 1)
                return false;

            var economyBefore = Economy;

            State.PrestigeClaimed += gained;
            State.Rebirths++;
            State.Points = 0;
            State.RunEarned = 0;
            State.Producers = new Dictionary<string, int>(economyBefore.HeadStart);
            State.Stars = new Dictionary<string, int>();
            State.Upgrades = State.Upgrades.Where(economyBefore.KeptUpgrades.Contains).ToHashSet();
            State.Synergies = new HashSet<string>();
            State.Stance = "none";
            State.Abilities = economyBefore.KeepAbilities ? Balance.Abilities.Select(a => a.Id).ToHashSet() : new HashSet<string>();
            State.AbilityActiveUntil.Clear();
            State.AbilityReadyAt.Clear();
            State.WarmedUpUntil = 0;
            State.Expeditions.Clear();
            Combo = 0;

            changed();
            Notice?.Invoke(ClickerStrings.Text("Rebirth! +{0} prestige points, {1} in total.", "Rebirth! +{0} Prestige-Punkte, insgesamt {1}.", gained, Economy.PrestigeEffective));
            return true;
        }

        public bool BuyNode(ClickerTreeNode node)
        {
            if (!Economy.CanBuyNode(node))
                return false;

            State.Tree.Add(node.Id);

            if (Economy.KeepAbilities || node.Effect == "keep_abilities")
            {
                foreach (var ability in Balance.Abilities)
                    State.Abilities.Add(ability.Id);
            }

            changed();
            return true;
        }

        /// <summary>
        /// Buys a prestige building with prestige points; they stay through every rebirth.
        /// </summary>
        public bool BuyPrestigeProducer(ClickerPrestigeProducer producer)
        {
            if (!Economy.PrestigeProducersUnlocked)
                return false;

            int price = Economy.PrestigeProducerCost(producer);
            if (Economy.PrestigeAvailable < price)
                return false;

            State.PrestigeProducers[producer.Id] = Economy.PrestigeProducerCount(producer.Id) + 1;
            changed();
            return true;
        }

        /// <summary>
        /// Clears the tree for a tenth of the prestige points; the rest is refunded.
        /// </summary>
        public bool Respec()
        {
            if (State.Tree.Count == 0 || Economy.PrestigeEffective < 1)
                return false;

            State.PrestigeBurned += Economy.RespecCost;
            State.Tree.Clear();
            changed();
            return true;
        }

        // ------------------------------------------------------------------ events (screen only)

        public void GrantSpinner(bool kiai)
        {
            double reward = Economy.SpinnerReward(Now, kiai);
            State.SpinnersDone++;
            credit(reward, null);
            SpinnerRewarded?.Invoke(reward, kiai);
        }

        public event Action<double, bool>? SpinnerRewarded;

        public double GrantSlider(double accuracy)
        {
            double reward = Economy.SliderReward(Now, accuracy);
            State.SlidersDone++;
            State.SliderBest = Math.Max(State.SliderBest, accuracy);
            credit(reward, null);
            checkMedals();
            return reward;
        }

        public bool StartExpedition(ClickerProducer producer, int durationIndex)
        {
            if (durationIndex < 0 || durationIndex >= Balance.Expedition.DurationsSeconds.Length)
                return false;
            if (State.Expeditions.Count >= Economy.ExpeditionSlots)
                return false;
            if (State.Expeditions.Any(e => e.Producer == producer.Id))
                return false;

            int count = Economy.Count(producer.Id) / 2;
            if (count < 1)
                return false;

            long now = Now;
            State.Expeditions.Add(new ClickerExpedition
            {
                Producer = producer.Id,
                Count = count,
                StartedAt = now,
                EndsAt = now + (long)Balance.Expedition.DurationsSeconds[durationIndex],
                Rate = Economy.SingleRate(producer.Id),
            });
            changed();
            return true;
        }

        private void returnExpeditions(long now)
        {
            var back = State.Expeditions.Where(e => e.EndsAt <= now).ToList();
            if (back.Count == 0)
                return;

            foreach (var expedition in back)
            {
                State.Expeditions.Remove(expedition);
                bool success = random.NextDouble() < Economy.ExpeditionChance;
                string name = ClickerStrings.Pick(Balance.Producer(expedition.Producer)?.Name ?? new LocalisedName());

                if (success)
                {
                    double reward = Economy.ExpeditionReward(expedition);
                    State.ExpeditionsDone++;
                    credit(reward, ledger => ledger.Expeditions += reward);
                    Notice?.Invoke(ClickerStrings.Text("Expedition back: {0} {1} won +{2} PP!", "Expedition zurück: {0} {1} haben +{2} PP gewonnen!", expedition.Count, name, ClickerFormat.Number(reward)));
                }
                else
                    Notice?.Invoke(ClickerStrings.Text("Expedition back: {0} {1} lost in the first round. Nothing won.", "Expedition zurück: {0} {1} sind in Runde eins ausgeschieden. Nichts gewonnen.", expedition.Count, name));
            }

            changed();
        }

        /// <summary>
        /// Encore: a passed map that was submitted online pays production time and clicks, and warms the player up.
        /// </summary>
        public void OnScoreSubmitted(ScoreInfo score)
        {
            if (!score.Passed)
                return;

            long now = Now;
            if (now - State.EncoreHourStart >= 3600)
            {
                State.EncoreHourStart = now;
                State.EncoreHourCount = 0;
            }

            if (State.EncoreHourCount >= Balance.Encore.MaxPerHour)
                return;

            State.EncoreHourCount++;
            State.EncoreMaps++;
            State.WarmedUpUntil = now + (long)Balance.Encore.WarmupSeconds;
            double reward = Economy.EncoreReward(now);
            credit(reward, ledger => ledger.Encore += reward);
            checkMedals();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ medals

        /// <summary>
        /// Awards every medal the state qualifies for. Returns whether something was awarded (the economy is then already recalculated and <see cref="Changed"/> raised).
        /// </summary>
        private bool checkMedals()
        {
            var earned = Economy.NewMedals().ToList();
            if (earned.Count == 0)
                return false;

            foreach (var medal in earned)
            {
                State.Medals.Add(medal.Id);
                if (ScreenOpen)
                    Notice?.Invoke(ClickerStrings.Text("Medal earned: {0} (+{1} production)", "Medaille verdient: {0} (+{1} Produktion)", ClickerStrings.Pick(medal.Name), ClickerFormat.Percent(Balance.MedalRate)));
                else
                    UnseenMedals.Add(medal.Id);
            }

            // a medal can qualify for the next one (never in practice, but cheap to be safe)
            Economy.Recalculate();
            dirty = true;
            if (!checkMedals())
                Changed?.Invoke();
            return true;
        }

        private void changed()
        {
            Economy.Recalculate();
            dirty = true;

            if (!checkMedals())
                Changed?.Invoke();
        }

        /// <summary>
        /// Hands over what was earned while the screen was closed and starts a new receipt.
        /// </summary>
        public ClickerLedger TakeReceipt()
        {
            var receipt = State.Ledger;
            State.Ledger = new ClickerLedger { Since = Now };
            dirty = true;
            return receipt;
        }

        // ------------------------------------------------------------------ server

        public ClickerSubmission CreateSubmission() => new ClickerSubmission
        {
            Season = State.Season,
            BalanceVersion = State.BalanceVersion,
            BpmEpoch = State.BpmEpoch,
            TotalEarned = Math.Floor(State.TotalEarned),
            RunEarned = Math.Floor(State.RunEarned),
            Points = Math.Floor(State.Points),
            Clicks = State.Clicks,
            BestBpm = Math.Max(State.BestBpm, Math.Round(BpmMeter.Best, 1)),
            BestCombo = State.BestCombo,
            Producers = State.Producers.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value),
            Stars = State.Stars.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value),
            Upgrades = State.Upgrades.ToList(),
            Synergies = State.Synergies.ToList(),
            Stance = State.Stance,
            Abilities = State.Abilities.ToList(),
            Rebirths = State.Rebirths,
            PrestigeClaimed = State.PrestigeClaimed,
            PrestigeBurned = State.PrestigeBurned,
            Tree = State.Tree.ToList(),
            PrestigeProducers = State.PrestigeProducers.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value),
            Medals = State.Medals.ToList(),
            EncoreMaps = State.EncoreMaps,
            DailyDays = State.DailyDays,
            ExpeditionsDone = State.ExpeditionsDone,
            SlidersDone = State.SlidersDone,
            SpinnersDone = State.SpinnersDone,
            Expeditions = State.Expeditions.Select(e => new ClickerSubmissionExpedition { Producer = e.Producer, Count = e.Count, StartedAt = e.StartedAt, EndsAt = e.EndsAt }).ToList(),
            ModifierId = Modifier?.Id,
            Save = new JRaw(JsonSerializer.Serialize(State)),
        };

        /// <summary>
        /// Sends the progress now if the player is online, something changed and no map is running.
        /// </summary>
        public void SubmitNow()
        {
            lastSubmit = Time.Current;

            if (DEBUG_TOOLS)
                return;

            if (api.State.Value != APIState.Online || State.TotalEarned < 1 || Math.Floor(State.TotalEarned) == lastSubmittedTotal)
                return;

            if (playing)
            {
                submitPending = true;
                return;
            }

            submitPending = false;
            submitRequest?.Cancel();
            var request = submitRequest = new SubmitClickerScoreRequest(CreateSubmission());
            lastSubmittedTotal = request.Submission.TotalEarned;

            request.Success += response =>
            {
                if (IsDisposed)
                    return;

                ApplyServerResponse(response);
            };
            request.Failure += e =>
            {
                Logger.Log($"osu! Clicker submission failed: {e.Message}", LoggingTarget.Network);
                lastSubmittedTotal = -1;
            };
            api.Queue(request);
        }

        public void ApplyServerResponse(ClickerSubmitResponse response)
        {
            LastResponse = response;

            if (!response.Accepted)
            {
                Logger.Log($"osu! Clicker progress not accepted: {response.Reason}", LoggingTarget.Network);

                // the 15 s server cooldown swallowed this snapshot; the next tick sends it again
                if (response.Reason == "cooldown")
                    lastSubmittedTotal = -1;
                if (response.Reason == "old_season")
                    Notice?.Invoke(ClickerStrings.Text("The server plays a newer season. Please update the client.", "Der Server spielt eine neuere Saison. Bitte den Client aktualisieren."));
                return;
            }

            if (response.Clamped && clampNotices++ < 3)
                Logger.Log($"osu! Clicker: the server capped the reported growth (server total {response.TotalEarned})", LoggingTarget.Network);

            // the weekly rule comes from the server; apply it when it changes
            string? oldModifier = Economy.Modifier?.Id;
            if (response.Modifier?.Id != oldModifier)
            {
                Economy.Modifier = response.Modifier;
                changed();
                if (response.Modifier != null && ScreenOpen)
                    Notice?.Invoke(ClickerStrings.Text("This week: {0}", "Diese Woche: {0}", ClickerStrings.Pick(response.Modifier.Name)));
            }

            // daily challenge bonus, once per day the player took part in lazer's daily challenge
            if (response.Daily != null)
            {
                State.DailyStreak = response.Daily.Streak;

                if (response.Daily.Played && response.Daily.Day != State.LastDailyDay)
                {
                    State.LastDailyDay = response.Daily.Day;
                    State.DailyDays++;
                    double reward = Economy.DailyReward(Now);
                    credit(reward, ledger => ledger.Daily += reward);
                    Notice?.Invoke(ClickerStrings.Text("Daily challenge played: +{0} PP bonus! Streak {1}.", "Daily Challenge gespielt: +{0} PP Bonus! Streak {1}.", ClickerFormat.Number(reward), response.Daily.Streak));
                    checkMedals();
                }
            }

            Submitted?.Invoke(response);
        }

        private void restoreFromCloud()
        {
            if (cloudChecked || IsDisposed || DEBUG_TOOLS)
                return;

            cloudChecked = true;

            // only a fresh save asks the server; a local save is always the newer truth
            if (State.TotalEarned > 0 || State.Clicks > 0)
                return;

            var request = new GetClickerSaveRequest();
            request.Success += response =>
            {
                if (IsDisposed || response.Save == null || response.Season != Balance.Season)
                    return;

                if (State.TotalEarned > 0 || State.Clicks > 0)
                    return;

                try
                {
                    var restored = JsonSerializer.Deserialize<ClickerState>(response.Save.ToString(Newtonsoft.Json.Formatting.None));
                    if (restored == null || restored.Season != Balance.Season || restored.TotalEarned <= 0)
                        return;

                    adopt(restored);
                    Notice?.Invoke(ClickerStrings.Text("Cloud save restored: {0} PP lifetime.", "Cloud-Spielstand wiederhergestellt: {0} PP insgesamt.", ClickerFormat.Number(restored.TotalEarned)));
                    Logger.Log("osu! Clicker: cloud save restored", LoggingTarget.Network);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "osu! Clicker cloud save could not be read");
                }
            };
            api.Queue(request);
        }

        /// <summary>
        /// Starts from an empty state (tests only).
        /// </summary>
        public void ResetForTests()
        {
            Combo = 0;
            StreamOwner = TapSource.None;
            LastTapTime = double.MinValue;
            BpmMeter.Best = 0;
            lastSubmittedTotal = -1;
            UnseenMedals.Clear();
            adopt(new ClickerState { CreatedAt = Now });
        }

        // ------------------------------------------------------------------ saving

        private void loadState()
        {
            string path = saveStorage.GetFullPath(SAVE_FILE);
            string backup = saveStorage.GetFullPath(BACKUP_FILE);

            ClickerState? loaded = read(path) ?? read(backup);

            if (loaded == null)
            {
                adopt(new ClickerState { CreatedAt = Now });
                return;
            }

            if (loaded.Season < Balance.Season)
            {
                // the one-time reset: old saves start over, the lifetime record is gone on purpose
                Logger.Log($"osu! Clicker: discarding save of season {loaded.Season}, playing season {Balance.Season}");
                SeasonResetNoticePending = true;
                adopt(new ClickerState { CreatedAt = Now });
                return;
            }

            adopt(loaded);

            // time since the last save counts as offline production
            double away = Now - loaded.SavedAt;
            if (away >= 60 && Economy.BasePerSecond > 0)
            {
                double earned = Economy.OfflineEarnings(away);
                credit(earned, ledger => ledger.Offline += earned);
            }

            returnExpeditions(Now);
        }

        private void adopt(ClickerState loaded)
        {
            State = loaded;
            State.Season = Balance.Season;
            State.BalanceVersion = Balance.BalanceVersion;

            if (State.BpmEpoch < Balance.BpmEpoch)
            {
                State.BestBpm = 0;
                State.BpmEpoch = Balance.BpmEpoch;
            }

            if (State.Stance != "none" && Balance.Stance(State.Stance) == null)
                State.Stance = "none";

            Economy = new ClickerEconomy(Balance, State, Economy?.Modifier);
            BpmMeter.Best = State.BestBpm;
            dirty = true;
            Changed?.Invoke();
        }

        private ClickerState? read(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                return JsonSerializer.Deserialize<ClickerState>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"osu! Clicker save {Path.GetFileName(path)} could not be read");
                return null;
            }
        }

        /// <summary>
        /// Writes the save atomically: new file next to the old one, then swap, keeping the previous save as backup.
        /// </summary>
        public void Save()
        {
            dirty = false;

            try
            {
                lastSave = Time.Current;
                State.SavedAt = Now;
                State.BestBpm = Math.Max(State.BestBpm, Math.Round(BpmMeter.Best));

                string path = saveStorage.GetFullPath(SAVE_FILE, true);
                string backup = saveStorage.GetFullPath(BACKUP_FILE, true);
                string temp = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(temp, JsonSerializer.Serialize(State));

                if (File.Exists(path))
                    File.Replace(temp, path, backup);
                else
                    File.Move(temp, path);
            }
            catch (Exception e)
            {
                Logger.Error(e, "osu! Clicker could not be saved");
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (dirty && !IsDisposed)
                Save();

            submitRequest?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}

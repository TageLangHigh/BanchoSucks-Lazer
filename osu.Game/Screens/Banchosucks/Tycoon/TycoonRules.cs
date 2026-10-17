// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    /// <summary>
    /// The save of the Private Server Tycoon; the JSON names are what the plugin <c>banchosucks_tycoon</c> stores and checks.
    /// </summary>
    public class TycoonState
    {
        [JsonProperty("v")] public int Version { get; set; } = TycoonRules.STATE_VERSION;

        /// <summary>Donations in hand.</summary>
        [JsonProperty("donations")] public double Donations { get; set; }

        /// <summary>Donations earned in this season.</summary>
        [JsonProperty("run_total")] public double RunTotal { get; set; }

        /// <summary>Donations earned over all seasons; the main leaderboard.</summary>
        [JsonProperty("lifetime_total")] public double LifetimeTotal { get; set; }

        [JsonProperty("players")] public double Players { get; set; }
        [JsonProperty("max_players")] public double MaxPlayers { get; set; }

        /// <summary>Reputation 0..100; low when the server lags.</summary>
        [JsonProperty("ruf")] public double Ruf { get; set; } = 100;

        /// <summary>Index into <see cref="TycoonRules.Hardware"/>.</summary>
        [JsonProperty("hardware")] public int Hardware { get; set; }

        [JsonProperty("marketing")] public Dictionary<string, int> Marketing { get; set; } = TycoonRules.Marketing.ToDictionary(m => m.Key, _ => 0);
        [JsonProperty("features")] public List<string> Features { get; set; } = new List<string>();
        [JsonProperty("clicks")] public long Clicks { get; set; }
        [JsonProperty("prestige")] public int Prestige { get; set; }
        [JsonProperty("legacy")] public int Legacy { get; set; }
        [JsonProperty("events")] public int Events { get; set; }
        [JsonProperty("play_seconds")] public double PlaySeconds { get; set; }

        public int MarketingCount(string key) => Marketing.TryGetValue(key, out int count) ? count : 0;

        public bool Has(string feature) => Features.Contains(feature);
    }

    public readonly record struct TycoonHardware(string Key, int Capacity, long Cost, string NameEn, string NameDe, string DescriptionEn, string DescriptionDe);

    public readonly record struct TycoonMarketing(string Key, double Rate, long BaseCost, string NameEn, string NameDe, string DescriptionEn, string DescriptionDe);

    public readonly record struct TycoonFeature(string Key, long Cost, double DonationMult, double InflowMult, double RegenMult, string NameEn, string NameDe, string DescriptionEn, string DescriptionDe);

    /// <summary>
    /// Banchosucks: the economy of the Private Server Tycoon, mirrored by the plugin's <c>economy.py</c> (same numbers in both test suites).
    /// Marketing attracts players, players donate, donations buy hardware (capacity), more marketing and features. Too many
    /// players for the hardware means lag: reputation falls and players leave. A season restart trades the run for legacy points.
    /// </summary>
    public static class TycoonRules
    {
        public const int STATE_VERSION = 1;
        public const int MAX_COUNT = 2000;
        public const double OFFLINE_CAP = 8 * 3600;
        public const double CHURN = 0.004;
        public const double OVERLOAD_CHURN = 0.05;
        public const double OVERLOAD_TOLERANCE = 1.05;
        public const double RUF_REGEN = 0.3;
        public const double RUF_LOSS = 3.0;
        public const double RUF_LOSS_CAP = 10.0;
        public const double DONATION_PER_PLAYER = 0.05;
        public const double LEGACY_BONUS = 0.05;
        public const double PRESTIGE_MIN_TOTAL = 1_000_000;
        public const int MAX_CLICKS_PER_SECOND = 15;
        public const double CLICK_BASE = 1.0;
        public const double CLICK_PER_BUILDING = 0.05;
        public const double COST_GROWTH = 1.15;
        public const double EVENT_BOOST = 2.0;

        public static readonly TycoonHardware[] Hardware =
        {
            new TycoonHardware("pi", 10, 0, "Raspberry Pi", "Raspberry Pi", "Runs on the windowsill. Ten players, then it gets warm.", "Läuft auf der Fensterbank. Zehn Spieler, dann wird er warm."),
            new TycoonHardware("laptop", 40, 60, "Old laptop", "Alter Laptop", "The lid stays open so it does not overheat.", "Der Deckel bleibt offen, damit er nicht überhitzt."),
            new TycoonHardware("minipc", 120, 450, "Mini PC", "Mini-PC", "Quiet, small, finally an SSD.", "Leise, klein, endlich eine SSD."),
            new TycoonHardware("vps", 400, 3_000, "VPS", "VPS", "Rented, somewhere in Frankfurt. The first real server.", "Gemietet, irgendwo in Frankfurt. Der erste richtige Server."),
            new TycoonHardware("dedicated", 1_500, 20_000, "Dedicated server", "Dedicated Server", "All cores are yours. Deploys no longer take the site down.", "Alle Kerne gehören dir. Deploys legen die Seite nicht mehr lahm."),
            new TycoonHardware("second", 5_000, 140_000, "Second server", "Zweiter Server", "Database here, bancho there. Lazer gets its own container.", "Datenbank hier, Bancho dort. Lazer bekommt seinen eigenen Container."),
            new TycoonHardware("cluster", 15_000, 1_000_000, "Server cluster", "Server-Cluster", "Load balancing, replicas, a wiki nobody reads.", "Load-Balancing, Replikate, ein Wiki, das keiner liest."),
            new TycoonHardware("datacenter", 50_000, 7_500_000, "Data centre", "Rechenzentrum", "Your own rack row and a badge for the door.", "Eine eigene Rack-Reihe und ein Ausweis für die Tür."),
            new TycoonHardware("cloud", 200_000, 60_000_000, "Cloud with autoscaling", "Cloud mit Autoscaling", "It scales. The bill scales too.", "Es skaliert. Die Rechnung auch."),
        };

        public static readonly TycoonMarketing[] Marketing =
        {
            new TycoonMarketing("discord", 0.1, 15, "Discord server", "Discord-Server", "Someone has to answer the questions.", "Irgendwer muss die Fragen beantworten."),
            new TycoonMarketing("reddit", 0.5, 100, "Reddit posts", "Reddit-Posts", "\"Private server with lazer support, no, really.\"", "„Privatserver mit Lazer-Support, nein, wirklich.“"),
            new TycoonMarketing("youtube", 3.0, 1_100, "YouTube videos", "YouTube-Videos", "Top plays, server tours, a tutorial with bad audio.", "Top-Plays, Server-Touren, ein Tutorial mit schlechtem Ton."),
            new TycoonMarketing("streamer", 15.0, 12_000, "Streamer sponsoring", "Streamer-Sponsoring", "One evening on stream brings a wave.", "Ein Abend im Stream bringt eine Welle."),
            new TycoonMarketing("tournament", 60.0, 130_000, "Tournament", "Turnier", "Prize pool, brackets, a referee who falls asleep.", "Preisgeld, Brackets, ein Schiedsrichter, der einschläft."),
            new TycoonMarketing("campaign", 250.0, 1_400_000, "Ad campaign", "Werbekampagne", "Banners everywhere. Even on the toilet door.", "Banner überall. Sogar an der Klotür."),
            new TycoonMarketing("influencer", 1_000.0, 20_000_000, "Influencer deal", "Influencer-Deal", "A face everyone knows plays on your server.", "Ein Gesicht, das jeder kennt, spielt auf deinem Server."),
        };

        public static readonly TycoonFeature[] Features =
        {
            new TycoonFeature("website", 500, 1.20, 1.0, 1.0, "Website", "Website", "Profiles, scores, a changelog. Donations +20 %.", "Profile, Scores, ein Changelog. Spenden +20 %."),
            new TycoonFeature("leaderboards", 2_000, 1.0, 1.25, 1.0, "Leaderboards", "Ranglisten", "Everyone wants to be number one. Player inflow +25 %.", "Jeder will die Nummer eins sein. Zulauf +25 %."),
            new TycoonFeature("medals", 8_000, 1.15, 1.0, 1.0, "Medals", "Medaillen", "Shiny things to collect. Donations +15 %.", "Glänzende Dinge zum Sammeln. Spenden +15 %."),
            new TycoonFeature("discord_bot", 15_000, 1.0, 1.0, 2.0, "Discord bot", "Discord-Bot", "Answers at 3 am. Reputation recovers twice as fast.", "Antwortet um drei Uhr nachts. Ruf erholt sich doppelt so schnell."),
            new TycoonFeature("anticheat", 40_000, 1.0, 1.0, 1.0, "Anticheat", "Anticheat", "Cheater waves become bans and reputation instead of damage.", "Cheater-Wellen werden zu Sperren und Ruf statt zu Schaden."),
            new TycoonFeature("backups", 60_000, 1.0, 1.0, 1.0, "Backups", "Backups", "Every night at 04:15 UTC. Database crashes cost nothing.", "Jede Nacht um 04:15 UTC. Datenbank-Crashs kosten nichts mehr."),
            new TycoonFeature("cloudflare", 120_000, 1.0, 1.0, 1.0, "Cloudflare", "Cloudflare", "DDoS attacks bounce off.", "DDoS-Angriffe prallen ab."),
            new TycoonFeature("lazer", 250_000, 1.0, 1.50, 1.0, "Lazer support", "Lazer-Support", "The new client, the new players. Inflow +50 %.", "Der neue Client, die neuen Spieler. Zulauf +50 %."),
            new TycoonFeature("multiplayer", 800_000, 1.0, 1.30, 1.0, "Multiplayer", "Multiplayer", "Rooms, playlists, arguments about mods. Inflow +30 %.", "Räume, Playlists, Streit um Mods. Zulauf +30 %."),
            new TycoonFeature("supporter", 2_500_000, 1.40, 1.0, 1.0, "Supporter tags", "Supporter-Tags", "A heart next to the name. Donations +40 %.", "Ein Herz neben dem Namen. Spenden +40 %."),
            new TycoonFeature("mirror", 8_000_000, 1.20, 1.10, 1.0, "Beatmap mirror", "Beatmap-Mirror", "Downloads in seconds. Donations +20 %, inflow +10 %.", "Downloads in Sekunden. Spenden +20 %, Zulauf +10 %."),
            new TycoonFeature("tests", 25_000_000, 1.0, 1.0, 1.0, "Unit tests and smoke checks", "Unit-Tests und Smoke-Checks", "Broken deploys and crashing medals are caught before they go live.", "Kaputte Deploys und abstürzende Medaillen werden gefangen, bevor sie live gehen."),
        };

        public static TycoonHardware HardwareAt(int level) => Hardware[Math.Clamp(level, 0, Hardware.Length - 1)];

        public static int Capacity(int level) => HardwareAt(level).Capacity;

        /// <summary>Price of upgrading to <paramref name="level"/>; 0 for the starting hardware and beyond the ladder.</summary>
        public static long HardwareCost(int level) => level >= 0 && level < Hardware.Length ? Hardware[level].Cost : 0;

        public static TycoonMarketing MarketingOf(string key) => Marketing.First(m => m.Key == key);

        public static long MarketingCost(string key, int owned) => (long)Math.Floor(MarketingOf(key).BaseCost * Math.Pow(COST_GROWTH, owned));

        public static long MarketingTotalCost(string key, int owned)
        {
            long total = 0;
            for (int i = 0; i < owned; i++) total += MarketingCost(key, i);
            return total;
        }

        public static TycoonFeature? FeatureOf(string key) => Features.Any(f => f.Key == key) ? Features.First(f => f.Key == key) : null;

        public static long FeatureCost(string key) => FeatureOf(key)?.Cost ?? 0;

        public static double DonationMult(IEnumerable<string> features) => features.Select(FeatureOf).Where(f => f != null).Aggregate(1.0, (v, f) => v * f!.Value.DonationMult);

        public static double InflowMult(IEnumerable<string> features) => features.Select(FeatureOf).Where(f => f != null).Aggregate(1.0, (v, f) => v * f!.Value.InflowMult);

        public static double RegenMult(IEnumerable<string> features) => features.Select(FeatureOf).Where(f => f != null).Aggregate(1.0, (v, f) => v * f!.Value.RegenMult);

        public static double LegacyMult(int legacy) => 1.0 + LEGACY_BONUS * Math.Max(0, legacy);

        public static double MarketingRate(TycoonState state) => Marketing.Sum(m => state.MarketingCount(m.Key) * m.Rate);

        public static double ClickValue(TycoonState state) => CLICK_BASE + CLICK_PER_BUILDING * Marketing.Sum(m => state.MarketingCount(m.Key));

        public static double RufFactor(double ruf) => 0.5 + Math.Clamp(ruf, 0, 100) / 100.0;

        /// <summary>Everything the season has paid for; may never exceed what the season earned.</summary>
        public static double Spent(TycoonState state)
        {
            double total = 0;
            for (int level = 1; level <= state.Hardware; level++) total += HardwareCost(level);
            foreach (var m in Marketing) total += MarketingTotalCost(m.Key, state.MarketingCount(m.Key));
            foreach (string key in state.Features) total += FeatureCost(key);
            return total;
        }

        public static double Inflow(TycoonState state, double boost = 1.0) =>
            MarketingRate(state) * InflowMult(state.Features) * LegacyMult(state.Legacy) * RufFactor(state.Ruf) * boost;

        public static double DonationRate(TycoonState state, double boost = 1.0) =>
            state.Players * DONATION_PER_PLAYER * RufFactor(state.Ruf) * DonationMult(state.Features) * LegacyMult(state.Legacy) * boost;

        /// <summary>Advances the simulation by <paramref name="dt"/> seconds (at most one second per call); returns the donations gained.</summary>
        public static double Step(TycoonState state, double dt, double inflowBoost = 1.0, double donationBoost = 1.0)
        {
            int cap = Capacity(state.Hardware);
            double players = state.Players;
            double gained = DonationRate(state, donationBoost) * dt;

            players += Inflow(state, inflowBoost) * dt;
            players -= players * CHURN * dt;
            if (players > cap)
                players -= (players - cap) * OVERLOAD_CHURN * dt;

            if (players > cap * OVERLOAD_TOLERANCE)
                state.Ruf = Math.Max(0.0, state.Ruf - Math.Min(RUF_LOSS_CAP, RUF_LOSS * (players / cap - 1.0)) * dt);
            else
                state.Ruf = Math.Min(100.0, state.Ruf + RUF_REGEN * RegenMult(state.Features) * dt);

            state.Players = Math.Max(0.0, players);
            state.MaxPlayers = Math.Max(state.MaxPlayers, state.Players);
            state.Donations += gained;
            state.RunTotal += gained;
            state.LifetimeTotal += gained;
            state.PlaySeconds += dt;
            return gained;
        }

        /// <summary>Runs <see cref="Step"/> for <paramref name="seconds"/>: offline progress (no events, no clicks).</summary>
        public static double Simulate(TycoonState state, double seconds, double dt = 1.0)
        {
            double gained = 0;
            double remaining = Math.Max(0, seconds);

            while (remaining > 0)
            {
                double chunk = Math.Min(dt, remaining);
                gained += Step(state, chunk);
                remaining -= chunk;
            }

            return gained;
        }

        /// <summary>A post in the Discord: players join at once.</summary>
        public static double Click(TycoonState state)
        {
            double added = ClickValue(state);
            state.Players += added;
            state.MaxPlayers = Math.Max(state.MaxPlayers, state.Players);
            state.Clicks++;
            return added;
        }

        public static bool CanAfford(TycoonState state, double cost) => state.Donations + 1e-9 >= cost;

        public static bool BuyHardware(TycoonState state)
        {
            int level = state.Hardware + 1;
            if (level >= Hardware.Length || !CanAfford(state, HardwareCost(level))) return false;

            state.Donations -= HardwareCost(level);
            state.Hardware = level;
            return true;
        }

        public static bool BuyMarketing(TycoonState state, string key)
        {
            if (Marketing.All(m => m.Key != key)) return false;

            int owned = state.MarketingCount(key);
            if (owned >= MAX_COUNT) return false;

            long cost = MarketingCost(key, owned);
            if (!CanAfford(state, cost)) return false;

            state.Donations -= cost;
            state.Marketing[key] = owned + 1;
            return true;
        }

        public static bool BuyFeature(TycoonState state, string key)
        {
            if (FeatureOf(key) == null || state.Has(key) || !CanAfford(state, FeatureCost(key))) return false;

            state.Donations -= FeatureCost(key);
            state.Features.Add(key);
            state.Features.Sort(StringComparer.Ordinal);
            return true;
        }

        public static int LegacyGain(double runTotal) => runTotal < PRESTIGE_MIN_TOTAL ? 0 : (int)Math.Floor(Math.Sqrt(runTotal / PRESTIGE_MIN_TOTAL));

        /// <summary>Season restart: everything but legacy, lifetime total, clicks, events and the player record starts over. Returns the legacy gained (0 = refused).</summary>
        public static int Prestige(TycoonState state)
        {
            int gain = LegacyGain(state.RunTotal);
            if (gain <= 0) return 0;

            var fresh = new TycoonState
            {
                LifetimeTotal = state.LifetimeTotal,
                Clicks = state.Clicks,
                Events = state.Events,
                PlaySeconds = state.PlaySeconds,
                MaxPlayers = state.MaxPlayers,
                Prestige = state.Prestige + 1,
                Legacy = state.Legacy + gain,
            };

            state.Donations = fresh.Donations;
            state.RunTotal = fresh.RunTotal;
            state.LifetimeTotal = fresh.LifetimeTotal;
            state.Players = fresh.Players;
            state.MaxPlayers = fresh.MaxPlayers;
            state.Ruf = fresh.Ruf;
            state.Hardware = fresh.Hardware;
            state.Marketing = fresh.Marketing;
            state.Features = fresh.Features;
            state.Clicks = fresh.Clicks;
            state.Prestige = fresh.Prestige;
            state.Legacy = fresh.Legacy;
            state.Events = fresh.Events;
            state.PlaySeconds = fresh.PlaySeconds;
            return gain;
        }

        /// <summary>The server's bound on sustainable players for this save (see economy.py).</summary>
        public static double MaxPlayersBound(TycoonState state)
        {
            double joined = MarketingRate(state) * InflowMult(state.Features) * LegacyMult(state.Legacy) * 1.5 * EVENT_BOOST;
            joined += MAX_CLICKS_PER_SECOND * ClickValue(state);
            double sustainable = (joined + Capacity(state.Hardware) * OVERLOAD_CHURN) / (CHURN + OVERLOAD_CHURN);
            return Math.Max(1.5 * Capacity(state.Hardware), sustainable * 1.25);
        }

        /// <summary>The server's bound on donations per second for this save.</summary>
        public static double MaxDonationRate(TycoonState state) =>
            MaxPlayersBound(state) * DONATION_PER_PLAYER * 1.5 * DonationMult(state.Features) * LegacyMult(state.Legacy) * EVENT_BOOST;
    }
}

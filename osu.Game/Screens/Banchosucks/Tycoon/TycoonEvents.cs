// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    /// <summary>
    /// Temporary multipliers an event leaves behind (a streamer, a broken deploy); not part of the save.
    /// </summary>
    internal class TycoonBoosts
    {
        private double inflowFactor = 1, donationFactor = 1;
        private double inflowRemaining, donationRemaining;

        public double Inflow => inflowRemaining > 0 ? inflowFactor : 1;
        public double Donation => donationRemaining > 0 ? donationFactor : 1;

        public double InflowRemaining => Math.Max(0, inflowRemaining);
        public double DonationRemaining => Math.Max(0, donationRemaining);

        public void SetInflow(double factor, double seconds)
        {
            inflowFactor = factor;
            inflowRemaining = seconds;
        }

        public void SetDonation(double factor, double seconds)
        {
            donationFactor = factor;
            donationRemaining = seconds;
        }

        public void Tick(double dt)
        {
            inflowRemaining -= dt;
            donationRemaining -= dt;
        }

        public void Clear()
        {
            inflowRemaining = donationRemaining = 0;
        }
    }

    /// <summary>What happens when an event is applied or a choice is taken: changes the state, returns the line for the log.</summary>
    internal delegate LocalisableString TycoonEffect(TycoonState state, TycoonBoosts boosts);

    internal class TycoonEventChoice
    {
        public readonly LocalisableString Label;
        public readonly TycoonEffect Apply;

        public TycoonEventChoice(LocalisableString label, TycoonEffect apply)
        {
            Label = label;
            Apply = apply;
        }
    }

    /// <summary>
    /// Something that happens to the server. With choices it waits for the player, otherwise <see cref="Auto"/> is applied at once.
    /// </summary>
    internal class TycoonEvent
    {
        public readonly string Key;
        public readonly LocalisableString Title;
        public readonly LocalisableString Text;
        public readonly IconUsage Icon;
        public readonly Color4 Colour;
        public readonly List<TycoonEventChoice> Choices = new List<TycoonEventChoice>();
        public TycoonEffect? Auto;

        /// <summary>A crisis instead of <see cref="Auto"/>: applied with severity 1, or 0.5 when the player rebooted the servers in time.</summary>
        public Func<TycoonState, TycoonBoosts, double, LocalisableString>? Crisis;

        public TycoonEvent(string key, LocalisableString title, LocalisableString text, IconUsage icon, Color4 colour)
        {
            Key = key;
            Title = title;
            Text = text;
            Icon = icon;
            Colour = colour;
        }
    }

    /// <summary>
    /// The event deck of the tycoon. Features turn bad events harmless or even good: anticheat bans the cheaters,
    /// backups restore the database, Cloudflare swallows the DDoS, tests catch the broken deploy.
    /// </summary>
    internal static class TycoonEvents
    {
        public const double MIN_INTERVAL = 75;
        public const double MAX_INTERVAL = 150;

        /// <summary>Clicks on the server block that count as an emergency reboot, and the time to do them in.</summary>
        public const int CRISIS_CLICKS = 8;

        public const double CRISIS_MS = 10_000;

        private static LocalisableString t(string en, string de, params object[] args) => ClickerStrings.Text(en, de, args);

        private static double income(TycoonState state, double seconds) => Math.Max(20, TycoonRules.DonationRate(state) * seconds);

        private static LocalisableString pay(TycoonState state, double amount, string en, string de)
        {
            state.Donations += amount;
            state.RunTotal += amount;
            state.LifetimeTotal += amount;
            return t(en, de, ClickerFormat.Number(amount));
        }

        public static TycoonEvent Roll(TycoonState state, Random rng, OsuColour colours)
        {
            var deck = new List<(int weight, Func<TycoonEvent> make)>
            {
                (3, () => cheaters(state, rng, colours)),
                (2, () => ddos(state, rng, colours)),
                (2, () => dbCrash(state, colours)),
                (3, () => streamer(colours)),
                (2, () => deployFail(state, colours)),
                (1, () => medalCrash(state, colours)),
                (3, () => donor(state, colours)),
                (2, () => weekend(colours)),
            };

            if (state.Has("lazer"))
                deck.Add((2, () => lazerUpdate(colours)));

            int total = deck.Sum(d => d.weight);
            int roll = rng.Next(total);

            foreach (var (weight, make) in deck)
            {
                if (roll < weight) return make();

                roll -= weight;
            }

            return deck[0].make();
        }

        private static TycoonEvent cheaters(TycoonState state, Random rng, OsuColour colours)
        {
            int accounts = (int)Math.Ceiling(state.Players * 0.03) + rng.Next(1, 4);

            if (state.Has("anticheat"))
            {
                return new TycoonEvent("cheaters", t("Cheater wave", "Cheater-Welle"),
                    t("The anticheat flagged {0} accounts with identical device ids.", "Das Anticheat hat {0} Konten mit gleicher Gerätekennung erwischt.", accounts),
                    FontAwesome.Solid.UserShield, colours.Green)
                {
                    Auto = (s, _) =>
                    {
                        s.Ruf = Math.Min(100, s.Ruf + 3);
                        return pay(s, income(s, 20), "{0} accounts banned, the community cheers: reputation +3, {0} donations", "{0} Konten gesperrt, die Community jubelt: Ruf +3, {0} Spenden");
                    },
                };
            }

            var ev = new TycoonEvent("cheaters", t("Cheater wave", "Cheater-Welle"),
                t("{0} suspicious accounts top the leaderboards. Without an anticheat it is your call.", "{0} verdächtige Konten stehen oben in den Ranglisten. Ohne Anticheat ist es deine Entscheidung.", accounts),
                FontAwesome.Solid.UserSecret, colours.Red);
            ev.Choices.Add(new TycoonEventChoice(t("Ban them", "Sperren"), (s, _) =>
            {
                s.Players *= 0.95;
                s.Ruf = Math.Min(100, s.Ruf + 4);
                return t("Banned. A few friends left with them, the rest trusts you more: players -5 %, reputation +4", "Gesperrt. Ein paar Freunde sind mitgegangen, der Rest vertraut dir mehr: Spieler -5 %, Ruf +4");
            }));
            ev.Choices.Add(new TycoonEventChoice(t("Ignore it", "Ignorieren"), (s, _) =>
            {
                s.Players *= 1.03;
                s.Ruf = Math.Max(0, s.Ruf - 12);
                return t("The cheaters stay, the honest players grumble: players +3 %, reputation -12", "Die Cheater bleiben, die Ehrlichen murren: Spieler +3 %, Ruf -12");
            }));
            return ev;
        }

        private static TycoonEvent ddos(TycoonState state, Random rng, OsuColour colours)
        {
            int gbit = rng.Next(4, 40);

            if (state.Has("cloudflare"))
            {
                return new TycoonEvent("ddos", t("DDoS attack", "DDoS-Angriff"),
                    t("{0} Gbit/s hit the edge. Cloudflare swallowed all of it.", "{0} Gbit/s prasseln auf den Rand. Cloudflare hat alles geschluckt.", gbit),
                    FontAwesome.Solid.ShieldAlt, colours.Green)
                {
                    Auto = (s, _) =>
                    {
                        s.Ruf = Math.Min(100, s.Ruf + 1);
                        return t("Nobody noticed. Reputation +1", "Keiner hat es gemerkt. Ruf +1");
                    },
                };
            }

            return new TycoonEvent("ddos", t("DDoS attack", "DDoS-Angriff"),
                t("{0} Gbit/s and the server is gone. Emergency reboot: click the servers {1} times within {2} seconds for half the damage.",
                    "{0} Gbit/s, und der Server ist weg. Notfall-Reboot: Klick die Server {1}-mal in {2} Sekunden für halben Schaden.", gbit, CRISIS_CLICKS, CRISIS_MS / 1000),
                FontAwesome.Solid.Bolt, colours.Red)
            {
                Crisis = (s, _, severity) =>
                {
                    s.Players *= 1 - 0.25 * severity;
                    s.Ruf = Math.Max(0, s.Ruf - 6 * severity);
                    return severity < 1
                        ? t("Rebooted in time: players -12.5 %, reputation -3", "Rechtzeitig neu gestartet: Spieler -12,5 %, Ruf -3")
                        : t("Players -25 %, reputation -6. Cloudflare would have helped.", "Spieler -25 %, Ruf -6. Cloudflare hätte geholfen.");
                },
            };
        }

        private static TycoonEvent dbCrash(TycoonState state, OsuColour colours)
        {
            if (state.Has("backups"))
            {
                return new TycoonEvent("dbcrash", t("Database crash", "Datenbank-Crash"),
                    t("The disk gave up. The backup from 04:15 UTC is back in place after ten minutes.", "Die Platte hat aufgegeben. Das Backup von 04:15 UTC ist nach zehn Minuten wieder drin."),
                    FontAwesome.Solid.Database, colours.Green)
                {
                    Auto = (_, _) => t("Nothing lost.", "Nichts verloren."),
                };
            }

            var ev = new TycoonEvent("dbcrash", t("Database crash", "Datenbank-Crash"),
                t("The disk gave up and there is no backup.", "Die Platte hat aufgegeben, und es gibt kein Backup."),
                FontAwesome.Solid.Database, colours.Red);
            ev.Choices.Add(new TycoonEventChoice(t("Rebuild from logs", "Aus Logs rekonstruieren"), (s, _) =>
            {
                s.Donations *= 0.85;
                return t("Three nights of work, paid from the donations: -15 %", "Drei Nächte Arbeit, bezahlt aus den Spenden: -15 %");
            }));
            ev.Choices.Add(new TycoonEventChoice(t("Shut down and wait", "Abschalten und warten"), (s, _) =>
            {
                s.Players *= 0.8;
                s.Ruf = Math.Max(0, s.Ruf - 3);
                return t("A day offline: players -20 %, reputation -3", "Ein Tag offline: Spieler -20 %, Ruf -3");
            }));
            return ev;
        }

        private static TycoonEvent streamer(OsuColour colours) => new TycoonEvent("streamer", t("A streamer plays on your server", "Ein Streamer spielt auf deinem Server"),
            t("Chat spams the invite link.", "Der Chat spammt den Einladungslink."), FontAwesome.Solid.Video, colours.Purple)
        {
            Auto = (_, b) =>
            {
                b.SetInflow(2.0, 60);
                return t("Player inflow x2 for 60 seconds", "Zulauf x2 für 60 Sekunden");
            },
        };

        private static TycoonEvent deployFail(TycoonState state, OsuColour colours)
        {
            if (state.Has("tests"))
            {
                return new TycoonEvent("deploy", t("Deploy", "Deploy"),
                    t("The smoke check caught an 'Illegal mix of collations' before anything went live.", "Der Smoke-Check hat ein „Illegal mix of collations“ gefangen, bevor etwas live ging."),
                    FontAwesome.Solid.CheckCircle, colours.Green)
                {
                    Auto = (_, _) => t("Rolled back automatically, nobody noticed.", "Automatisch zurückgerollt, keiner hat es gemerkt."),
                };
            }

            return new TycoonEvent("deploy", t("Deploy failed", "Deploy fehlgeschlagen"),
                t("'Illegal mix of collations': the leaderboards return 500. Roll back by hand: click the servers {0} times within {1} seconds.",
                    "„Illegal mix of collations“: Die Ranglisten liefern 500. Von Hand zurückrollen: Klick die Server {0}-mal in {1} Sekunden.", CRISIS_CLICKS, CRISIS_MS / 1000),
                FontAwesome.Solid.ExclamationTriangle, colours.Yellow)
            {
                Crisis = (_, b, severity) =>
                {
                    b.SetDonation(severity < 1 ? 0.75 : 0.5, 45);
                    return severity < 1
                        ? t("Rolled back by hand: donations x0.75 for 45 seconds", "Von Hand zurückgerollt: Spenden x0,75 für 45 Sekunden")
                        : t("Donations halved for 45 seconds", "Spenden halbiert für 45 Sekunden");
                },
            };
        }

        private static TycoonEvent medalCrash(TycoonState state, OsuColour colours)
        {
            if (state.Has("tests"))
            {
                return new TycoonEvent("medal", t("New medal", "Neue Medaille"),
                    t("'One Year In' reads an attribute the player object does not have. The unit test failed first.", "„One Year In“ liest ein Attribut, das der Spieler nicht hat. Der Unit-Test ist zuerst gescheitert."),
                    FontAwesome.Solid.Medal, colours.Green)
                {
                    Auto = (_, _) => t("Fixed before the deploy.", "Vor dem Deploy repariert."),
                };
            }

            return new TycoonEvent("medal", t("New medal crashes score submission", "Neue Medaille crasht die Score-Einreichung"),
                t("Every finished play ends in an error. Hotfix: click the servers {0} times within {1} seconds for half the damage.",
                    "Jeder gespielte Score endet in einem Fehler. Hotfix: Klick die Server {0}-mal in {1} Sekunden für halben Schaden.", CRISIS_CLICKS, CRISIS_MS / 1000),
                FontAwesome.Solid.Medal, colours.Red)
            {
                Crisis = (s, _, severity) =>
                {
                    s.Players *= 1 - 0.1 * severity;
                    s.Ruf = Math.Max(0, s.Ruf - 4 * severity);
                    return severity < 1
                        ? t("Hotfix shipped: players -5 %, reputation -2", "Hotfix ausgeliefert: Spieler -5 %, Ruf -2")
                        : t("Players -10 %, reputation -4. Tests would have caught it.", "Spieler -10 %, Ruf -4. Tests hätten es gefangen.");
                },
            };
        }

        private static TycoonEvent donor(TycoonState state, OsuColour colours) => new TycoonEvent("donor", t("A generous donor", "Ein großzügiger Spender"),
            t("Someone paid the server bill for a whole month.", "Jemand hat die Serverrechnung für einen ganzen Monat bezahlt."), FontAwesome.Solid.Heart, colours.Pink)
        {
            Auto = (s, _) => pay(s, income(s, 60), "+{0} donations", "+{0} Spenden"),
        };

        private static TycoonEvent weekend(OsuColour colours) => new TycoonEvent("weekend", t("Weekend", "Wochenende"),
            t("Everyone is online and in a good mood.", "Alle sind online und gut gelaunt."), FontAwesome.Solid.CalendarAlt, colours.Blue)
        {
            Auto = (_, b) =>
            {
                b.SetDonation(1.5, 90);
                return t("Donations x1.5 for 90 seconds", "Spenden x1,5 für 90 Sekunden");
            },
        };

        private static TycoonEvent lazerUpdate(OsuColour colours) => new TycoonEvent("lazer", t("New lazer release", "Neue Lazer-Version"),
            t("The release app did its thing: new client, new players.", "Die Release-App hat ihre Arbeit getan: neuer Client, neue Spieler."), FontAwesome.Solid.Rocket, colours.Blue)
        {
            Auto = (_, b) =>
            {
                b.SetInflow(1.5, 120);
                return t("Player inflow x1.5 for 120 seconds", "Zulauf x1,5 für 120 Sekunden");
            },
        };
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: writes a <see cref="BanchosucksOverlayState"/> as the JSON document of tosu's v2 API
    /// (<c>ws://127.0.0.1:24050/websocket/v2</c>, <c>GET /json/v2</c>), which stream overlays read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the ONLY place that knows tosu's field names. Every key is written literally below, in the order of the
    /// document, so a name that turns out to be wrong is corrected by changing one string here (and the matching
    /// assertion in <c>BanchosucksOverlayJsonTest</c>).
    /// </para>
    /// <para>
    /// The names were written down from memory of tosu's v2 schema, without access to its source. Sure: the top-level
    /// keys, <c>state</c>, <c>beatmap</c> (including <c>time</c> and <c>stats</c>), <c>play</c> (score, accuracy, combo,
    /// hits, healthBar, mods, rank, pp.current, pp.fc), <c>resultsScreen</c>, <c>profile.name</c>, <c>folders</c>,
    /// <c>files</c>. Less sure: everything under <c>settings</c>, the numbers of <c>profile.userStatus</c>, the names
    /// under <c>play.pp.detailed</c>, <c>play.pp.maxAchieved*</c>, the extra hit counts of lazer
    /// (<c>sliderEndHits</c>, <c>smallTickHits</c>, <c>largeTickHits</c>) and the star rating parts
    /// (<c>beatmap.stats.stars.*</c> other than <c>live</c> and <c>total</c>).
    /// </para>
    /// <para>
    /// A value the client does not know is written as 0, false, an empty string or an empty array, never made up:
    /// those lines carry the comment "not known". Paths (<c>folders</c>, <c>files</c>, <c>directPath</c>) are always
    /// empty: lazer keeps beatmap files under hashed names, and a local path on stream would show the Windows user name.
    /// </para>
    /// </remarks>
    public static class BanchosucksOverlayJson
    {
        private static readonly JsonWriterOptions writer_options = new JsonWriterOptions
        {
            // titles and names go out as they are (UTF-8) instead of \uXXXX
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly string[] unknown_accuracy_keys = { "90", "91", "92", "93", "94", "95", "96", "97", "98", "99" };

        public static string Build(BanchosucksOverlayState state)
        {
            var buffer = new ArrayBufferWriter<byte>(4096);

            using (var json = new Utf8JsonWriter(buffer, writer_options))
            {
                json.WriteStartObject();

                json.WriteString("client", "lazer");
                json.WriteString("server", "banchosucks.cc");

                writeNumberName(json, "state", (int)state.Game, GameStateName(state.Game));

                json.WriteStartObject("session");
                json.WriteNumber("playTime", state.SessionPlayTime);
                json.WriteNumber("playCount", state.SessionPlayCount);
                json.WriteEndObject();

                writeSettings(json, state);
                writeProfile(json, state);
                writeBeatmap(json, state.Beatmap);
                writePlay(json, state.Play);

                // other players' scores: not known (and not wanted in streamer mode)
                json.WriteStartArray("leaderboard");
                json.WriteEndArray();

                writePerformance(json, state.Beatmap);
                writeResults(json, state.Results);
                writePaths(json);

                json.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        #region settings

        private static void writeSettings(Utf8JsonWriter json, BanchosucksOverlayState state)
        {
            var settings = state.Settings;

            json.WriteStartObject("settings");

            json.WriteBoolean("interfaceVisible", settings.InterfaceVisible);
            json.WriteBoolean("replayUIVisible", false); // not known
            writeNumberName(json, "chatVisibilityStatus", 0, "hidden"); // not reported on purpose

            json.WriteStartObject("leaderboard");
            json.WriteBoolean("visible", false); // not known
            writeNumberName(json, "type", 0, "local"); // not known
            json.WriteEndObject();

            writeNumberName(json, "progressBar", 0, "off"); // not known
            json.WriteNumber("bassDensity", 0); // not known

            json.WriteStartObject("resolution");
            json.WriteBoolean("fullscreen", settings.Fullscreen);
            json.WriteNumber("width", settings.WindowWidth);
            json.WriteNumber("height", settings.WindowHeight);
            json.WriteNumber("widthFullscreen", settings.Fullscreen ? settings.WindowWidth : 0);
            json.WriteNumber("heightFullscreen", settings.Fullscreen ? settings.WindowHeight : 0);
            json.WriteEndObject();

            json.WriteStartObject("client");
            json.WriteBoolean("updateAvailable", false); // not known
            json.WriteNumber("branch", 0); // stable only
            json.WriteString("version", state.ClientVersion);
            json.WriteEndObject();

            json.WriteStartObject("scoreMeter");
            writeNumberName(json, "type", 0, "none"); // not known
            json.WriteNumber("size", 0); // not known
            json.WriteEndObject();

            json.WriteStartObject("cursor");
            json.WriteBoolean("useSkinCursor", false); // not known
            json.WriteBoolean("autoSize", false); // not known
            json.WriteNumber("size", number(settings.CursorSize));
            json.WriteEndObject();

            json.WriteStartObject("mouse");
            json.WriteBoolean("rawInput", false); // not known
            json.WriteBoolean("disableButtons", false); // not known
            json.WriteBoolean("disableWheel", false); // not known
            json.WriteNumber("sensitivity", 0); // not known
            json.WriteEndObject();

            json.WriteStartObject("mania");
            json.WriteBoolean("speedBPMScale", false); // not known
            json.WriteBoolean("usePerBeatmapSpeedScale", false); // not known
            json.WriteEndObject();

            writeNumberName(json, "sort", 0, "artist"); // not known
            writeNumberName(json, "group", 0, "none"); // not known

            json.WriteStartObject("skin");
            json.WriteBoolean("useDefaultSkinInEditor", false); // stable only
            json.WriteBoolean("ignoreBeatmapSkins", false); // not known
            json.WriteBoolean("tintSliderBall", false); // stable only
            json.WriteBoolean("useTaikoSkin", false); // stable only
            json.WriteString("name", settings.SkinName);
            json.WriteEndObject();

            writeMode(json, state.RulesetId);

            json.WriteStartObject("audio");
            json.WriteBoolean("ignoreBeatmapSounds", false); // not known
            json.WriteBoolean("useSkinSamples", false); // not known
            json.WriteStartObject("volume");
            json.WriteNumber("master", number(settings.VolumeMaster));
            json.WriteNumber("music", number(settings.VolumeMusic));
            json.WriteNumber("effect", number(settings.VolumeEffect));
            json.WriteEndObject();
            json.WriteStartObject("offset");
            json.WriteNumber("universal", number(settings.AudioOffset));
            json.WriteEndObject();
            json.WriteEndObject();

            json.WriteStartObject("background");
            json.WriteNumber("dim", number(settings.BackgroundDim));
            json.WriteBoolean("video", settings.Storyboard); // lazer has one switch for storyboard and video
            json.WriteBoolean("storyboard", settings.Storyboard);
            json.WriteEndObject();

            // key names: not reported
            json.WriteStartObject("keybinds");
            json.WriteStartObject("osu");
            json.WriteString("k1", string.Empty);
            json.WriteString("k2", string.Empty);
            json.WriteString("smokeKey", string.Empty);
            json.WriteEndObject();
            json.WriteStartObject("fruits");
            json.WriteString("k1", string.Empty);
            json.WriteString("k2", string.Empty);
            json.WriteString("Dash", string.Empty);
            json.WriteEndObject();
            json.WriteStartObject("taiko");
            json.WriteString("innerLeft", string.Empty);
            json.WriteString("innerRight", string.Empty);
            json.WriteString("outerLeft", string.Empty);
            json.WriteString("outerRight", string.Empty);
            json.WriteEndObject();
            json.WriteString("quickRetry", string.Empty);
            json.WriteEndObject();

            json.WriteEndObject();
        }

        #endregion

        #region profile

        private static void writeProfile(Utf8JsonWriter json, BanchosucksOverlayState state)
        {
            var profile = state.Profile;

            json.WriteStartObject("profile");

            // tosu passes on stable's login state; of those only "connected" and "disconnected" exist here
            if (profile.Online)
                writeNumberName(json, "userStatus", 65793, "connected");
            else
                writeNumberName(json, "userStatus", 65537, "disconnected");

            writeNumberName(json, "banchoStatus", profile.ActivityNumber, profile.ActivityName);

            json.WriteNumber("id", profile.Id);
            json.WriteString("name", profile.Name);
            writeMode(json, state.RulesetId);
            json.WriteNumber("rankedScore", profile.RankedScore);
            json.WriteNumber("level", number(profile.Level));
            json.WriteNumber("accuracy", number(profile.Accuracy));
            json.WriteNumber("pp", number(profile.PP));
            json.WriteNumber("playCount", profile.PlayCount);
            json.WriteNumber("globalRank", profile.GlobalRank);

            // the player's country is not handed out
            writeNumberName(json, "countryCode", 0, string.Empty);
            json.WriteString("backgroundColour", "000000"); // stable only

            json.WriteEndObject();
        }

        #endregion

        #region beatmap

        private static void writeBeatmap(Utf8JsonWriter json, BanchosucksOverlayState.BeatmapState beatmap)
        {
            json.WriteStartObject("beatmap");

            json.WriteBoolean("isKiai", beatmap.IsKiai);
            json.WriteBoolean("isBreak", beatmap.IsBreak);
            json.WriteBoolean("isConvert", beatmap.IsConvert);

            json.WriteStartObject("time");
            json.WriteNumber("live", whole(beatmap.TimeLive));
            json.WriteNumber("firstObject", whole(beatmap.TimeFirstObject));
            json.WriteNumber("lastObject", whole(beatmap.TimeLastObject));
            json.WriteNumber("mp3Length", whole(beatmap.TimeMp3Length));
            json.WriteEndObject();

            writeNumberName(json, "status", beatmap.StatusNumber, RankedStatusName(beatmap.StatusNumber));

            json.WriteString("checksum", beatmap.Checksum);
            json.WriteNumber("id", beatmap.Id);
            json.WriteNumber("set", beatmap.SetId);
            writeMode(json, beatmap.RulesetId);

            json.WriteString("artist", beatmap.Artist);
            json.WriteString("artistUnicode", beatmap.ArtistUnicode);
            json.WriteString("title", beatmap.Title);
            json.WriteString("titleUnicode", beatmap.TitleUnicode);
            json.WriteString("mapper", beatmap.Mapper);
            json.WriteString("version", beatmap.Version);

            json.WriteStartObject("stats");

            json.WriteStartObject("stars");
            json.WriteNumber("live", number(beatmap.StarsLive));
            // the parts of the star rating differ per ruleset and are not reported
            json.WriteNumber("aim", 0);
            json.WriteNumber("speed", 0);
            json.WriteNumber("flashlight", 0);
            json.WriteNumber("sliderFactor", 0);
            json.WriteNumber("stamina", 0);
            json.WriteNumber("rhythm", 0);
            json.WriteNumber("color", 0);
            json.WriteNumber("reading", 0);
            json.WriteNumber("hitWindow", 0);
            json.WriteNumber("total", number(beatmap.Stars));
            json.WriteEndObject();

            writeOriginalConverted(json, "ar", beatmap.ApproachRate, beatmap.ApproachRateConverted);
            writeOriginalConverted(json, "cs", beatmap.CircleSize, beatmap.CircleSizeConverted);
            writeOriginalConverted(json, "od", beatmap.OverallDifficulty, beatmap.OverallDifficultyConverted);
            writeOriginalConverted(json, "hp", beatmap.DrainRate, beatmap.DrainRateConverted);

            json.WriteStartObject("bpm");
            json.WriteNumber("realtime", number(beatmap.BpmRealtime));
            json.WriteNumber("common", number(beatmap.BpmCommon));
            json.WriteNumber("min", number(beatmap.BpmMin));
            json.WriteNumber("max", number(beatmap.BpmMax));
            json.WriteEndObject();

            json.WriteStartObject("objects");
            json.WriteNumber("circles", beatmap.Circles);
            json.WriteNumber("sliders", beatmap.Sliders);
            json.WriteNumber("spinners", beatmap.Spinners);
            json.WriteNumber("holds", beatmap.Holds);
            json.WriteNumber("total", beatmap.Objects);
            json.WriteEndObject();

            json.WriteNumber("maxCombo", beatmap.MaxCombo);

            json.WriteEndObject(); // stats
            json.WriteEndObject(); // beatmap
        }

        #endregion

        #region play

        private static void writePlay(Utf8JsonWriter json, BanchosucksOverlayState.PlayState play)
        {
            json.WriteStartObject("play");

            json.WriteString("playerName", play.PlayerName);
            writeMode(json, play.RulesetId);
            json.WriteNumber("score", play.Score);
            json.WriteNumber("accuracy", number(play.Accuracy));

            // stable's health bar goes from 0 to 200, and that is what overlays divide by
            json.WriteStartObject("healthBar");
            json.WriteNumber("normal", number(play.Health * 200));
            json.WriteNumber("smooth", number(play.Health * 200));
            json.WriteEndObject();

            writeHits(json, play.Hits, true);

            // the offset of every single hit: not reported
            json.WriteStartArray("hitErrorArray");
            json.WriteEndArray();

            json.WriteStartObject("combo");
            json.WriteNumber("current", play.Combo);
            json.WriteNumber("max", play.MaxCombo);
            json.WriteEndObject();

            writeMods(json, play.Mods);

            json.WriteStartObject("rank");
            json.WriteString("current", play.Rank);
            json.WriteString("maxThisPlay", string.Empty); // not known
            json.WriteEndObject();

            json.WriteStartObject("pp");
            json.WriteNumber("current", number(play.PP.Total));
            json.WriteNumber("fc", number(play.PPIfFullCombo.Total));
            json.WriteNumber("maxAchieved", number(play.PPMaxThisPlay));
            json.WriteNumber("maxAchievedThisPlay", number(play.PPMaxThisPlay));
            json.WriteStartObject("detailed");
            writePerformanceParts(json, "current", play.PP);
            writePerformanceParts(json, "fc", play.PPIfFullCombo);
            json.WriteEndObject();
            json.WriteEndObject();

            json.WriteNumber("unstableRate", number(play.UnstableRate));

            json.WriteEndObject();
        }

        private static void writePerformanceParts(Utf8JsonWriter json, string name, BanchosucksOverlayState.PerformanceState pp)
        {
            json.WriteStartObject(name);
            json.WriteNumber("aim", number(pp.Aim));
            json.WriteNumber("speed", number(pp.Speed));
            json.WriteNumber("accuracy", number(pp.Accuracy));
            json.WriteNumber("difficulty", number(pp.Difficulty));
            json.WriteNumber("flashlight", number(pp.Flashlight));
            json.WriteNumber("total", number(pp.Total));
            json.WriteEndObject();
        }

        private static void writeHits(Utf8JsonWriter json, BanchosucksOverlayState.HitsState hits, bool live)
        {
            json.WriteStartObject("hits");
            json.WriteNumber("300", hits.Great);
            json.WriteNumber("geki", hits.Geki);
            json.WriteNumber("100", hits.Ok);
            json.WriteNumber("katu", hits.Katu);
            json.WriteNumber("50", hits.Meh);
            json.WriteNumber("0", hits.Miss);

            if (live)
                json.WriteNumber("sliderBreaks", 0); // not known

            json.WriteNumber("sliderEndHits", hits.SliderEndHits);
            json.WriteNumber("smallTickHits", hits.SmallTickHits);
            json.WriteNumber("largeTickHits", hits.LargeTickHits);
            json.WriteEndObject();
        }

        private static void writeMods(Utf8JsonWriter json, BanchosucksOverlayState.ModsState mods)
        {
            json.WriteStartObject("mods");
            json.WriteString("checksum", string.Empty); // not known
            json.WriteNumber("number", mods.Number);
            json.WriteString("name", string.Concat(mods.Acronyms));

            json.WriteStartArray("array");

            foreach (string acronym in mods.Acronyms)
            {
                json.WriteStartObject();
                json.WriteString("acronym", acronym);
                json.WriteEndObject();
            }

            json.WriteEndArray();

            json.WriteNumber("rate", number(mods.Rate));
            json.WriteEndObject();
        }

        #endregion

        #region performance, results, paths

        private static void writePerformance(Utf8JsonWriter json, BanchosucksOverlayState.BeatmapState beatmap)
        {
            json.WriteStartObject("performance");

            // pp of a full combo at each accuracy; only the perfect play is known
            json.WriteStartObject("accuracy");

            foreach (string accuracy in unknown_accuracy_keys)
                json.WriteNumber(accuracy, 0);

            json.WriteNumber("100", number(beatmap.MaxPP));
            json.WriteEndObject();

            // the difficulty graph over time: not reported
            json.WriteStartObject("graph");
            json.WriteStartArray("series");
            json.WriteEndArray();
            json.WriteStartArray("xaxis");
            json.WriteEndArray();
            json.WriteEndObject();

            json.WriteEndObject();
        }

        private static void writeResults(Utf8JsonWriter json, BanchosucksOverlayState.ResultsState results)
        {
            json.WriteStartObject("resultsScreen");

            json.WriteNumber("scoreId", results.ScoreId);
            json.WriteString("playerName", results.PlayerName);
            writeMode(json, results.RulesetId);
            json.WriteNumber("score", results.Score);
            json.WriteNumber("accuracy", number(results.Accuracy));
            json.WriteString("name", results.PlayerName);
            writeHits(json, results.Hits, false);
            writeMods(json, results.Mods);
            json.WriteNumber("maxCombo", results.MaxCombo);
            json.WriteString("rank", results.Rank);

            json.WriteStartObject("pp");
            json.WriteNumber("current", number(results.PP));
            json.WriteNumber("fc", 0); // not known
            json.WriteEndObject();

            json.WriteString("createdAt", results.CreatedAt);

            json.WriteEndObject();
        }

        private static void writePaths(Utf8JsonWriter json)
        {
            // see the class remarks: never filled
            json.WriteStartObject("folders");
            json.WriteString("game", string.Empty);
            json.WriteString("skin", string.Empty);
            json.WriteString("songs", string.Empty);
            json.WriteString("beatmap", string.Empty);
            json.WriteEndObject();

            json.WriteStartObject("files");
            json.WriteString("beatmap", string.Empty);
            json.WriteString("background", string.Empty);
            json.WriteString("audio", string.Empty);
            json.WriteEndObject();

            json.WriteStartObject("directPath");
            json.WriteString("beatmapFile", string.Empty);
            json.WriteString("beatmapBackground", string.Empty);
            json.WriteString("beatmapAudio", string.Empty);
            json.WriteString("beatmapFolder", string.Empty);
            json.WriteString("skinFolder", string.Empty);
            json.WriteEndObject();
        }

        #endregion

        #region names and numbers

        public static string GameStateName(BanchosucksOverlayGameState state)
        {
            switch (state)
            {
                case BanchosucksOverlayGameState.Edit:
                    return "edit";

                case BanchosucksOverlayGameState.Play:
                    return "play";

                case BanchosucksOverlayGameState.SelectPlay:
                    return "selectPlay";

                case BanchosucksOverlayGameState.ResultScreen:
                    return "resultScreen";

                case BanchosucksOverlayGameState.Lobby:
                    return "lobby";

                default:
                    return "menu";
            }
        }

        /// <summary>
        /// tosu's (stable's) ranked status: 0 unknown, 1 not submitted, 2 pending (also WIP and graveyard), 4 ranked, 5 approved, 6 qualified, 7 loved.
        /// </summary>
        public static string RankedStatusName(int number)
        {
            switch (number)
            {
                case 1:
                    return "notSubmitted";

                case 2:
                    return "pending";

                case 4:
                    return "ranked";

                case 5:
                    return "approved";

                case 6:
                    return "qualified";

                case 7:
                    return "loved";

                default:
                    return "unknown";
            }
        }

        public static string ModeName(int rulesetId)
        {
            switch (rulesetId)
            {
                case 0:
                    return "osu";

                case 1:
                    return "taiko";

                case 2:
                    return "fruits";

                case 3:
                    return "mania";

                default:
                    return string.Empty;
            }
        }

        private static void writeMode(Utf8JsonWriter json, int rulesetId)
            => writeNumberName(json, "mode", rulesetId, ModeName(rulesetId));

        private static void writeNumberName(Utf8JsonWriter json, string key, int number, string name)
        {
            json.WriteStartObject(key);
            json.WriteNumber("number", number);
            json.WriteString("name", name);
            json.WriteEndObject();
        }

        private static void writeOriginalConverted(Utf8JsonWriter json, string key, double original, double converted)
        {
            json.WriteStartObject(key);
            json.WriteNumber("original", number(original));
            json.WriteNumber("converted", number(converted));
            json.WriteEndObject();
        }

        /// <summary>
        /// Two decimals, and never NaN or infinity (which JSON cannot hold).
        /// </summary>
        private static double number(double value) => double.IsFinite(value) ? Math.Round(value, 2) : 0;

        private static long whole(double value) => double.IsFinite(value) ? (long)Math.Round(value) : 0;

        #endregion
    }
}

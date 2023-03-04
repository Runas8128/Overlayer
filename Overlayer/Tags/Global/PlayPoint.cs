﻿using System;
using System.Collections.Generic;
using System.Linq;
using Overlayer.Core;
using Overlayer.AdofaiggApi;
using AgLevel = Overlayer.AdofaiggApi.Types.Level;
using Overlayer.Core.Utils;
using SA.GoogleDoc;
using System.Threading.Tasks;
using Overlayer.Patches;
using ACL = Overlayer.MapParser.CustomLevel;
using System.IO;
using JSON;
using Vostok.Commons.Helpers.Extensions;
using System.Management.Instrumentation;
using Discord;

namespace Overlayer.Tags.Global
{
    [ClassTag("PlayPoint")]
    public static class PlayPoint
    {
        public static double IntegratedDifficulty = 0;
        public static double ForumDifficulty = 0;
        public static double PredictedDifficulty = 0;
        [Tag]
        public static double PlayPointValue(double digits = -1)
        {
            double result;
            var edit = scnEditor.instance;
            if (edit)
                result = (double)CalculatePlayPoint(IntegratedDifficulty, edit.levelData.pitch, Misc.XAccuracy(), scrLevelMaker.instance.listFloors.Count);
            else result = (double)CalculatePlayPoint(IntegratedDifficulty, (int)Math.Round(Misc.Pitch() * 100), Misc.XAccuracy(), scrLevelMaker.instance.listFloors.Count);
            return result.Round(digits);
        }
        public static double CalculatePlayPoint(double difficulty, int speed, double accuracy, int tile)
        {
            if (difficulty < 1) return 0.0;
            double difficultyRating = 1600.0 / (1.0 + Math.Exp(-0.3 * difficulty + 5.5));
            double xAccuracy = accuracy / 100.0;
            double accuracyRating = 0.03 / (-PowEx(Math.Min(1, xAccuracy), PowEx(tile, 0.05)) + 1.05) + 0.4;
            double pitch = speed / 100.0;
            double pitchRating = pitch >= 1 ? PowEx((2 + pitch) / 3.0, PowEx(0.1 + PowEx(tile, 0.5) / PowEx(2000, 0.5), 1.1)) : PowEx(pitch, 1.8);
            double tilesRating = tile < 2000 ? 0.9 + tile / 10000.0 : PowEx(tile / 2000.0, 0.05);
            return PowEx(difficultyRating * accuracyRating * pitchRating * tilesRating, 1.01);
        }
        public static async void Setup(scnEditor instance)
        {
            IntegratedDifficulty = 0;
            PredictedDifficulty = 0;
            ForumDifficulty = 0;
            if (instance)
            {
                try
                {
                    var levelData = instance.levelData;
                    string artist = levelData.artist.BreakRichTag(), author = levelData.author.BreakRichTag(), title = levelData.song.BreakRichTag();
                    var result = await Request(artist, title, author, string.IsNullOrWhiteSpace(levelData.pathData) ? levelData.angleData.Count : levelData.pathData.Length, (int)Math.Round(levelData.bpm));
#if DIFFICULTY_PREDICTOR
                    PredictDiff(instance, !(result?.difficulty).HasValue);
                    IntegratedDifficulty = (result?.difficulty).HasValue ? (ForumDifficulty = result.difficulty) : PredictedDifficulty;
#else
                    PredictedDifficulty = ((double)instance.levelData.difficulty).Map(1, 10, 1, 21);
                    if ((result?.difficulty).HasValue)
                        IntegratedDifficulty = ForumDifficulty = result.difficulty;
                    else IntegratedDifficulty = PredictedDifficulty;
#endif
                }
                catch (Exception e)
                {
                    IntegratedDifficulty = PredictedDifficulty = ((double)instance.levelData.difficulty).Map(1, 10, 1, 21);
                    Main.Logger.Log($"Error On Requesting Difficulty At Adofai.gg. Check Adofai.gg's Api State.\n{e}");
                }
            }
        }
        public static async Task<AgLevel> Request(string artist, string title, string author, int tiles, int bpm)
        {
            Main.Logger.Log($"<b>Requesting {artist} - {title}, {author}</b>");
            var result = await AgLevel.Request(ActualParams());
            if (result.count <= 0)
            {
                Main.Logger.Log($"<b>Result Count Is {result.count}. Re-Requesting With Fixup Artist Name..</b>");
                result = await AgLevel.Request(ActualParams2());
                if (result.count <= 0)
                {
                    Main.Logger.Log($"<b>Result Count Is {result.count}. Re-Requesting With Fixup Author Name..</b>");
                    result = await AgLevel.Request(ActualParams3());
                    if (result.count <= 0)
                    {
                        Main.Logger.Log($"<b>Result Count Is {result.count}. Re-Requesting With Fixup Artist And Author Name..</b>");
                        result = await AgLevel.Request(ActualParams4());
                        if (result.count <= 0)
                            return Fail();
                        return result.results[0];
                    }
                    return result.results[0];
                }
                return result.results[0];
            }
            if (result.count > 1)
            {
                Main.Logger.Log($"<b>Result Count Is {result.count}. Re-Requesting With Bpm..</b>");
                result = await AgLevel.Request(ActualParams(AgLevel.MinBpm(bpm - 1), AgLevel.MaxBpm(bpm + 1)));
            }
            if (result.count <= 0) return Fail();
            if (result.count > 1)
            {
                Main.Logger.Log($"<b>Result Count Is {result.count}. Re-Requesting With Tile Count..</b>");
                result = await AgLevel.Request(ActualParams(AgLevel.MinTiles(tiles - 1), AgLevel.MaxTiles(tiles + 1)));
            }
            if (result.count <= 0) return Fail();
            if (result.count > 1)
            {
                Main.Logger.Log($"<b>Result Count Is {result.count}. Re-Requesting With Bpm And Tile Count..</b>");
                result = await AgLevel.Request(ActualParams(AgLevel.MinBpm(bpm - 1), AgLevel.MaxBpm(bpm + 1), AgLevel.MinTiles(tiles - 1), AgLevel.MaxTiles(tiles + 1)));
            }
            if (result.count <= 0) return Fail();
            if (result.count > 1)
            {
                Main.Logger.Log($"<b>Result Count Is {result.count}. Use First Level.</b>");
                return result.results[0];
            }
            Main.Logger.Log("Success");
            return result.results[0];
            Parameters ActualParams(params Parameter[] with)
            {
                List<Parameter> parameters = new List<Parameter>(with);
                artist = artist.TrimEx();
                title = title.TrimEx();
                author = author.TrimEx();
                if (!string.IsNullOrWhiteSpace(artist) && !IsSameWithDefault("editor.artist", artist))
                    parameters.Add(AgLevel.QueryArtist(artist));
                if (!string.IsNullOrWhiteSpace(title) && !IsSameWithDefault("editor.title", title))
                    parameters.Add(AgLevel.QueryTitle(title));
                if (!string.IsNullOrWhiteSpace(author) && !IsSameWithDefault("editor.author", author))
                    parameters.Add(AgLevel.QueryCreator(author));
                return new Parameters(parameters);
            }
            Parameters ActualParams2(params Parameter[] with)
            {
                List<Parameter> parameters = new List<Parameter>(with);
                artist = artist.TrimEx().Replace(" ", "");
                title = title.TrimEx();
                author = author.TrimEx();
                if (!string.IsNullOrWhiteSpace(artist) && !IsSameWithDefault("editor.artist", artist))
                    parameters.Add(AgLevel.QueryArtist(artist));
                if (!string.IsNullOrWhiteSpace(title) && !IsSameWithDefault("editor.title", title))
                    parameters.Add(AgLevel.QueryTitle(title));
                if (!string.IsNullOrWhiteSpace(author) && !IsSameWithDefault("editor.author", author))
                    parameters.Add(AgLevel.QueryCreator(author));
                return new Parameters(parameters);
            }
            Parameters ActualParams3(params Parameter[] with)
            {
                List<Parameter> parameters = new List<Parameter>(with);
                artist = artist.TrimEx();
                title = title.TrimEx();
                author = author.TrimEx().Replace(" ", "");
                if (!string.IsNullOrWhiteSpace(artist) && !IsSameWithDefault("editor.artist", artist))
                    parameters.Add(AgLevel.QueryArtist(artist));
                if (!string.IsNullOrWhiteSpace(title) && !IsSameWithDefault("editor.title", title))
                    parameters.Add(AgLevel.QueryTitle(title));
                if (!string.IsNullOrWhiteSpace(author) && !IsSameWithDefault("editor.author", author))
                    parameters.Add(AgLevel.QueryCreator(author));
                return new Parameters(parameters);
            }
            Parameters ActualParams4(params Parameter[] with)
            {
                List<Parameter> parameters = new List<Parameter>(with);
                artist = artist.TrimEx().Replace(" ", "");
                title = title.TrimEx();
                author = author.TrimEx().Replace(" ", "");
                if (!string.IsNullOrWhiteSpace(artist) && !IsSameWithDefault("editor.artist", artist))
                    parameters.Add(AgLevel.QueryArtist(artist));
                if (!string.IsNullOrWhiteSpace(title) && !IsSameWithDefault("editor.title", title))
                    parameters.Add(AgLevel.QueryTitle(title));
                if (!string.IsNullOrWhiteSpace(author) && !IsSameWithDefault("editor.author", author))
                    parameters.Add(AgLevel.QueryCreator(author));
                return new Parameters(parameters);
            }
            AgLevel Fail()
            {
                Main.Logger.Log("Failed");
                return null;
            }
        }
        public static bool IsSameWithDefault(string key, string value)
        {
            if (!CachedState.Contains(key))
            {
                CachedStrings[key] = new string[]
                {
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Korean),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.English),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Japanese),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Spanish),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Portuguese),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.French),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Polish),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Romanian),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Russian),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Vietnamese),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.Czech),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.German),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.ChineseSimplified),
                    Localization.GetLocalizedString(key, LangSection.Translations, LangCode.ChineseTraditional),
                };
                CachedState.Add(key);
            }
            return CachedStrings[key].Contains(value);
        }
#if DIFFICULTY_PREDICTOR
        public static async void PredictDiff(scnEditor editor, bool requireUpload)
        {
            try
            {
                IntegratedDifficulty = PredictedDifficulty = await PredictDifficulty(editor).TryWaitAsync(TimeSpan.FromSeconds(10));
                if (requireUpload)
                    LevelMeta.Upload(editor.customLevel.levelPath, editor.levelData.song, PredictedDifficulty.ToString());
            }
            catch (Exception e)
            {
                var levelPath = editor.customLevel.levelPath;
                Main.Logger.Log($"Error On Predicting Difficulty:\n{e}");
                Main.Logger.Log($"Level Path: {levelPath}");
                Main.Logger.Log($"Adjusting PredictedDifficulty To Editor Difficulty {IntegratedDifficulty = ForumDifficulty = ((double)editor.levelData.difficulty).Map(1, 10, 1, 21)}");
            }
        }
        public static async Task<double> PredictDifficulty(scnEditor editor)
        {
            var levelData = editor.levelData;
            var hash = DataInit.MakeHash(levelData.author, levelData.artist, levelData.song);
            if (PredictedDiffCache.TryGetValue(hash, out var diff)) return diff;
            var meta = await Task.Run(() => GetMeta(editor.customLevel.levelPath));
            Main.Logger.Log($"Difficulty Request Url: {meta.RequestUrl}");
            var predicted = await Task.Run(() => meta.Difficulty.ToDouble());
            return PredictedDiffCache[hash] = AdjustDiff(Clamp(Math.Round(predicted, 1), 1, 21));
        }
        public static LevelMeta GetMeta(string levelPath)
            => LevelMeta.GetMeta(ACL.Read(JsonNode.Parse(File.ReadAllText(levelPath))));
        public static double AdjustDiff(double diff)
        {
            double result;
            if (diff > 30)
                return 21;
            if (diff <= 20)
                if (diff > 18)
                    result = Math.Round(diff / .5) * .5;
                else result = Math.Round(diff);
            else result = Math.Round(20f + (diff % 20f / 10f), 1);
            return result;
        }
#endif
        public static List<string> CachedState = new List<string>();
        public static Dictionary<string, double> PredictedDiffCache = new Dictionary<string, double>();
        public static Dictionary<string, string[]> CachedStrings = new Dictionary<string, string[]>();
        public static string TrimEx(this string s)
        {
            var result = s.BreakRichTag().Trim();
            if (result.Contains("&") || result.Contains(",")) 
                return result.Replace(" ", "");
            return result;
        }
        public static double PowEx(double x, double y)
            => x.Pow(y);
        public static double Clamp(double value, double min, double max)
            => value > max ? max : value < min ? min : value;
    }
}

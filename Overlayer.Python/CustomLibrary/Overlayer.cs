using HarmonyLib;
using IronPython.Runtime;
using IronPython.Runtime.Types;
using Overlayer.Core;
using Overlayer.Core.Utils;
using Overlayer.Tags.Global;
using Overlayer.Tags.Lenient;
using Overlayer.Tags.Normal;
using Overlayer.Tags.Strict;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Overlayer.Python.CustomLibrary
{
    public class Ovlr
    {
        internal static string currentSource = null;
        internal static Harmony harmony = new Harmony("Overlayer_PY");
        public static object invokeTag(string name, object op)
        {
            var tag = TagManager.Dict[name];
            return tag.HasOption ? tag.FastInvokerOpt(op) : tag.FastInvoker();
        }
        public static int log(object obj)
        {
            Overlayer.Main.Logger.Log(obj.ToString());
            return 0;
        }
        public static bool prefix(string typeColonMethodName, Delegate func)
        {
#if !TOURNAMENT
            var target = AccessTools.Method(typeColonMethodName);
            var wrap = func.Wrap();
            if (wrap == null)
                return false;
            harmony.Patch(target, new HarmonyMethod(wrap));
            return true;
#else
            return false;
#endif
        }
        public static bool postfix(string typeColonMethodName, Delegate func)
        {
#if !TOURNAMENT
            var target = AccessTools.Method(typeColonMethodName);
            var wrap = func.Wrap();
            if (wrap == null)
                return false;
            harmony.Patch(target, postfix: new HarmonyMethod(wrap));
            return true;
#else
            return false;
#endif
        }
        public static int hit(Action func)
        {
            JSEngine.CustomLibrary.Ovlr.Hits.Add(func);
            return 0;
        }
        public static int init(Action func)
        {
            JSEngine.CustomLibrary.Ovlr.Inits.Add(func);
            return 0;
        }
        public static int openLevel(Action func)
        {
            JSEngine.CustomLibrary.Ovlr.OpenLevels.Add(func);
            return 0;
        }
        public static int sceneLoad(Action func)
        {
            JSEngine.CustomLibrary.Ovlr.SceneLoads.Add(func);
            return 0;
        }
        public static int update(Action func)
        {
            JSEngine.CustomLibrary.Ovlr.Updates.Add(func);
            return 0;
        }
        public static double calculatePP(double difficulty, int speed, double accuracy, int totalTiles)
        {
            return Tags.Global.Adofaigg.CalculatePlayPoint(difficulty, speed, accuracy, totalTiles);
        }
        public static object getGlobalVariable(string name)
        {
            return JSEngine.CustomLibrary.Ovlr.Variables.TryGetValue(name, out var value) ? value : null;
        }
        public static void setGlobalVariable(string name, object obj)
        {
            JSEngine.CustomLibrary.Ovlr.Variables[name] = obj;
        }
        public static string getCurDir() => Overlayer.Main.Mod.Path + "/Inits";
        public static string getModDir() => Overlayer.Main.Mod.Path;
        public static PythonType resolve(string clrType)
        {
            return DynamicHelpers.GetPythonTypeFromType(AccessTools.TypeByName(clrType));
        }
        public static void registerTag(string name, Func<object> func, bool notplaying)
        {
            registerOptTag(name, s => func(), notplaying);
        }
        public static void registerOptTag(string name, Func<string, object> func, bool notplaying)
        {
            Replacer.Tag tag = new Replacer().CreateTag(name);
            tag.SetGetter(func).Build();
            Overlayer.Main.AllTags.SetTag(name, tag);
            tag.SourcePath = currentSource;
            if (notplaying) Overlayer.Main.NotPlayingTags.SetTag(name, tag);
            Overlayer.Main.Recompile();
        }
        public static void unregisterTag(string name)
        {
            Overlayer.Main.AllTags.RemoveTag(name);
            Overlayer.Main.NotPlayingTags.RemoveTag(name);
            Overlayer.Main.Recompile();
        }
        #region Generated Tag Bindings
        public static string SHit() => SHitTags.Hit();
        public static double STE() => SHitTags.TE();
        public static double SVE() => SHitTags.VE();
        public static double SEP() => SHitTags.EP();
        public static double SP() => SHitTags.P();
        public static double SLP() => SHitTags.LP();
        public static double SVL() => SHitTags.VL();
        public static double STL() => SHitTags.TL();
        public static string NHit() => NHitTags.Hit();
        public static double NTE() => NHitTags.TE();
        public static double NVE() => NHitTags.VE();
        public static double NEP() => NHitTags.EP();
        public static double NP() => NHitTags.P();
        public static double NLP() => NHitTags.LP();
        public static double NVL() => NHitTags.VL();
        public static double NTL() => NHitTags.TL();
        public static string LHit() => LHitTags.Hit();
        public static double LTE() => LHitTags.TE();
        public static double LVE() => LHitTags.VE();
        public static double LEP() => LHitTags.EP();
        public static double LP() => LHitTags.P();
        public static double LLP() => LHitTags.LP();
        public static double LVL() => LHitTags.VL();
        public static double LTL() => LHitTags.TL();
        public static string CurHit() => CurHitTags.Hit();
        public static double CurTE() => CurHitTags.TE();
        public static double CurVE() => CurHitTags.VE();
        public static double CurEP() => CurHitTags.EP();
        public static double CurP() => CurHitTags.P();
        public static double CurLP() => CurHitTags.LP();
        public static double CurVL() => CurHitTags.VL();
        public static double CurTL() => CurHitTags.TL();
        public static string CurDifficulty() => CurHitTags.Difficulty();
        public static object Expression(string op) => Tags.Global.Expression.Expr(op);
        public static string KeyJudge(double op) => KeyJudgeTag.KJ(op);
        public static double CurKps() => KPS.GetKps();
        public static double Radius(double op) => Misc.Radius(op);
        public static double Pitch(double op) => Misc.Pitch(op);
        public static double EditorPitch(double op) => Misc.EditorPitch(op);
        public static double ShortcutPitch(double op) => Misc.ShortcutPitch(op);
        public static string TEHex() => Misc.TEHex();
        public static string VEHex() => Misc.VEHex();
        public static string EPHex() => Misc.EPHex();
        public static string PHex() => Misc.PHex();
        public static string LPHex() => Misc.LPHex();
        public static string VLHex() => Misc.VLHex();
        public static string TLHex() => Misc.TLHex();
        public static string MPHex() => Misc.MPHex();
        public static string FMHex() => Misc.FMHex();
        public static string FOHex() => Misc.FOHex();
        public static string Title() => Misc.Title();
        public static string Author() => Misc.Author();
        public static string Artist() => Misc.Artist();
        public static double StartTile() => Misc.StartTile();
        public static double IntegratedDifficulty(double op) => Misc.IntegratedDifficulty(op);
        public static double PredictedDifficulty(double op) => Misc.PredictedDifficulty(op);
        public static double ForumDifficulty(double op) => Misc.ForumDifficulty(op);
        public static double Accuracy(double op) => Misc.Accuracy(op);
        public static double Progress(double op) => Misc.Progress(op);
        public static double CheckPoint() => Misc.CheckPoint();
        public static double CurCheckPoint() => Misc.CurCheckPoint();
        public static double TotalCheckPoint() => Misc.TotalCheckPoints();
        public static double XAccuracy(double op) => Misc.XAccuracy(op);
        public static double FailCount() => Misc.FailCount();
        public static double MissCount() => Misc.MissCount();
        public static double Overloads() => Misc.Overloads();
        public static double CurBpm(double op) => Misc.CurBpm(op);
        public static double TileBpm(double op) => Misc.TileBpm(op);
        public static double RecKps(double op) => Misc.RecKps(op);
        public static double BestProgress(double op) => Misc.BestProgress(op);
        public static double LeastCheckPoint() => Misc.LeastCheckPoint();
        public static double StartProgress(double op) => Misc.StartProgress(op);
        public static double CurMinute() => Misc.CurMinute();
        public static double CurSecond() => Misc.CurSecond();
        public static double CurMilliSecond() => Misc.CurMilliSecond();
        public static double TotalMinute() => Misc.TotalMinute();
        public static double TotalSecond() => Misc.TotalSecond();
        public static double TotalMilliSecond() => Misc.TotalMilliSecond();
        public static double LeftTile() => Misc.LeftTile();
        public static double TotalTile() => Misc.TotalTile();
        public static double CurTile() => Misc.CurTile();
        public static double Attempts() => Misc.Attempts();
        public static double Year() => Misc.Year();
        public static double Month() => Misc.Month();
        public static double Day() => Misc.Day();
        public static double Hour() => Misc.Hour();
        public static double Minute() => Misc.Minute();
        public static double Second() => Misc.Second();
        public static double MilliSecond() => Misc.MilliSecond();
        public static double Multipress() => Misc.Multipress();
        public static double Combo() => Misc.Combo();
        public static double Fps(double op) => Misc.Fps(op);
        public static double FrameTime(double op) => Misc.FrameTime(op);
        public static double PlayTime(string op) => Misc.PlayTime(op);
        public static double ProcessorCount() => Performance.ProcessorCount_();
        public static double MemoryGBytes(double op) => Performance.MemoryGBytes_(op);
        public static double CpuUsage(double op) => Performance.CpuUsage_(op);
        public static double TotalCpuUsage(double op) => Performance.TotalCpuUsage_(op);
        public static double MemoryUsage(double op) => Performance.MemoryUsage_(op);
        public static double TotalMemoryUsage(double op) => Performance.TotalMemoryUsage_(op);
        public static double MemoryUsageGBytes(double op) => Performance.MemoryUsageGBytes_(op);
        public static double TotalMemoryUsageGBytes(double op) => Performance.TotalMemoryUsageGBytes_(op);
        public static double PlayPoint(double op) => Tags.Global.Adofaigg.PlayPointValue(op);
        public static double ProgressDeath(string op) => Tags.Global.ProgressDeath.GetDeaths(op);
        public static double Score() => Scores.Score();
        public static double LScore() => Scores.LScore();
        public static double NScore() => Scores.NScore();
        public static double SScore() => Scores.SScore();
        public static double Timing(double op) => Timings.Timing(op);
        public static double TimingAvg(double op) => Timings.TimingAvg(op);
        #endregion
    }
}

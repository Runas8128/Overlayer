using HarmonyLib;
using System;
using System.Collections.Generic;
using Overlayer.Scripting.JS;
using Overlayer.Core.Tags;
using Overlayer.Core;
using System.Reflection;
using IronPython.Runtime.Types;
using System.Linq;
using Jint.Native.Function;
using Jint.Native;
using Overlayer.Core.Utils;
using Jint.Runtime.Interop;
using TMPro.Examples;
using Jint;
using Jint.Pooling;

namespace Overlayer.Scripting
{
    public static class Api
    {
        static Dictionary<ScriptType, List<(ApiAttribute, MethodInfo)>> mcache = new Dictionary<ScriptType, List<(ApiAttribute, MethodInfo)>>()
        {
            [ScriptType.JavaScript] = new List<(ApiAttribute, MethodInfo)>(),
            [ScriptType.Python] = new List<(ApiAttribute, MethodInfo)>(),
        };
        static Dictionary<ScriptType, List<(ApiAttribute, Type)>> tcache = new Dictionary<ScriptType, List<(ApiAttribute, Type)>>()
        {
            [ScriptType.JavaScript] = new List<(ApiAttribute, Type)>(),
            [ScriptType.Python] = new List<(ApiAttribute, Type)>(),
        };
        static Dictionary<string, FIWrapper> jsFuncs = new Dictionary<string, FIWrapper>();
        static Dictionary<string, FastDelegateHandler> pyFuncs = new Dictionary<string, FastDelegateHandler>();
        static Api()
        {
            ScriptType[] types = new[] { ScriptType.JavaScript, ScriptType.Python };
            foreach (MethodInfo method in typeof(Api).GetMethods())
            {
                ApiAttribute attr = method.GetCustomAttribute<ApiAttribute>();
                if (attr == null) continue;
                for (int i = 0; i < types.Length; i++)
                    if ((attr.SupportScript & types[i]) != 0)
                        mcache[types[i]].Add((attr, method));
            }
            foreach (Type type in typeof(Api).GetNestedTypes())
            {
                ApiAttribute attr = type.GetCustomAttribute<ApiAttribute>();
                if (attr == null) continue;
                for (int i = 0; i < types.Length; i++)
                    if ((attr.SupportScript & types[i]) != 0)
                        tcache[types[i]].Add((attr, type));
            }
        }
        public static IEnumerable<MethodInfo> GetApiMethods(ScriptType type) => mcache[type].Select(t => t.Item2);
        public static IEnumerable<(ApiAttribute, MethodInfo)> GetApiMethodsWithAttr(ScriptType type) => mcache[type];
        public static IEnumerable<Type> GetApiTypes(ScriptType type)  => tcache[type].Select(t => t.Item2);
        public static IEnumerable<(ApiAttribute, Type)> GetApiTypesWithAttr(ScriptType type) => tcache[type];
        public static bool IsContains(ScriptType type, Type t) => tcache[type].FindIndex(tuple => tuple.Item2.FullName == t.FullName) >= 0;
        public static bool IsContains(ScriptType type, MethodInfo m) => mcache[type].FindIndex(tuple => tuple.Item2 == m) >= 0;
        public static TileInfo[] TileInfos = new TileInfo[0];
        public static Harmony harmony = new Harmony("Overlayer.Scripting.Api");
        public static Dictionary<string, object> Variables = new Dictionary<string, object>();
        public static List<string> RegisteredCustomTags = new List<string>();
        public static void Clear()
        {
            harmony.UnpatchAll(harmony.Id);
            Variables.Clear();
            foreach (var tag in RegisteredCustomTags)
                TagManager.RemoveTag(tag);
            RegisteredCustomTags.Clear();
            TextManager.Refresh();
        }
        public static TileInfo CaptureTile(double accuracy, double xAccuracy, int seqID, double timing, double timingAvg, double bpm, int hitMargin)
        {
            var info = new TileInfo(accuracy, xAccuracy, seqID, timing, timingAvg, bpm, hitMargin);
            ArrayUtils.Add(ref TileInfos, info);
            return info;
        }
        public static void ClearTileInfo()
        {
            TileInfos = new TileInfo[0];
        }
        [Api]
        public static void Log(object obj) => Main.Logger.Log(OverlayerDebug.Log(obj).ToString());
        [Api(SupportScript = ScriptType.JavaScript)]
        public static bool Prefix(string typeColonMethodName, JsValue patch)
        {
            if (!(patch is FunctionInstance func)) return false;
            var target = AccessTools.Method(typeColonMethodName);
            var wrap = func.Wrap(target, true);
            if (wrap == null)
                return false;
            harmony.Patch(target, new HarmonyMethod(wrap));
            return true;
        }
        [Api(SupportScript = ScriptType.JavaScript)]
        public static bool Postfix(string typeColonMethodName, JsValue patch)
        {
            if (!(patch is FunctionInstance func)) return false;
            var target = AccessTools.Method(typeColonMethodName);
            var wrap = func.Wrap(target, false);
            if (wrap == null)
                return false;
            harmony.Patch(target, postfix: new HarmonyMethod(wrap));
            return true;
        }
        //[Api(SupportScript = ScriptType.JavaScript)]
        //public static void GenerateProxy(string clrType) => JSUtils.BuildProxy(MiscUtils.TypeByName(clrType), Main.ScriptPath);
        [Api]
        public static object GetGlobalVariable(string name)
        {
            return Variables.TryGetValue(name, out var value) ? value : null;
        }
        [Api]
        public static void SetGlobalVariable(string name, object obj)
        {
            Variables[name] = obj;
        }
        [Api(SupportScript = ScriptType.JavaScript)]
        public static void RegisterTag(string name, JsValue tagFunc, bool notplaying)
        {
            var executor = $"Registered Tag \"{name}\" (NotPlaying:{notplaying})";
            OverlayerDebug.Begin(executor);
            Tag tag = new Tag(name);
            if (!(tagFunc is FunctionInstance func)) return;
            FIWrapper wrapper = new FIWrapper(func);
            if (func.FunctionDeclaration.Params.Select(n => n.AssociatedData.ToString()).Count() == 1)
                tag.SetGetter((string o) => wrapper.Call(o).ToString());
            else tag.SetGetter(new Func<string>(() => wrapper.Call().ToString()));
            tag.Build();
            TagManager.SetTag(tag, notplaying);
            OverlayerDebug.Disable();
            TextManager.Refresh();
            OverlayerDebug.Enable();
            RegisteredCustomTags.Add(name);
            OverlayerDebug.End();
            Main.Logger?.Log(executor);
        }
        [Api(SupportScript = ScriptType.Python)]
        public static void RegisterTagOpt(string name, Func<string, object> func, bool notplaying)
        {
            var executor = $"Registered Tag \"{name}\" (NotPlaying:{notplaying})";
            OverlayerDebug.Begin(executor);
            Tag tag = new Tag(name);
            tag.SetGetter(s => func(s).ToString());
            tag.Build();
            TagManager.SetTag(tag, notplaying);
            OverlayerDebug.Disable();
            TextManager.Refresh();
            OverlayerDebug.Enable();
            RegisteredCustomTags.Add(name);
            OverlayerDebug.End();
            Main.Logger?.Log(executor);
        }
        [Api(SupportScript = ScriptType.Python)]
        public static void RegisterTag(string name, Func<object> func, bool notplaying)
        {
            var executor = $"Registered Tag \"{name}\" (NotPlaying:{notplaying})";
            OverlayerDebug.Begin(executor);
            Tag tag = new Tag(name);
            tag.SetGetter(new Func<string>(() => func().ToString()));
            tag.Build();
            TagManager.SetTag(tag, notplaying);
            OverlayerDebug.Disable();
            TextManager.Refresh();
            OverlayerDebug.Enable();
            RegisteredCustomTags.Add(name);
            OverlayerDebug.End();
            Main.Logger?.Log(executor);
        }
        [Api]
        public static void UnregisterTag(string name)
        {
            TagManager.RemoveTag(name);
            TextManager.Refresh();
        }
        [Api(SupportScript = ScriptType.Python)]
        public static PythonType Resolve(string clrType)
        {
            return DynamicHelpers.GetPythonTypeFromType(MiscUtils.TypeByName(clrType));
        }
        [Api(SupportScript = ScriptType.JavaScript)]
        public static TypeReference Resolve(Engine engine, string clrType)
        {
            return TypeReference.CreateTypeReference(engine, MiscUtils.TypeByName(clrType));
        }
        [Api(SupportScript = ScriptType.Python)]
        public static float RoundFloat(double value, int digits = -1) => (float)value.Round(digits);
        [Api(SupportScript = ScriptType.Python)]
        public static string RoundFloatString(double value, int digits = -1) => digits < 0 ? value.ToString() : value.ToString($"F{digits}");
        [Api("TileInfo")]
        public class TileInfo
        {
            public double Accuracy;
            public double XAccuracy;
            public int SeqID;
            public double Timing;
            public double TimingAvg;
            public double Bpm;
            public int HitMargin;
            public TileInfo(double accuracy, double xAccuracy, int seqID, double timing, double timingAvg, double bpm, int hitMargin)
            {
                Accuracy = accuracy;
                XAccuracy = xAccuracy;
                SeqID = seqID;
                Timing = timing;
                TimingAvg = timingAvg;
                Bpm = bpm;
                HitMargin = hitMargin;
            }
        }
        [Api]
        public static TileInfo[] GetTileInfos() => TileInfos;
        [Api("Interop", SupportScript = ScriptType.JavaScript)]
        public class InteropJS
        {
            public static void ExportJSFunction(string name, JsValue func)
            {
                var fi = func as FunctionInstance;
                if (fi == null) return;
                jsFuncs[name] = new FIWrapper(fi);
            }
            public static JsValue InvokePyFunction(Engine engine, string name, object[] args)
            {
                if (pyFuncs.TryGetValue(name, out var handler))
                    return JsValue.FromObject(engine, handler(args));
                return JsValue.Null;
            }
        }
        [Api("Interop", SupportScript = ScriptType.Python)]
        public class InteropPy
        {
            public static void ExportPyFunction(string name, Delegate func)
            {
                pyFuncs[name] = MethodWrapperPool.Get(func);
            }
            public static object InvokeJSFunction(string name, object[] args)
            {
                if (jsFuncs.TryGetValue(name, out var wrapper))
                    return wrapper.Call(args);
                return null;
            }
        }
    }
}

using JSEngine;
using JSEngine.Library;
using System.Collections.Generic;
using System;
using Overlayer.Tags.Global;
using System.IO;
using HarmonyLib;
using Overlayer;
using Overlayer.Core;
using UnityEngine.Experimental.AI;
using UnityEngine;

namespace JSEngine.CustomLibrary
{
    public class Ovlr : ObjectInstance
    {
        internal static Harmony harmony = new Harmony("Overlayer_JS");
        public Ovlr(ScriptEngine engine) : base(engine) => PopulateFunctions();
        [JSFunction(Name = "log")]
        public static int Log(object obj)
        {
            Main.Logger.Log(obj.ToString());
            return 0;
        }
        public static List<Action> Hits = new List<Action>();
        public static List<Action> OpenLevels = new List<Action>();
        public static List<Action> SceneLoads = new List<Action>();
        public static List<Action> Inits = new List<Action>();
        public static List<Action> Updates = new List<Action>();
        public static Dictionary<string, object> Variables = new Dictionary<string, object>();
        [JSFunction(Name = "prefix")]
        public static bool Prefix(string typeColonMethodName, FunctionInstance func)
        {
#if !TOURNAMENT
            var target = AccessTools.Method(typeColonMethodName);
            var wrap = func.Wrap(target, true);
            if (wrap == null)
                return false;
            harmony.Patch(target, new HarmonyMethod(wrap));
            return true;
#else
            return false;
#endif
        }
        [JSFunction(Name = "postfix")]
        public static bool Postfix(string typeColonMethodName, FunctionInstance func)
        {
#if !TOURNAMENT
            var target = AccessTools.Method(typeColonMethodName);
            var wrap = func.Wrap(target, false);
            if (wrap == null)
                return false;
            harmony.Patch(target, postfix: new HarmonyMethod(wrap));
            return true;
#else
            return false;
#endif
        }
        [JSFunction(Name = "hit")]
        public static int Hit(FunctionInstance func)
        {
            Hits.Add(() => func.Call(func.Prototype == null ? Undefined.Value : func.Prototype));
            return 0;
        }
        [JSFunction(Name = "init")]
        public static int Init(FunctionInstance func)
        {
            Inits.Add(() => func.Call(func.Prototype == null ? Undefined.Value : func.Prototype));
            return 0;
        }
        [JSFunction(Name = "openLevel")]
        public static int OpenLevel(FunctionInstance func)
        {
            OpenLevels.Add(() => func.Call(func.Prototype == null ? Undefined.Value : func.Prototype));
            return 0;
        }
        [JSFunction(Name = "sceneLoad")]
        public static int SceneLoad(FunctionInstance func)
        {
            SceneLoads.Add(() => func.Call(func.Prototype == null ? Undefined.Value : func.Prototype));
            return 0;
        }
        [JSFunction(Name = "update")]
        public static int Update(FunctionInstance func)
        {
            Updates.Add(() => func.Call(func.Prototype == null ? Undefined.Value : func.Prototype));
            return 0;
        }
        [JSFunction(Name = "getPlanet", Flags = JSFunctionFlags.HasEngineParameter)]
        public static Planet GetPlanet(ScriptEngine engine, int pt)
        {
            return new PlanetConstructor(engine).Construct(pt);
        }
        [JSFunction(Name = "calculatePP")]
        public static double CalculatePP(double difficulty, int speed, double accuracy, int totalTiles)
        {
            return Adofaigg.CalculatePlayPoint(difficulty, speed, accuracy, totalTiles);
        }
        [JSFunction(Name = "getGlobalVariable")]
        public static object GetGlobalVariable(string name)
        {
            return Variables.TryGetValue(name, out var value) ? value : Undefined.Value;
        }
        [JSFunction(Name = "setGlobalVariable")]
        public static void SetGlobalVariable(string name, object obj)
        {
            Variables[name] = obj;
        }
        [JSFunction(Name = "getCurDir")]
        public static string GetCurDir() => Main.Mod.Path + "/Inits";
        [JSFunction(Name = "getModDir")]
        public static string GetModDir() => Main.Mod.Path;
        [JSFunction(Name = "RGBToHSV", Flags = JSFunctionFlags.HasEngineParameter)]
        public static HSV RGBToHSV(ScriptEngine engine, Color col)
        {
            UnityEngine.Color.RGBToHSV(col, out float h, out float s, out float v);
            return new HSVConstructor(engine).Construct(h, s, v);
        }
        [JSFunction(Name = "HSVToRGB", Flags = JSFunctionFlags.HasEngineParameter)]
        public static Color HSVToRGB(ScriptEngine engine, HSV hsv)
        {
            UnityEngine.Color col = hsv;
            return new ColorConstructor(engine).Construct(col.r, col.g, col.b, col.a);
        }
        [JSFunction(Name = "resolve", Flags = JSFunctionFlags.HasEngineParameter)]
        public static ObjectInstance Resolve(ScriptEngine engine, string clrType)
        {
            return ClrStaticTypeWrapper.FromCache(engine, AccessTools.TypeByName(clrType));
        }
        [JSFunction(Name = "generateProxy")]
        public static void GenerateProxy(string clrType)
        {
            Type t = AccessTools.TypeByName(clrType);
            JSUtils.BuildProxy(t, Main.CustomTagsPath);
            JSUtils.BuildProxy(t, Main.InitsPath);
        }
        [JSFunction(Name = "registerTag", Flags = JSFunctionFlags.HasEngineParameter)]
        public static void RegisterTag(ScriptEngine engine, string name, UserDefinedFunction func, bool notplaying)
        {
            Replacer tmp = new Replacer();
            Tag tag = new Tag(name);
            UDFWrapper wrapper = new UDFWrapper(func);
            if (func.ArgumentNames.Count == 1)
                tag.SetGetter((string o) => wrapper.CallGlobal(o));
            else tag.SetGetter(() => wrapper.CallGlobal());
            tag.Build();
            Main.AllTags.SetTag(name, tag);
            tag.SourcePath = engine.Source.Path;
            if (notplaying) Main.NotPlayingTags.SetTag(name, tag);
            Main.Recompile();
        }
        [JSFunction(Name = "unregisterTag")]
        public static void UnregisterTag(string name)
        {
            Main.AllTags.RemoveTag(name);
            Main.NotPlayingTags.RemoveTag(name);
            Main.Recompile();
        }
        public static List<TileData> tiles = new List<TileData>();
    }
}

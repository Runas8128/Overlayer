using HarmonyLib;
using IronPython.Runtime.Types;
using Overlayer.Core;
using Overlayer.Core.Utils;
using Overlayer.Tags.Global;
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
            return PlayPoint.CalculatePlayPoint(difficulty, speed, accuracy, totalTiles);
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
        public static void registerTag(string name, Delegate func, bool notplaying)
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
    }
}

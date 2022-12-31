using HarmonyLib;
using Overlayer.Core;
using Overlayer.Tags.Global;

namespace Overlayer.Patches
{
    [HarmonyPatch(typeof(scrController), "FailAction")]
    public static class DeathMessagePatch
    {
        public static Replacer compiler;
        public static void Prefix(scrController __instance)
        {
            ProgressDeath.Increment(__instance.percentComplete * 100);
        }
        public static void Postfix(scrController __instance)
        {
            if (!__instance.noFail)
                __instance.txtTryCalibrating.text = compiler.Replace();
        }
    }
}

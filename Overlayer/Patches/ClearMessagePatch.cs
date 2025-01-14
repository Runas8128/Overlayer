﻿using HarmonyLib;
using Overlayer.Core;
using Overlayer.Tags.Global;

namespace Overlayer.Patches
{
    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    public static class ClearMessagePatch
    {
        public static Replacer compiler;
        public static void Postfix(scrController __instance)
        {
            if (__instance.gameworld && !__instance.noFail && !string.IsNullOrEmpty(compiler.Source))
                __instance.txtCongrats.text = compiler.Replace();
        }
    }
}

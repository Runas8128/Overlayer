using HarmonyLib;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine;
using Overlayer.Core;
using TMPro;
using System.Collections.Generic;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore;

namespace Overlayer.Patches
{
    public static class FontPatch
    {
        [HarmonyPatch(typeof(RDString), "GetFontDataForLanguage")]
        public static class ChangeFontPatch
        {
            public static bool Prefix(ref FontData __result)
            {
                if (!Settings.Instance.ChangeFont) 
                    return true;
                if (!FontManager.TryGetFont(Settings.Instance.AdofaiFont, out var font))
                    return true;
                if (!font.font.dynamic)
                    return true;
                __result = font;
                return false;
            }
        }
    }
}

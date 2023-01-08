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
using UnityEngine.UIElements;

namespace Overlayer.Patches
{
    public static class FontPatch
    {
        [HarmonyPatch(typeof(RDString), "GetFontDataForLanguage")]
        public static class ChangeFontPatch
        {
            public static bool Prefix(ref FontData __result)
            {
                if (!FontManager.Initialized)
                    return true;
                if (!Settings.Instance.ChangeFont)
                    return true;
                if (!FontManager.TryGetFont(Settings.Instance.AdofaiFont, out var font))
                    return true;
                if (!(font.font?.dynamic ?? false)) 
                    return true;
                __result = font;
                return false;
            }
        }

        [HarmonyPatch(typeof(scrController), "Update")]
        public static class FontAttacher
        {
            public static void Postfix(scrController __instance)
            {
                __instance.StartCoroutine(UpdateFontCo());
            }
            static IEnumerator UpdateFontCo()
            {
                if (!Settings.Instance.ChangeFont) yield break;
                List<GameObject> list = new List<GameObject>();
                try { Main.activeScene.GetRootGameObjects(list); }
                catch { yield break; }
                foreach (var i in list)
                {
                    foreach (var j in i.GetComponentsInChildren<Text>())
                        j.SetLocalizedFont();
                    yield return null;
                }
            }
        }
    }
}

using HarmonyLib;
using System;
using Overlayer.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Security.Permissions;
using System.Reflection;

namespace Overlayer.Patches
{
    [HarmonyPatch]
    public static class PlaytimeCounter
    {
        public static Dictionary<string, float> PlayTimes = new Dictionary<string, float>();
        public static string MapID = string.Empty;
        public static string ID(string id) => id + "_PlayTime";
        [HarmonyPatch(typeof(scrController), "Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(scrController __instance)
        {
            if (__instance.gameworld && __instance.state == States.PlayerControl)
                if (PlayTimes.TryGetValue(ID(MapID), out _))
                    Variables.PlayTime = PlayTimes[ID(MapID)] += Time.deltaTime;
                else Variables.PlayTime = 0;
        }
        [HarmonyPatch]
        public static class SetIDPatch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(scrController), "Awake_Rewind");
                yield return AccessTools.Method(typeof(scnEditor), "Play");
            }
            public static void Postfix()
            {
                if (ADOBase.sceneName.Contains("-"))
                {
                    if (!PlayTimes.TryGetValue(ID(ADOBase.sceneName), out _))
                        PlayTimes[ID(ADOBase.sceneName)] = Persistence.generalPrefs.GetFloat(ID(ADOBase.sceneName));
                    Variables.PlayTime = PlayTimes[ID(ADOBase.sceneName)];
                }
                else
                {
                    if (scnEditor.instance?.levelData == null)
                        return;
                    Main.Logger.Log("Hash");
                    var levelData = scnEditor.instance.levelData;
                    MapID = DataInit.MakeHash(levelData.author, levelData.artist, levelData.song);
                    if (!PlayTimes.TryGetValue(ID(MapID), out _))
                        PlayTimes[ID(MapID)] = Persistence.generalPrefs.GetFloat(ID(MapID));
                    Variables.PlayTime = PlayTimes[ID(MapID)];
                }
            }
        }
    }
}

using HarmonyLib;
using System;
using Overlayer.Tags;
using Overlayer.Scripting;

namespace Overlayer.Patches
{
    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    public static class TimingUpdater
    {
        public static void Prefix(scrPlanet __instance)
        {
            if (OverlayerText.IsPlaying)
            {
                Variables.Timing = (__instance.angle - __instance.targetExitAngle) * (scrController.instance.isCW ? 1.0 : -1.0) * 60000.0 / (Math.PI * __instance.conductor.bpm * scrController.instance.speed * __instance.conductor.song.pitch);
                BpmUpdater.TimingList.Add(Variables.Timing);
            }
            else Variables.Timing = 0;
        }
        public static void Postfix()
        {
            if (scrController.instance.gameworld)
                Api.CaptureTile(Misc.Accuracy(), Misc.XAccuracy(), Variables.CurrentTile, Variables.Timing, BpmUpdater.TimingAvg(), Variables.TileBpm, (int)CurHitTags.GetCurHitMargin(GCS.difficulty));
        }
    }
}

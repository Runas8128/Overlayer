﻿using HarmonyEx;
using System;
using Overlayer.Tags;
using System.Collections.Generic;
using Overlayer.Core.Tags;

namespace Overlayer.Patches
{
    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    public static class TimingUpdater
    {
        public static void Prefix(scrPlanet __instance)
        {
            if (OverlayerText.IsPlaying)
            {
                Timing = (__instance.angle - __instance.targetExitAngle) * (scrController.instance.isCW ? 1.0 : -1.0) * 60000.0 / (Math.PI * __instance.conductor.bpm * scrController.instance.speed * __instance.conductor.song.pitch);
                BpmUpdater.TimingList.Add(Timing);
                if (!scrController.instance.noFail)
                    Variables.BestProg = Math.Max(Variables.BestProg, scrController.instance.percentComplete * 100);
            }
            else Timing = 0;
        }
    }
}

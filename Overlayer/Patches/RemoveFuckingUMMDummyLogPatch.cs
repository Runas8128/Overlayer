using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

namespace Overlayer.Patches
{
    [HarmonyPatch(typeof(UnityModManager.Logger), "Log", new[] { typeof(string) })]
    public static class RemoveFuckingUMMDummyLogPatch
    {
        public static bool Prefix(string str)
        {
            if (str == "Cancel start. Already started.")
                return false;
            return true;
        }
    }
}

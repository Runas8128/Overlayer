using HarmonyLib;
using static UnityModManagerNet.UnityModManager;

namespace Overlayer.Patches
{
    [HarmonyPatch(typeof(UI), "Start")]
    public static class DisplayExceptionMessagePatch
    {
        public static AccessTools.FieldRef<UI, int> guiIndex = AccessTools.FieldRefAccess<UI, int>(AccessTools.Field(typeof(UI), "mShowModSettings"));
        public static void Postfix(UI __instance)
        {
            if (Main.ErrorString != null)
                guiIndex(__instance) = Main.OverlayerEntryIndex;
        }
    }
}

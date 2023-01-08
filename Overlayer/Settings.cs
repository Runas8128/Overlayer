﻿using UnityModManagerNet;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;
using Overlayer.Core.Utils;
using TMPro.Examples;
using Overlayer.Core;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
#pragma warning disable

namespace Overlayer
{
    public class Settings : UnityModManager.ModSettings
    {
        public static void Load(UnityModManager.ModEntry modEntry)
            => Instance = Load<Settings>(modEntry);
        public static void Save(UnityModManager.ModEntry modEntry)
            => Save(Instance, modEntry);
        public static Settings Instance;

        public bool Reset = true;
        public int KPSUpdateRate = 20;
        public int FPSUpdateRate = 500;
        public int PerfStatUpdateRate = 500;
        public int FrameTimeUpdateRate = 500;
        public bool AddAllJudgementsAtErrorMeter = true;
        public bool ApplyPitchAtBpmTags = true;
        public string AdofaiFont = "";
        public void DrawManual()
        {
            Reset = GUIUtils.RightToggle(Reset, Main.Language["ResetOnStart"]);
            KPSUpdateRate = GUIUtils.SpaceIntField(KPSUpdateRate, Main.Language["KPSUpdateRate"]);
            FPSUpdateRate = GUIUtils.SpaceIntField(FPSUpdateRate, Main.Language["FPSUpdateRate"]);
            PerfStatUpdateRate = GUIUtils.SpaceIntField(PerfStatUpdateRate, Main.Language["PerfStatUpdateRate"]);
            FrameTimeUpdateRate = GUIUtils.SpaceIntField(FrameTimeUpdateRate, Main.Language["FrameTimeUpdateRate"]);
            AddAllJudgementsAtErrorMeter = GUIUtils.RightToggle(AddAllJudgementsAtErrorMeter, Main.Language["AddAllJudgementsAtErrorMeter"]);
            ApplyPitchAtBpmTags = GUIUtils.RightToggle(ApplyPitchAtBpmTags, Main.Language["ApplyPitchAtBpmTags"]);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Adofai Font");
            AdofaiFont = GUILayout.TextField(AdofaiFont);
            if (GUILayout.Button("Apply"))
            {
                RDString.initialized = false;
                RDString.Setup();
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        public bool ChangeFont => !string.IsNullOrEmpty(AdofaiFont);
        public SystemLanguage lang = SystemLanguage.English;
        public string DeathMessage = "";
        public string ClearMessage = "";
    }
}

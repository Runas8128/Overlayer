using UnityModManagerNet;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;
using Overlayer.Core.Utils;
using TMPro.Examples;
using Overlayer.Core;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Overlayer.Core.Translation;
#pragma warning disable

namespace Overlayer
{
    public class FontMeta
    {
        public string name;
        public float lineSpacing = 1;
        public float fontScale = 0.5f;
        public bool Apply(out FontData font)
        {
            if (FontManager.TryGetFont(name, out font))
            {
                font.lineSpacing = lineSpacing;
                font.lineSpacingTMP = lineSpacing;
                font.fontScale = fontScale;
                return true;
            }
            return false;
        }
    }
    public class Settings : UnityModManager.ModSettings
    {
        public static void Load(UnityModManager.ModEntry modEntry)
        {
            Instance = Load<Settings>(modEntry);
            if (Instance.AdofaiFont.Apply(out FontData font))
                FontManager.SetFont(Instance.AdofaiFont.name, font);
        }
        public static void Save(UnityModManager.ModEntry modEntry)
            => Save(Instance, modEntry);
        public static Settings Instance;
        public bool CollectLevels = true;
        public bool Reset = true;
        public int KPSUpdateRate = 20;
        public int FPSUpdateRate = 500;
        public int PerfStatUpdateRate = 500;
        public int FrameTimeUpdateRate = 500;
        public bool AddAllJudgementsAtErrorMeter = true;
        public bool ApplyPitchAtBpmTags = true;
        public bool ChangeFont = false;
        public FontMeta AdofaiFont = new FontMeta();
        public void DrawManual()
        {
            Reset = GUIUtils.RightToggle(Reset, Main.Language["ResetOnStart"]);
            KPSUpdateRate = GUIUtils.SpaceIntField(KPSUpdateRate, Main.Language["KPSUpdateRate"]);
            FPSUpdateRate = GUIUtils.SpaceIntField(FPSUpdateRate, Main.Language["FPSUpdateRate"]);
            PerfStatUpdateRate = GUIUtils.SpaceIntField(PerfStatUpdateRate, Main.Language["PerfStatUpdateRate"]);
            FrameTimeUpdateRate = GUIUtils.SpaceIntField(FrameTimeUpdateRate, Main.Language["FrameTimeUpdateRate"]);
            AddAllJudgementsAtErrorMeter = GUIUtils.RightToggle(AddAllJudgementsAtErrorMeter, Main.Language["AddAllJudgementsAtErrorMeter"]);
            ApplyPitchAtBpmTags = GUIUtils.RightToggle(ApplyPitchAtBpmTags, Main.Language["ApplyPitchAtBpmTags"]);

            if (ChangeFont = GUILayout.Toggle(ChangeFont, Main.Language[TranslationKeys.AdofaiFont]))
                GUIUtils.IndentGUI(() =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Main.Language[TranslationKeys.AdofaiFont_Font]);
                    AdofaiFont.name = GUILayout.TextField(AdofaiFont.name);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Main.Language[TranslationKeys.AdofaiFont_FontScale]);
                    string scale = GUILayout.TextField(AdofaiFont.fontScale.ToString());
                    _ = float.TryParse(scale, out float s) ? AdofaiFont.fontScale = s : 0;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Main.Language[TranslationKeys.AdofaiFont_LineSpacing]);
                    string lineSpacing = GUILayout.TextField(AdofaiFont.lineSpacing.ToString());
                    _ = float.TryParse(lineSpacing, out float ls) ? AdofaiFont.lineSpacing = ls : 0;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(Main.Language[TranslationKeys.Apply]))
                    {
                        if (AdofaiFont.Apply(out FontData font))
                        {
                            FontManager.SetFont(AdofaiFont.name, font);
                            RDString.initialized = false;
                            RDString.Setup();
                            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        }
                        else Main.Logger.Log($"Font Name '{AdofaiFont.name}' Does Not Exist.");
                    }
                    if (GUILayout.Button(Main.Language[TranslationKeys.LogFontList]))
                        foreach (string font in FontManager.OSFonts)
                            Main.Logger.Log(font);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                });
        }
        public SystemLanguage lang = SystemLanguage.English;
        public string DeathMessage = "";
        public string ClearMessage = "";
    }
}

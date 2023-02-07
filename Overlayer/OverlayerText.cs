using Overlayer.Core;
using Overlayer.Core.Utils;
using Overlayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TinyJson;
using TMPro;
using UnityEngine;
using UnityModManagerNet;
using Overlayer.Core.Translation;

namespace Overlayer
{
    public class OverlayerText
    {
        public class Setting
        {
            public Setting() => InitValues();
            public void InitValues()
            {
                Position = new float[2] { 0.011628f, 1 };
                Color = new float[4] { 1, 1, 1, 1 };
                ShadowColor = new float[4] { 0, 0, 0, 0.5f };
                NotPlayingText = Main.Language[TranslationKeys.NotPlaying];
                PlayingText = "<color=#{FOHex}>{Overloads}</color> <color=#{TEHex}>{CurTE}</color> <color=#{VEHex}>{CurVE}</color> <color=#{EPHex}>{CurEP}</color> <color=#{PHex}>{CurP}</color> <color=#{LPHex}>{CurLP}</color> <color=#{VLHex}>{CurVL}</color> <color=#{TLHex}>{CurTL}</color> <color=#{FMHex}>{MissCount}</color>";
                FontSize = 44;
                IsExpanded = true;
                Alignment = TextAlignmentOptions.Left;
                Font = "Default";
                Active = true;
                GradientText = false;
                Gradient = new float[4][] { new float[4] { 1, 1, 1, 1 }, new float[4] { 1, 1, 1, 1 }, new float[4] { 1, 1, 1, 1 }, new float[4] { 1, 1, 1, 1 } };
            }
            public string Name;
            public float[] Position;
            public float[] Color;
            public int FontSize;
            public string NotPlayingText;
            public string PlayingText;
            public bool IsExpanded;
            public float[] ShadowColor;
            public string Font;
            public bool Active;
            public TextAlignmentOptions Alignment;
            public bool GradientText;
            public float[][] Gradient;
            public void ValidCheck()
            {
                if (Position == null)
                    Position = new float[2] { 0.011628f, 1 };
                if (Color == null)
                    Color = new float[4] { 1, 1, 1, 1 };
                if (ShadowColor == null)
                    ShadowColor = new float[4] { 0, 0, 0, 0.5f };
                if (NotPlayingText == null)
                    NotPlayingText = Main.Language[TranslationKeys.NotPlaying];
                if (PlayingText == null)
                    PlayingText = "<color=#{FOHex}>{Overloads}</color> <color=#{TEHex}>{CurTE}</color> <color=#{VEHex}>{CurVE}</color> <color=#{EPHex}>{CurEP}</color> <color=#{PHex}>{CurP}</color> <color=#{LPHex}>{CurLP}</color> <color=#{VLHex}>{CurVL}</color> <color=#{TLHex}>{CurTL}</color> <color=#{FMHex}>{MissCount}</color>";
                if (FontSize == 0)
                    FontSize = 44;
                if (Font == null)
                    Font = "Default";
                if (Gradient == null)
                    Gradient = new float[4][] { new float[4] { 1, 1, 1, 1 }, new float[4] { 1, 1, 1, 1 }, new float[4] { 1, 1, 1, 1 }, new float[4] { 1, 1, 1, 1 } };
            }
        }
        public static TextGroup Global = new TextGroup(true);
        public static List<TextGroup> Groups = new List<TextGroup>();
        public static void Load()
        {
            Global.Load(GlobalTextsPath);
            foreach (string file in Directory.GetFiles(Main.Mod.Path, "_Group.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var groupName = fileName.Split('_')[0];
                var group = new TextGroup(false);
                group.Name = groupName;
                group.Load(file);
                Groups.Add(group);
            }
        }
        public static void Clear()
        {
            Global.Clear();
            Groups.ForEach(g => g.Clear());
        }
        public static void Save()
        {
            Global.Save(GlobalTextsPath);
            Groups.ForEach(g => g.Save());
        }
        public static bool IsPlaying
        {
            get
            {
                var ctrl = scrController.instance;
                var cdt = scrConductor.instance;
                if (ctrl != null && cdt != null)
                    return !ctrl.paused && cdt.isGameWorld;
                return false;
            }
        }
        public static readonly string GlobalTextsPath = Path.Combine(Main.Mod.Path, "Texts.json");
        public TextGroup group;
        public OverlayerText(TextGroup group, Setting setting = null)
        {
            this.group = group;
            SText = ShadowText.NewText();
            UnityEngine.Object.DontDestroyOnLoad(SText.gameObject);
            TSetting = setting ?? new Setting();
            Number = SText.Number;
            TSetting.ValidCheck();
            if (TSetting.Name == null)
                TSetting.Name = $"{Main.Language[TranslationKeys.Text]} {Number}";
            PlayingCompiler = new Replacer(TSetting.PlayingText, Main.AllTags);
            NotPlayingCompiler = new Replacer(TSetting.NotPlayingText, Main.NotPlayingTags);
            BrokenPlayingCompiler = new Replacer(TSetting.PlayingText.BreakRichTagWithoutSize(), Main.AllTags);
            BrokenNotPlayingCompiler = new Replacer(TSetting.NotPlayingText.BreakRichTagWithoutSize(), Main.NotPlayingTags);
            SText.Updater = () =>
            {
                if (IsPlaying)
                {
                    SText.Main.text = PlayingCompiler.Replace();
                    SText.Shadow.text = BrokenPlayingCompiler.Replace();
                }
                else
                {
                    SText.Main.text = NotPlayingCompiler.Replace();
                    SText.Shadow.text = BrokenNotPlayingCompiler.Replace();
                }
            };
            SText.gameObject.SetActive(TSetting.Active);
        }
        public void GUI()
        {
            GUILayout.BeginHorizontal();
            TSetting.IsExpanded = GUILayout.Toggle(TSetting.IsExpanded, "");
            if (TSetting.IsExpanded)
                TSetting.Name = GUILayout.TextField(TSetting.Name);
            else GUILayout.Label(TSetting.Name);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (TSetting.IsExpanded)
            {
                GUILayout.BeginVertical();
                GUIUtils.IndentGUI(() =>
                {
                    var active = GUILayout.Toggle(TSetting.Active, Main.Language[TranslationKeys.Active]);
                    if (active != TSetting.Active)
                        SText.Active = TSetting.Active = active;
                    GUILayout.BeginVertical();

                    GUILayout.BeginHorizontal();
                    if (UnityModManager.UI.DrawFloatField(ref TSetting.Position[0], Main.Language[TranslationKeys.TextXPos])) Apply();
                    TSetting.Position[0] = GUILayout.HorizontalSlider(TSetting.Position[0], 0, 1);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (UnityModManager.UI.DrawFloatField(ref TSetting.Position[1], Main.Language[TranslationKeys.TextYPos])) Apply();
                    TSetting.Position[1] = GUILayout.HorizontalSlider(TSetting.Position[1], 0, 1);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{Main.Language[TranslationKeys.TextAlignment]}:");
                    if (GUIUtils.DrawEnum($"{Main.Language[TranslationKeys.Text]} {Number} {Main.Language[TranslationKeys.Alignment]}", ref TSetting.Alignment)) Apply();
                    if (GUILayout.Button(Main.Language[TranslationKeys.Reset]))
                    {
                        TSetting.Alignment = TextAlignmentOptions.Left;
                        Apply();
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (UnityModManager.UI.DrawIntField(ref TSetting.FontSize, Main.Language[TranslationKeys.TextSize])) Apply();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUIUtils.DrawTextField(ref TSetting.Font, Main.Language[TranslationKeys.TextFont])) Apply();
                    if (GUILayout.Button(Main.Language[TranslationKeys.LogFontList]))
                        foreach (string font in FontManager.OSFonts)
                            Main.Logger.Log(font);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Main.Language[TranslationKeys.TextColor]);
                    GUILayout.Space(1);
                    if (GUIUtils.DrawColor(ref TSetting.Color)) Apply();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Main.Language[TranslationKeys.ShadowColor]);
                    GUILayout.Space(1);
                    if (GUIUtils.DrawColor(ref TSetting.ShadowColor)) Apply();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    bool newGradientText = GUILayout.Toggle(TSetting.GradientText, Main.Language[TranslationKeys.Gradient]);
                    if (newGradientText)
                    {
                        GUIUtils.IndentGUI(() =>
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(Main.Language[TranslationKeys.TopLeft]);
                            GUILayout.Space(1);
                            if (GUIUtils.DrawColor(ref TSetting.Gradient[0])) Apply();
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label(Main.Language[TranslationKeys.TopRight]);
                            GUILayout.Space(1);
                            if (GUIUtils.DrawColor(ref TSetting.Gradient[1])) Apply();
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label(Main.Language[TranslationKeys.BottomLeft]);
                            GUILayout.Space(1);
                            if (GUIUtils.DrawColor(ref TSetting.Gradient[2])) Apply();
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label(Main.Language[TranslationKeys.BottomRight]);
                            GUILayout.Space(1);
                            if (GUIUtils.DrawColor(ref TSetting.Gradient[3])) Apply();
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        });
                    }
                    if (newGradientText != TSetting.GradientText)
                    {
                        TSetting.GradientText = newGradientText;
                        Apply();
                    }
                    GUILayout.BeginHorizontal();
                    if (GUIUtils.DrawTextArea(ref TSetting.PlayingText, Main.Language[TranslationKeys.Text])) Apply();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUIUtils.DrawTextArea(ref TSetting.NotPlayingText, Main.Language[TranslationKeys.TextDisplayedWhenNotPlaying])) Apply();
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(Main.Language[TranslationKeys.Refresh])) Apply();
                    if (GUILayout.Button(Main.Language[TranslationKeys.Reset]))
                    {
                        TSetting = new Setting();
                        Apply();
                    }
                    if (ShadowText.Count > 1 && GUILayout.Button(Main.Language[TranslationKeys.Destroy]))
                    {
                        UnityEngine.Object.Destroy(SText.gameObject);
                        group.Remove(this);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                });
                GUILayout.EndVertical();
            }
        }
        public OverlayerText Apply()
        {
            SText.TrySetFont(TSetting.Font);
            if (TSetting.GradientText)
            {
                Color col1 = new Color(TSetting.Gradient[0][0], TSetting.Gradient[0][1], TSetting.Gradient[0][2], TSetting.Gradient[0][3]);
                Color col2 = new Color(TSetting.Gradient[1][0], TSetting.Gradient[1][1], TSetting.Gradient[1][2], TSetting.Gradient[1][3]);
                Color col3 = new Color(TSetting.Gradient[2][0], TSetting.Gradient[2][1], TSetting.Gradient[2][2], TSetting.Gradient[2][3]);
                Color col4 = new Color(TSetting.Gradient[3][0], TSetting.Gradient[3][1], TSetting.Gradient[3][2], TSetting.Gradient[3][3]);
                SText.Main.colorGradient = new VertexGradient(col1, col2, col3, col4);
            }
            else SText.Main.colorGradient = new VertexGradient(new Color(TSetting.Color[0], TSetting.Color[1], TSetting.Color[2], TSetting.Color[3]));
            Vector2 pos = new Vector2(TSetting.Position[0], TSetting.Position[1]);
            SText.Center = pos;
            SText.Position = pos;
            SText.FontSize = TSetting.FontSize;
            SText.Alignment = TSetting.Alignment;
            SText.Shadow.color = TSetting.ShadowColor.ToColor();
            Tags.Global.ProgressDeath.Reset();
            PlayingCompiler.Source = TSetting.PlayingText;
            NotPlayingCompiler.Source = TSetting.NotPlayingText;
            BrokenPlayingCompiler.Source = TSetting.PlayingText.BreakRichTagWithoutSize();
            BrokenNotPlayingCompiler.Source = TSetting.NotPlayingText.BreakRichTagWithoutSize();
            return this;
        }
        public Replacer PlayingCompiler;
        public Replacer NotPlayingCompiler;
        public Replacer BrokenPlayingCompiler;
        public Replacer BrokenNotPlayingCompiler;
        public readonly ShadowText SText;
        public Setting TSetting;
        public int Number;
    }
}

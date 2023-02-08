using Overlayer.Core;
using Overlayer.Core.Utils;
using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyJson;
using UnityEngine;
using Setting = Overlayer.OverlayerText.Setting;
using static UnityModManagerNet.UnityModManager.UI;
using Overlayer.Core.Translation;
using HarmonyLib;

namespace Overlayer
{
    public class TextGroup
    {
        static bool globalExists = false;
        readonly bool isGlobal = false;
        internal List<Replacer.Tag> references = new List<Replacer.Tag>();
        private Vector2 relativePos = new Vector2();
        private float relativeSize = 0;
        public TextGroup(bool isGlobal = false)
        {
            Name = string.Empty;
            Texts = new List<OverlayerText>();
            if (globalExists && isGlobal)
                throw new InvalidOperationException("Global Text Group Cannot Be 2!");
            globalExists |= this.isGlobal = isGlobal;
        }
        public string Name { get; set; }
        public List<OverlayerText> Texts { get; set; }
        public bool Expanded = false;
        public Vector2 Position
        {
            get => relativePos;
            set
            {
                relativePos = value;
                Texts.ForEach(t => t.SText.Position += relativePos);
            }
        }
        public float Size
        {
            get => relativeSize;
            set
            {
                relativeSize = value;
                Texts.ForEach(t => t.SText.FontSize += relativeSize);
            }
        }
        public void GUI()
        {
            if (isGlobal)
                for (int i = 0; i < Texts.Count; i++)
                    Texts[i].GUI();
            else
            {
                GUILayout.BeginHorizontal();
                if (Expanded)
                {
                    Expanded = GUILayout.Toggle(Expanded, "");
                    Name = GUILayout.TextField(Name);
                }
                else Expanded = GUILayout.Toggle(Expanded, Name);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (Expanded)
                {
                    GUIUtils.IndentGUI(() =>
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(Main.Language[TranslationKeys.AddText]))
                            Add(new OverlayerText.Setting());
                        if (GUILayout.Button("Export Group"))
                        {
                            var result = StandaloneFileBrowser.OpenFolderPanel("Export Group", Persistence.GetLastUsedFolder(), true);
                            if (result.Length > 0) Array.ForEach(result, Save);
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Position");
                        if (DrawVector(ref relativePos))
                            Position = relativePos;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if (DrawFloatField(ref relativeSize, "Size"))
                            Size = relativeSize;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        for (int i = 0; i < Texts.Count; i++)
                            Texts[i].GUI();
                    });
                }
            }
        }
        public void Load(string path)
        {
            if (!File.Exists(path))
            {
                Texts = new List<OverlayerText>();
                return;
            }
            var json = File.ReadAllText(path);
            try
            {
                var pkg = json.FromJson<TextPackage>();
                var group = TextPackage.Unpack(pkg);
                Texts = group.Texts;
                Name = group.Name;
                Expanded = group.Expanded;
                relativePos = group.relativePos;
                relativeSize = group.relativeSize;
                Texts.ForEach(t => t.SText.FontSize += relativeSize);
                Texts.ForEach(t => t.SText.Position += relativePos);
            }
            catch
            {
                var texts = json.FromJson<List<Setting>>();
                Texts = texts.Select(s => new OverlayerText(this, s).Apply()).ToList();
            }
            Order();
        }
        public void Save(string path = null)
        {
            var json = TextPackage.Pack(this).ToJson();
            if (isGlobal)
                File.WriteAllText(OverlayerText.GlobalTextsPath, json);
            else
            {
                var name = $"{Name}_Group.txtgrp";
                path = path ?? Main.Mod.Path;
                File.WriteAllText(Path.Combine(path, name), json);
            }
        }
        public void Add(Setting setting)
        {
            Texts.Add(new OverlayerText(this, setting).Apply());
            Order();
        }
        public void Move(OverlayerText text, TextGroup group)
        {
            Remove(text);
            Setting newSetting = new Setting();
            newSetting.Active = text.TSetting.Active;
            newSetting.Alignment = text.TSetting.Alignment;
            newSetting.FontSize = text.TSetting.FontSize;
            newSetting.Gradient = text.TSetting.Gradient.Select(arr => arr.ToArray()).ToArray();
            newSetting.Font = text.TSetting.Font;
            newSetting.Name = text.TSetting.Name;
            newSetting.Color = text.TSetting.Color.ToArray();
            newSetting.GradientText = text.TSetting.GradientText;
            newSetting.IsExpanded = text.TSetting.IsExpanded;
            newSetting.ShadowColor = text.TSetting.ShadowColor.ToArray();
            newSetting.Position = text.TSetting.Position.ToArray();
            newSetting.PlayingText = text.TSetting.PlayingText;
            newSetting.NotPlayingText = text.TSetting.NotPlayingText;
            group.Add(newSetting);
            Order();
        }
        public void Remove(OverlayerText text)
        {
            int index = Texts.IndexOf(text);
            Texts.RemoveAt(index);
            text.PlayingCompiler = null;
            text.NotPlayingCompiler = null;
            UnityEngine.Object.Destroy(text.SText.Main.gameObject);
            UnityEngine.Object.Destroy(text.SText.Shadow.gameObject);
            for (int i = index; i < Texts.Count; i++)
            {
                var txt = Texts[i];
                txt.Number--;
                txt.SText.Number--;
            }
            ShadowText.Count--;
            GC.SuppressFinalize(text);
            Order();
        }
        public void Order()
        {
            Texts = Texts.OrderBy(o => o.Number).ToList();
            TagAnalyze();
        }
        public void Clear()
        {
            foreach (var text in Texts)
                Remove(text);
            UnityEngine.Object.Destroy(ShadowText.PublicCanvas.gameObject);
            ShadowText.Count = 0;
            Texts.Clear();
            references = new List<Replacer.Tag>();
        }
        public void TagAnalyze()
        {
            references = Texts.Aggregate(new List<Replacer.Tag>(), (list, text) =>
            {
                list.AddRange(text.PlayingCompiler.References);
                return list;
            });
            references = references.Distinct().ToList();
        }
    }
    public class TextPackage
    {
        public List<Setting> Texts;
        public string Name;
        public float[] Position;
        public float Size;
        public List<JSData> JSDatas;
        public static TextPackage Pack(TextGroup group)
        {
            TextPackage pkg = new TextPackage();
            pkg.Name = group.Name;
            pkg.Position = new float[2] { group.Position.x, group.Position.y };
            pkg.Size = group.Size;
            pkg.Texts = group.Texts.Select(t => t.TSetting).ToList();
            List<JSData> jsDatas = pkg.JSDatas = new List<JSData>();
            foreach (var reference in group.references)
                if (reference.SourcePath != null)
                    jsDatas.Add(new JSData()
                    {
                        Name = reference.Name,
                        Inits = reference.SourcePath.Contains("Inits/"),
                        Source = File.ReadAllText(reference.SourcePath)
                    });
            return pkg;
        }
        public static TextGroup Unpack(TextPackage pkg)
        {
            foreach (var js in pkg.JSDatas)
            {
                if (js.Inits)
                    File.WriteAllText(Path.Combine(Main.InitJSPath, js.Name + ".js"), js.Source);
                else File.WriteAllText(Path.Combine(Main.CustomTagsPath, js.Name + ".js"), js.Source);
            }
            var group = new TextGroup();
            group.Texts = pkg.Texts.Select(t => new OverlayerText(group, t).Apply()).ToList();
            group.Name = pkg.Name;
            group.Position = new Vector2(pkg.Position[0], pkg.Position[1]);
            group.Size = pkg.Size;
            return group;
        }
    }
    public class JSData
    {
        public bool Inits;
        public string Name;
        public string Source;
    }
}

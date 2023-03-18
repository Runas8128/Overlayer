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
using JSEngine;
using System.Xml.Linq;
using System.Collections.ObjectModel;

namespace Overlayer
{
    public class TextGroup
    {
        readonly bool isGlobal = false;
        internal List<Tag> references = new List<Tag>();
        private Vector2 relativePos = new Vector2();
        private float relativeSize = 0;
        public TextGroup(bool isGlobal = false)
        {
            Name = string.Empty;
            Texts = new List<OverlayerText>();
            if (this.isGlobal = isGlobal)
                Name = "Global";
        }
        public int Count { get; internal set; }
        public string LoadedPath { get; private set; }
        public string Name { get; set; }
        public List<OverlayerText> Texts { get; set; }
        public ReadOnlyCollection<Tag> References => references.AsReadOnly();
        public bool Expanded = false;
        public Vector2 Position
        {
            get => relativePos;
            set
            {
                var inc = value - relativePos;
                relativePos = value;
                Texts.ForEach(t =>
                {
                    t.TSetting.Position[0] += inc.x;
                    t.TSetting.Position[1] += inc.y;
                });
                Texts.ForEach(t => t.Apply());
            }
        }
        public float Size
        {
            get => relativeSize;
            set
            {
                var inc = value - relativeSize;
                relativeSize = value;
                Texts.ForEach(t => t.TSetting.FontSize += inc);
                Texts.ForEach(t => t.Apply());
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
                            Add(new Setting());
                        if (GUILayout.Button("Remove Group"))
                        {
                            Clear();
                            if (File.Exists(LoadedPath))
                                File.Delete(LoadedPath);
                            OverlayerText.Groups.Remove(this);
                        }
                        if (GUILayout.Button("Export Group"))
                        {
                            var result = StandaloneFileBrowser.OpenFolderPanel("Export Group", Persistence.GetLastUsedFolder(), true);
                            if (result.Length > 0) Array.ForEach(result, Save);
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Position");
                        Vector2 v = relativePos;
                        if (DrawVector(ref v))
                            Position = v;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        float s = relativeSize;
                        if (DrawFloatField(ref s, "Size"))
                            Size = s;
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
            var json = File.ReadAllText(LoadedPath = path);
            try
            {
                var pkg = json.FromJson<TextPackage>();
                var group = TextPackage.Unpack(pkg);
                Texts = group.Texts;
                Name = group.Name;
                Expanded = group.Expanded;
                Position = group.relativePos;
                Size = group.relativeSize;
            }
            catch
            {
                var texts = json.FromJson<List<Setting>>();
                Texts = texts.Select(s => new OverlayerText(this, s)).ToList();
            }
            Texts.ForEach(t => t.Apply());
            Order();
        }
        public void Save(string path = null)
        {
            if (File.Exists(LoadedPath))
                File.Delete(LoadedPath);
            var json = TextPackage.Pack(this).ToJson();
            if (isGlobal)
                File.WriteAllText(OverlayerText.GlobalTextsPath, json);
            else
            {
                var name = $"{Name}.txtgrp";
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
            group.Add(text.TSetting.Copy());
            Remove(text);
            Order();
        }
        public void Remove(OverlayerText text)
        {
            int index = Texts.IndexOf(text);
            Texts.RemoveAt(index);
            UnityEngine.Object.Destroy(text.SText);
            UnityEngine.Object.Destroy(text.SText.Main);
            UnityEngine.Object.Destroy(text.SText.Shadow);
            text.Activated = false;
            text.PlayingCompiler = null;
            text.NotPlayingCompiler = null;
            text.BrokenNotPlayingCompiler = null;
            text.BrokenNotPlayingCompiler = null;
            text.SText.Updater = null;
            for (int i = index; i < Texts.Count; i++)
            {
                var txt = Texts[i];
                txt.Number--;
                txt.SText.Number--;
            }
            Count--;
            GC.SuppressFinalize(text);
            Order();
        }
        public void Order()
        {
            Texts = Texts.OrderBy(o => o.Number).ToList();
            TraceReference();
        }
        public void Clear()
        {
            for (int i = 0; i < Texts.Count; i++)
                Remove(Texts[i]);
            Count = 0;
            Texts.Clear();
            references = new List<Tag>();
        }
        public void TraceReference()
        {
            HashSet<Tag> refs = new HashSet<Tag>();
            foreach (var tag in Texts.Where(t => t.Activated).SelectMany(t => t.PlayingCompiler.References))
                refs.Add(tag);
            references = refs.ToList();
        }
    }
    public class TextPackage
    {
        public List<Setting> Texts;
        public string Name;
        public float[] Position;
        public float Size;
        public bool Expanded;
        public List<CustomTag> CustomTags;
        public static TextPackage Pack(TextGroup group)
        {
            TextPackage pkg = new TextPackage();
            pkg.Name = group.Name;
            pkg.Position = new float[2] { group.Position.x, group.Position.y };
            pkg.Size = group.Size;
            pkg.Expanded = group.Expanded;
            pkg.Texts = group.Texts.Where(t => t.Activated).Select(t => t.TSetting).ToList();
            List<CustomTag> ctDatas = pkg.CustomTags = new List<CustomTag>();
            foreach (var reference in group.references)
                if (reference.SourcePath != null)
                    ctDatas.Add(new CustomTag()
                    {
                        Py = Path.GetExtension(reference.SourcePath) == ".py",
                        Name = reference.Name,
                        Inits = reference.SourcePath.Contains("Inits/"),
                        Source = File.ReadAllText(reference.SourcePath)
                    });
            return pkg;
        }
        public static TextGroup Unpack(TextPackage pkg)
        {
            foreach (var ct in pkg.CustomTags)
            {
                if (ct.Inits)
                {
                    var path = Path.Combine(Main.InitsPath, ct.Name + (ct.Py ? ".py" : ".js"));
                    if (File.Exists(path)) continue;
                    File.WriteAllText(path, ct.Source);
                    ScriptEngine engine = new ScriptEngine();
                    ct.Source.CompileExec()();
                }
                else
                {
                    var path = Path.Combine(Main.CustomTagsPath, ct.Name + (ct.Py ? ".py" : ".js"));
                    if (File.Exists(path)) continue;
                    File.WriteAllText(path, ct.Source);
                    if (Main.LoadJSTag(path, ct.Name, out var tag))
                    {
                        Main.AllTags.SetTag(tag.Name, tag);
                        Main.NotPlayingTags.SetTag(tag.Name, tag);
                        Main.CustomTagCache.Add(tag.Name);
                    }
                }
            }
            var group = new TextGroup();
            group.Texts = pkg.Texts.Select(t => new OverlayerText(group, t)).ToList();
            group.Name = pkg.Name;
            group.Expanded = pkg.Expanded;
            group.Position = new Vector2(pkg.Position[0], pkg.Position[1]);
            group.Size = pkg.Size;
            return group;
        }
    }
    public class CustomTag
    {
        public bool Py;
        public bool Inits;
        public string Name;
        public string Source;
    }
}

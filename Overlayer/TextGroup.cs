using Overlayer.Core;
using Overlayer.Core.Utils;
using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyJson;
using UnityEngine;
using Setting = Overlayer.OverlayerText.Setting;

namespace Overlayer
{
    public class TextGroup
    {
        static bool globalExists = false;
        readonly bool isGlobal = false;
        readonly List<Replacer.Tag> references = new List<Replacer.Tag>();
        private Vector2 relativePos = new Vector2();
        private float relativeSize = 0;
        public TextGroup(bool isGlobal = false)
        {
            Name = string.Empty;
            Texts = new List<OverlayerText>();
            globalExists |= this.isGlobal = isGlobal;
            if (globalExists && isGlobal)
                throw new InvalidOperationException("Global Text Group Cannot Be 2!");
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
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Export"))
                    Array.ForEach(StandaloneFileBrowser.OpenFolderPanel("Export Group", Persistence.GetLastUsedFolder(), true), Save);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                foreach (var text in Texts)
                    text.GUI();
            }
            else
            {
                GUILayout.BeginHorizontal();
                Expanded = GUILayout.Toggle(Expanded, Name);
                Name = GUILayout.TextField(Name);
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Export"))
                    Array.ForEach(StandaloneFileBrowser.OpenFolderPanel("Export Group", Persistence.GetLastUsedFolder(), true), Save);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (Expanded)
                    GUIUtils.IndentGUI(() =>
                    {
                        foreach (var text in Texts)
                            text.GUI();
                    });
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
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
            var settings = json.FromJson<List<Setting>>();
            for (int i = 0; i < settings.Count; i++)
                Texts.Add(new OverlayerText(this, settings[i]).Apply());
            Order();
        }
        public void Save(string path = null)
        {
            var settings = Texts.Select(t => t.TSetting);
            var json = settings.ToList().ToJson();
            if (isGlobal)
                File.WriteAllText(OverlayerText.GlobalTextsPath, json);
            else
            {
                var name = $"{Name}_Group.json";
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
            group.Add(text.TSetting);
        }
        public void Remove(OverlayerText text)
        {
            int index = Texts.IndexOf(text);
            Texts.RemoveAt(index);
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
        }
        public void Order()
        {
            Texts = Texts.OrderBy(o => o.Number).ToList();
        }
        public void Clear()
        {
            foreach (var text in Texts)
            {
                text.PlayingCompiler = null;
                text.NotPlayingCompiler = null;
                UnityEngine.Object.Destroy(text.SText.Main.gameObject);
                UnityEngine.Object.Destroy(text.SText.Shadow.gameObject);
            }
            UnityEngine.Object.Destroy(ShadowText.PublicCanvas.gameObject);
            ShadowText.Count = 0;
            Texts.Clear();
        }
        public void TagAnalyze()
        {
            references = Texts.Aggregate(new List<Tag>)
        }
    }
}

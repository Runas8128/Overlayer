using HarmonyLib;
using Overlayer.Patches;
using Overlayer.Core;
using Overlayer.Core.Utils;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using static UnityModManagerNet.UnityModManager;
using System.IO;
using Overlayer.Tags.Global;
using Overlayer.Core.Translation;
using JSEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using JavaScript = Overlayer.Core.JavaScript;
using JSEngine.CustomLibrary;

namespace Overlayer
{
    public static class Main
    {
        public static ModEntry Mod;
        public static ModEntry.ModLogger Logger;
        public static Harmony Harmony;
        public static Language Language;
        public static float fpsTimer = 0;
        public static float fpsTimeTimer = 0;
        public static float lastDeltaTime;
        public static byte[] Impljs;
        public static List<Replacer.Tag> AllTags = new List<Replacer.Tag>();
        public static List<Replacer.Tag> NotPlayingTags = new List<Replacer.Tag>();
        public static Scene activeScene { get; private set; }
        public static void Load(ModEntry modEntry)
        {
            SceneManager.activeSceneChanged += (cur, next) => activeScene = next;
            CustomTagsPath = Path.Combine(modEntry.Path, "CustomTags");
            InitJSPath = Path.Combine(modEntry.Path, "Inits");
            Mod = modEntry;
            Logger = modEntry.Logger;
            var asm = Assembly.GetExecutingAssembly();
            Settings.Load(modEntry);
            UpdateLanguage();
            Performance.Init();
            AllTags.LoadTags(asm);
            using var impljs = asm.GetManifestResourceStream("Overlayer.Impl");
            Impljs = new byte[impljs.Length];
            impljs.Read(Impljs, 0, Impljs.Length);
            //StringBuilder sb = new StringBuilder();
            //foreach (Tag tag in TagManager.AllTags)
            //{
            //    sb.AppendLine("/**");
            //    if (tag.IsOpt)
            //    {
            //        if (tag.IsStringOpt)
            //            sb.AppendLine(" * @param {string} opt");
            //        else sb.AppendLine(" * @param {number} opt");
            //    }
            //    if (tag.IsString)
            //        sb.AppendLine($" * @returns {{string}} {tag.Description}");
            //    else sb.AppendLine($" * @returns {{number}} {tag.Description}");
            //    sb.AppendLine(" */");
            //    sb.Append($"function {tag.Name}(");
            //    if (tag.IsOpt)
            //        sb.Append("opt");
            //    sb.AppendLine(");");
            //}
            //File.WriteAllText("Mods/Overlayer/Tags.js", sb.ToString());

            NotPlayingTags.AddRange(new[]
            {
                AllTags.FindTag("Year"),
                AllTags.FindTag("Month"),
                AllTags.FindTag("Day"),
                AllTags.FindTag("Hour"),
                AllTags.FindTag("Minute"),
                AllTags.FindTag("Second"),
                AllTags.FindTag("MilliSecond"),
                AllTags.FindTag("Fps"),
                AllTags.FindTag("FrameTime"),
                AllTags.FindTag("CurKps"),

                AllTags.FindTag("ProcessorCount"),
                AllTags.FindTag("MemoryGBytes"),
                AllTags.FindTag("CpuUsage"),
                AllTags.FindTag("TotalCpuUsage"),
                AllTags.FindTag("MemoryUsage"),
                AllTags.FindTag("TotalMemoryUsage"),
                AllTags.FindTag("MemoryUsageGBytes"),
                AllTags.FindTag("TotalMemoryUsageGBytes"),

                AllTags.FindTag("TEHex"),
                AllTags.FindTag("VEHex"),
                AllTags.FindTag("EPHex"),
                AllTags.FindTag("PHex"),
                AllTags.FindTag("LPHex"),
                AllTags.FindTag("VLHex"),
                AllTags.FindTag("TLHex"),
                AllTags.FindTag("MPHex"),
                AllTags.FindTag("FMHex"),
                AllTags.FindTag("FOHex"),
            });
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = (mod, deltaTime) =>
            {
                if (Input.anyKeyDown)
                    Variables.KpsTemp++;
                lastDeltaTime += (UnityEngine.Time.deltaTime - lastDeltaTime) * 0.1f;
                if (fpsTimer > Settings.Instance.FPSUpdateRate / 1000.0f)
                {
                    Variables.Fps = 1.0f / lastDeltaTime;
                    fpsTimer = 0;
                }
                fpsTimer += deltaTime;
                if (fpsTimeTimer > Settings.Instance.FrameTimeUpdateRate / 1000.0f)
                {
                    Variables.FrameTime = lastDeltaTime * 1000.0f;
                    fpsTimeTimer = 0;
                }
                fpsTimeTimer += deltaTime;
            };
        }
        public static bool LoadJSTag(string source, string name, out Replacer.Tag tag)
        {
            tag = null;
            string desc = null;
            using (StringReader sr = new StringReader(source))
            {
                string first = sr.ReadLine();
                if (first.StartsWith("//"))
                    desc = first.Remove(0, 2).Trim();
            }
            try
            {
                var del = source.CompileEval();
                tag = new Replacer().CreateTag(name).SetGetter(del);
                Language[name] = desc;
                Logger.Log($"Loaded '{name}' Tag.");
                return true;
            }
            catch (Exception e)
            {
                Logger.Log($"Exception At Loading {name} Tag..\n({e})");
                return false;
            }
        }
        public static readonly List<string> JSTagCache = new List<string>();
        public static void LoadAllJSTags(string folderPath)
        {
            var impljsPath = Path.Combine(folderPath, "Impl.js");
            if (File.Exists(impljsPath))
                File.SetAttributes(impljsPath, File.GetAttributes(impljsPath) & ~FileAttributes.ReadOnly);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                File.WriteAllBytes(impljsPath, Impljs);
                return;
            }
            File.WriteAllBytes(impljsPath, Impljs);
            int success = 0, fail = 0;
            foreach (string path in Directory.GetFiles(folderPath, "*.js"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == "Impl") continue;
                if (LoadJSTag(File.ReadAllText(path), name, out var tag))
                {
                    AllTags.SetTag(tag.Name, tag);
                    NotPlayingTags.SetTag(tag.Name, tag);
                    JSTagCache.Add(tag.Name);
                    success++;
                }
                else fail++;
            }
            Logger.Log($"Loaded {success} Scripts Successfully. (Failed: {fail})");
        }
        public static void ReloadAllJSTags(string folderPath)
        {
            Logger.Log($"Reloading {JSTagCache.Count} Scripts..");
            UnloadAllJSTags();
            LoadAllJSTags(folderPath);
        }
        public static void RunInits()
        {
            Ovlr.harmony.UnpatchAll(Ovlr.harmony.Id);
            if (!Directory.Exists(InitJSPath))
            {
                Directory.CreateDirectory(InitJSPath);
                var impljsPath = Path.Combine(InitJSPath, "Impl.js");
                if (File.Exists(impljsPath))
                    File.SetAttributes(impljsPath, File.GetAttributes(impljsPath) & ~FileAttributes.ReadOnly);
                File.WriteAllBytes(impljsPath, Impljs);
            }
            else
            {
                var impljsPath = Path.Combine(InitJSPath, "Impl.js");
                if (File.Exists(impljsPath))
                    File.SetAttributes(impljsPath, File.GetAttributes(impljsPath) & ~FileAttributes.ReadOnly);
                File.WriteAllBytes(impljsPath, Impljs);
                foreach (string file in Directory.GetFiles(InitJSPath, "*.js"))
                {
                    
                    if (Path.GetFileNameWithoutExtension(file) == "Impl")
                        continue;
                    ScriptEngine engine = new ScriptEngine();
                    File.ReadAllText(file).CompileExec()();
                }
            }
        }
        public static void UnloadAllJSTags()
        {
            foreach (string tagName in JSTagCache)
            {
                AllTags.RemoveTag(tagName);
                NotPlayingTags.RemoveTag(tagName);
            }
            JSTagCache.Clear();
        }
        public static string CustomTagsPath;
        public static string InitJSPath;
        public static UnityAction<Scene, LoadSceneMode> evt = (s, m) => JSPatches.SceneLoads();
        public static bool OnToggle(ModEntry modEntry, bool value)
        {
            try
            {
                if (value)
                {
                    SceneManager.sceneLoaded += evt;
                    Settings.Load(modEntry);
                    Variables.Reset();
                    JavaScript.Init();
                    LoadAllJSTags(CustomTagsPath);
                    OText.Load();
                    if (!OText.Texts.Any())
                        new OText().Apply();
                    Harmony = new Harmony(modEntry.Info.Id);
                    Harmony.PatchAll(Assembly.GetExecutingAssembly());
                    RunInits();
                    UpdateLanguage();
                    var settings = Settings.Instance;
                    DeathMessagePatch.compiler = new Replacer(AllTags);
                    ClearMessagePatch.compiler = new Replacer(AllTags);
                    if (!string.IsNullOrEmpty(settings.DeathMessage))
                        DeathMessagePatch.compiler.Source = settings.DeathMessage;
                    if (!string.IsNullOrEmpty(settings.ClearMessage))
                        ClearMessagePatch.compiler.Source = settings.ClearMessage;
                }
                else
                {
                    SceneManager.sceneLoaded -= evt;
                    OnSaveGUI(modEntry);
                    try
                    {
                        OText.Clear();
                        UnloadAllJSTags();
                        DeathMessagePatch.compiler = null;
                        ClearMessagePatch.compiler = null;
                        GC.Collect();
                    }
                    finally
                    {
                        Harmony.UnpatchAll(Harmony.Id);
                        Harmony = null;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
                return false;
            }
        }
        public static void OnGUI(ModEntry modEntry)
        {
            var settings = Settings.Instance;
            LangGUI(settings);
            settings.DrawManual();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Language[TranslationKeys.DeathMessage]);
            var dm = GUILayout.TextField(settings.DeathMessage);
            if (dm != settings.DeathMessage)
            {
                settings.DeathMessage = dm;
                if (!string.IsNullOrEmpty(settings.DeathMessage))
                    DeathMessagePatch.compiler.Source = settings.DeathMessage;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Language[TranslationKeys.ClearMessage]);
            var cm = GUILayout.TextField(settings.ClearMessage);
            if (cm != settings.ClearMessage)
            {
                settings.ClearMessage = cm;
                if (!string.IsNullOrEmpty(settings.ClearMessage))
                    ClearMessagePatch.compiler.Source = settings.ClearMessage;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Language[TranslationKeys.ReloadCustomTags]))
            {
                ReloadAllJSTags(CustomTagsPath);
                Recompile();
            }
            if (GUILayout.Button(Language[TranslationKeys.ReloadInits]))
            {
                RunInits();
                Recompile();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Language[TranslationKeys.AddText]))
            {
                new OText().Apply();
                OText.Order();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            for (int i = 0; i < OText.Texts.Count; i++)
                OText.Texts[i].GUI();
            AllTags.DescGUI();
        }
        public static void LangGUI(Settings settings)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("한국어"))
            {
                settings.lang = SystemLanguage.Korean;
                UpdateLanguage();
            }
            if (GUILayout.Button("English"))
            {
                settings.lang = SystemLanguage.English;
                UpdateLanguage();
            }
            if (GUILayout.Button("中國語"))
            {
                settings.lang = SystemLanguage.Chinese;
                UpdateLanguage();
            }
            if (GUILayout.Button("日本語"))
            {
                settings.lang = SystemLanguage.Japanese;
                UpdateLanguage();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        public static void UpdateLanguage()
        {
            switch (Settings.Instance.lang)
            {
                case SystemLanguage.Korean:
                    Language = Language.Korean;
                    break;
                case SystemLanguage.English:
                    Language = Language.English;
                    break;
                case SystemLanguage.Chinese:
                    Language = Language.Chinese;
                    break;
                case SystemLanguage.Japanese:
                    Language = Language.Japanese;
                    break;
            }
        }
        public static void OnSaveGUI(ModEntry modEntry)
        {
            Settings.Save(modEntry);
            Variables.Reset();
            OText.Save();
        }
        public static void Recompile()
        {
            foreach (OText text in OText.Texts)
            {
                text.PlayingCompiler.Source = text.TSetting.PlayingText;
                text.NotPlayingCompiler.Source = text.TSetting.NotPlayingText;
                text.BrokenPlayingCompiler.Source = text.TSetting.PlayingText.BreakRichTagWithoutSize();
                text.BrokenNotPlayingCompiler.Source = text.TSetting.NotPlayingText.BreakRichTagWithoutSize();
            }
        }
    }
}
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

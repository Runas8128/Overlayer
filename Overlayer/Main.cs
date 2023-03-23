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
using Overlayer.Core.Tags;
using SFB;
using System.Text;

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
        // Prevent GUI Error
        public static int LockGUIFrames = 0;
        public static List<Tag> AllTags = new List<Tag>();
        public static List<Tag> NotPlayingTags = new List<Tag>();
        public static event Action AllCustomTagsLoaded = delegate { };
        public static event Action AllInitsLoaded = delegate { };
        public static readonly string AsmFullName = Assembly.GetExecutingAssembly().FullName;
        public static Scene activeScene { get; private set; }
        public static bool PythonAvailable { get; private set; }
        public static void Load(ModEntry modEntry)
        {
            PythonAvailable = modEntries.Any(m => m.Info.Id == "Overlayer.Python" && m.Enabled);
            SceneManager.activeSceneChanged += (cur, next) => activeScene = next;
            CustomTagsPath = Path.Combine(modEntry.Path, "CustomTags");
            InitsPath = Path.Combine(modEntry.Path, "Inits");
            Mod = modEntry;
            Logger = modEntry.Logger;
            if (PythonAvailable)
                Logger.Log("Python Is Available!");
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
        public static bool LoadJSTag(string path, string name, out Tag tag)
        {
            tag = null;
            string desc = null;
            var source = File.ReadAllText(path);
            using (StringReader sr = new StringReader(source))
            {
                string first = sr.ReadLine();
                if (first.StartsWith("//"))
                    desc = first.Remove(0, 2).Trim();
            }
            try
            {
                var del = source.CompileEval();
                tag = new Tag(name).SetGetter(del);
                tag.Build();
                tag.SourcePath = path;
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
        public static readonly List<string> CustomTagCache = new List<string>();
        public static void LoadAllCustomTags(string folderPath)
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
                if (LoadJSTag(path, name, out var tag))
                {
                    AllTags.SetTag(tag.Name, tag);
                    NotPlayingTags.SetTag(tag.Name, tag);
                    CustomTagCache.Add(tag.Name);
                    success++;
                }
                else fail++;
            }
            AllCustomTagsLoaded();
            Logger.Log($"Loaded {success} Scripts Successfully. (Failed: {fail})");
        }
        public static void LockGUI(int frames = 1) => LockGUIFrames += frames;
        public static void ReloadAllCustomTags(string folderPath)
        {
            Logger.Log($"Reloading {CustomTagCache.Count} Scripts..");
            UnloadAllCustomTags();
            LoadAllCustomTags(folderPath);
        }
        public static void RunInits()
        {
            Ovlr.harmony.UnpatchAll(Ovlr.harmony.Id);
            if (!Directory.Exists(InitsPath))
            {
                Directory.CreateDirectory(InitsPath);
                var impljsPath = Path.Combine(InitsPath, "Impl.js");
                if (File.Exists(impljsPath))
                    File.SetAttributes(impljsPath, File.GetAttributes(impljsPath) & ~FileAttributes.ReadOnly);
                File.WriteAllBytes(impljsPath, Impljs);
            }
            else
            {
                var impljsPath = Path.Combine(InitsPath, "Impl.js");
                if (File.Exists(impljsPath))
                    File.SetAttributes(impljsPath, File.GetAttributes(impljsPath) & ~FileAttributes.ReadOnly);
                File.WriteAllBytes(impljsPath, Impljs);
                foreach (string file in Directory.GetFiles(InitsPath, "*.js"))
                {
                    if (Path.GetFileNameWithoutExtension(file) == "Impl")
                        continue;
                    ScriptEngine engine = new ScriptEngine();
                    file.CompileFileExec()();
                }
            }
            AllInitsLoaded();
        }
        public static void UnloadAllCustomTags()
        {
            foreach (string tagName in CustomTagCache)
            {
                AllTags.RemoveTag(tagName);
                NotPlayingTags.RemoveTag(tagName);
            }
            CustomTagCache.Clear();
        }
        public static string CustomTagsPath;
        public static string InitsPath;
        public static string ErrorString = null;
        public static int OverlayerEntryIndex;
#if !TOURNAMENT
        public static UnityAction<Scene, LoadSceneMode> evt = (s, m) => JSPatches.SceneLoads();
#endif
        public static void CatchException(Exception e)
        {
            var target = e.TargetSite;
            if (!target.DeclaringType.Assembly.FullName.Contains("Overlayer"))
                return;
            if (target.Name == "Update" ||
                target.Name == "FixedUpdate" ||
                target.Name == "LateUpdate")
                return;
            StringBuilder error = new StringBuilder();
            error.AppendLine($"Exception: {e.GetType()}");
            error.AppendLine($"Message: {e.Message}");
            error.AppendLine($"Target Site: {e.TargetSite.FullDescription()}");
            error.AppendLine($"StackTrace: {e.StackTrace}");
            ErrorString = error.ToString();
            Mod.Info.DisplayName = "Overlayer <b>Error Detected! See GUI Option!</b>";
        }
        public static void Reported()
        {
            ErrorString = null;
            Mod.Info.DisplayName = "Overlayer";
        }
        public static bool OnToggle(ModEntry modEntry, bool value)
        {
            try
            {
                if (value)
                {
                    PythonAvailable = modEntries.Any(m => m.Info.Id == "Overlayer.Python" && m.Enabled);
                    OverlayerEntryIndex = modEntries.IndexOf(modEntry);
                    ExceptionCatcher.Catch();
                    ExceptionCatcher.Unhandled += CatchException;
                    SceneManager.sceneLoaded += evt;
                    Backup();
                    Settings.Load(modEntry);
                    Variables.Reset();
                    JavaScript.Init();
                    if (!PythonAvailable)
                    {
                        RunInits();
                        LoadAllCustomTags(CustomTagsPath);
                        OverlayerText.Load();
                    }
                    Harmony = new Harmony(modEntry.Info.Id);
                    Harmony.PatchAll(Assembly.GetExecutingAssembly());
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
                    ExceptionCatcher.Unhandled -= CatchException;
                    ExceptionCatcher.Drop();
                    SceneManager.sceneLoaded -= evt;
                    OnSaveGUI(modEntry);
                    try
                    {
                        OverlayerText.Clear();
                        UnloadAllCustomTags();
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
                CatchException(e);
                return true;
            }
        }
        public static void OnGUI(ModEntry modEntry)
        {
            if (LockGUIFrames > 0)
            {
                LockGUIFrames--;
                return;
            }
            if (ErrorString != null)
            {
                GUILayout.Label("<b>Overlayer Error Detected!</b>");
                GUILayout.Label($"<b>{ErrorString}</b>");
                GUILayout.Label("<b>PLEASE CAPTURE THIS MESSAGE AND REPORT!</b>");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("I Reported"))
                    Reported();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            try
            {
                var settings = Settings.Instance;
                LangGUI(settings);
                settings.DrawManual();
                settings.CollectLevels = GUILayout.Toggle(settings.CollectLevels, "Allow Collecting Playing Levels (For Improve Difficulty Predicting Performance)");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Recover With Backup Files")) Recover();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
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
                    ReloadAllCustomTags(CustomTagsPath);
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
                if (GUILayout.Button("Import Group"))
                {
                    var result = StandaloneFileBrowser.OpenFilePanel("Import Group", Persistence.GetLastUsedFolder(), "txtgrp", false);
                    if (result.Length > 0)
                    {
                        var group = new TextGroup();
                        group.Name = Path.GetFileNameWithoutExtension(result[0]);
                        OverlayerText.Groups.Add(group);
                        group.Load(result[0]);
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Language[TranslationKeys.AddText]))
                    OverlayerText.Global.Add(new OverlayerText.Setting());
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                OverlayerText.Global.GUI();

                if (OverlayerText.Groups.Any())
                {
                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("--Groups--");
                    GUILayout.EndHorizontal();
                    for (int i = 0; i < OverlayerText.Groups.Count; i++)
                        OverlayerText.Groups[i].GUI();
                    GUILayout.EndVertical();
                }

                AllTags.DescGUI();
            }
            catch 
            {
                if (ErrorString != null)
                {
                    GUILayout.Label("<b>Overlayer Error Detected!</b>");
                    GUILayout.Label($"<b>{ErrorString}</b>");
                    GUILayout.Label("<b>PLEASE CAPTURE THIS MESSAGE AND REPORT!</b>");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("I Reported"))
                        ErrorString = null;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
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
            // Force Save Persistence
            foreach (var kvp in PlaytimeCounter.PlayTimes)
                Persistence.generalPrefs.SetFloat(kvp.Key, kvp.Value);
            List<object> list = new List<object>();
            foreach (CalibrationPreset calibrationPreset in scrConductor.userPresets)
                list.Add(calibrationPreset.ToDict());
            Persistence.generalPrefs.SetList("calibrationPresets", list);
            PlayerPrefsJson playerPrefsJson = PlayerPrefsJson.SelectAll();
            playerPrefsJson.deltaDict.Add("version", 101);
            playerPrefsJson.ApplyDeltaDict();
            PlayerPrefsJson.SaveAllFiles();

            Settings.Save(modEntry);
            Variables.Reset();
            OverlayerText.Save();
        }
        public static void Recompile()
        {
            foreach (OverlayerText text in OverlayerText.Global.Texts.Concat(OverlayerText.Groups.SelectMany(g => g.Texts)))
            {
                text.PlayingCompiler.Compile();
                text.NotPlayingCompiler.Compile();
                text.BrokenPlayingCompiler.Compile();
                text.BrokenNotPlayingCompiler.Compile();
            }
            DeathMessagePatch.compiler?.Compile();
            ClearMessagePatch.compiler?.Compile();
        }
        public static void Backup()
        {
            foreach (var file in Directory.GetFiles(Mod.Path, "*.json").Concat(Directory.GetFiles(Mod.Path, "*.txtgrp")).Concat(Directory.GetFiles(Mod.Path, "*.xml")).Where(f => Path.GetFileName(f) != "info.json"))
                File.WriteAllBytes(file + ".backup", File.ReadAllBytes(file));
        }
        public static void Recover()
        {
            foreach (var file in Directory.GetFiles(Mod.Path, "*.backup"))
                File.WriteAllBytes(file.Remove(file.LastIndexOf(".backup"), 7), File.ReadAllBytes(file));
        }
    }
}
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

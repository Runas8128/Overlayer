using Overlayer.Core;
using System;
using static UnityModManagerNet.UnityModManager;
using System.IO;
using Overlayer.Python.CustomLibrary;
using static IronPython.Modules._ast;
using System.Reflection;
using System.Text;

namespace Overlayer.Python
{
    public static class Main
    {
        public static byte[] ImplPy;
        public static ModEntry.ModLogger Logger { get; private set; }
        public static void LoadAllPyTags()
        {
            var implpyPath = Path.Combine(Overlayer.Main.CustomTagsPath, "Impl.py");
            if (!Directory.Exists(Overlayer.Main.CustomTagsPath))
            {
                Directory.CreateDirectory(Overlayer.Main.CustomTagsPath);
                File.WriteAllBytes(implpyPath, ImplPy);
                return;
            }
            File.WriteAllBytes(implpyPath, ImplPy);
            int success = 0, fail = 0;
            foreach (string path in Directory.GetFiles(Overlayer.Main.CustomTagsPath, "*.py"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == "Impl") continue;
                try
                {
                    Ovlr.currentSource = path;
                    var tag = PythonUtils.CreateTag(path);
                    Overlayer.Main.AllTags.SetTag(tag.Name, tag);
                    Overlayer.Main.NotPlayingTags.SetTag(tag.Name, tag);
                    Overlayer.Main.CustomTagCache.Add(tag.Name);
                    success++;
                }
                catch { fail++; }
            }
            Ovlr.currentSource = null;
        }
        public static void RunInits()
        {
            var implpyPath = Path.Combine(Overlayer.Main.InitsPath, "Impl.py");
            if (!Directory.Exists(Overlayer.Main.InitsPath))
            {
                Directory.CreateDirectory(Overlayer.Main.InitsPath);
                File.WriteAllBytes(implpyPath, ImplPy);
                return;
            }
            File.WriteAllBytes(implpyPath, ImplPy);
            foreach (string file in Directory.GetFiles(Overlayer.Main.InitsPath, "*.py"))
            {
                if (Path.GetFileNameWithoutExtension(file) == "Impl") continue;
                Ovlr.currentSource = file;
                try { PythonUtils.Execute(file); }
                catch (Exception e) { Logger.Log($"Error On Executing Script ({file}): {e.Message}"); }
            }
            Ovlr.currentSource = null;
        }
        public static void Load(ModEntry modEntry)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (var implpy = asm.GetManifestResourceStream("Overlayer.Python.Impl"))
            {
                ImplPy = new byte[implpy.Length];
                implpy.Read(ImplPy, 0, ImplPy.Length);
            }
            Logger = modEntry.Logger;
            Overlayer.Main.AllCustomTagsLoaded += LoadAllPyTags;
            Overlayer.Main.AllInitsLoaded += RunInits;
            Overlayer.Main.LoadAllCustomTags(Overlayer.Main.CustomTagsPath);
            OverlayerText.Load();
            Overlayer.Main.RunInits();
        }
    }
}

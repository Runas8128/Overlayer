using Overlayer.Core;
using System;
using static UnityModManagerNet.UnityModManager;
using System.IO;
using Overlayer.Python.CustomLibrary;
using static IronPython.Modules._ast;
using System.Reflection;
using System.Text;
using System.Linq;

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
            //StringBuilder sb = new StringBuilder();
            //foreach (var tag in Overlayer.Main.AllTags)
            //{
            //    if (tag.HasOption)
            //        sb.AppendLine($"def {tag.Name}(op) -> {(tag.Getter.ReturnType == typeof(double) ? "float" : "str")}:");
            //    else sb.AppendLine($"def {tag.Name}() -> {(tag.Getter.ReturnType == typeof(double) ? "float" : "str")}:");
            //    if (tag.HasOption)
            //        sb.AppendLine($"  return Overlayer_Internal.{tag.Name}(op)");
            //    else sb.AppendLine($"  return Overlayer_Internal.{tag.Name}()");
            //}
            //File.WriteAllText(Path.Combine(modEntry.Path, "tag.py"), sb.ToString());

            //sb.Clear();
            //foreach (var tag in Overlayer.Main.AllTags)
            //{
            //    if (tag.HasOption)
            //        sb.AppendLine($"public static {tag.Getter.ReturnType.Name} {tag.Name}({tag.Getter.GetParameters().First().ParameterType.Name} op) => {tag.Getter.DeclaringType.Name}.{tag.Getter.Name}(op);");
            //    else sb.AppendLine($"public static {tag.Getter.ReturnType.Name} {tag.Name}() => {tag.Getter.DeclaringType.Name}.{tag.Getter.Name}();");
            //}
            //File.WriteAllText(Path.Combine(modEntry.Path, "tag.cs"), sb.ToString());
        }
    }
}

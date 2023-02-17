using IronPython.Runtime;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Overlayer.Core;
using Overlayer.Python.CustomLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Py = IronPython.Hosting.Python;

namespace Overlayer.Python
{
    public static class PythonUtils
    {
        public static readonly Dictionary<string, object> options = new Dictionary<string, object>()
        {
            ["Overlayer_Internal"] = DynamicHelpers.GetPythonTypeFromType(typeof(Ovlr)),
        };
        public static string[] modulePaths = new string[]
        {
            Overlayer.Main.InitsPath,
            Overlayer.Main.CustomTagsPath
        };
        public static Func<dynamic> Compile(string path)
        {
            var engine = Py.CreateEngine();
            var source = engine.CreateScriptSourceFromFile(path, Encoding.UTF8, SourceCodeKind.AutoDetect);
            ScriptScope scope = Py.GetBuiltinModule(engine);
            scope.SetVariable("__import__", new ImportDelegate(ResolveImport));
            engine.SetSearchPaths(modulePaths);
            var com = source.Compile();
            return com.Execute<dynamic>;
        }
        public static Replacer.Tag CreateTag(string path)
        {
            Replacer tmp = new Replacer();
            var name = Path.GetFileNameWithoutExtension(path);
            var tag = tmp.CreateTag(name);
            tag.SourcePath = path;
            tag.SetGetter(Compile(path));
            tag.Build();
            return tag;
        }
        public static object Execute(string path) => Compile(path)();
        private static object ResolveImport(CodeContext context, string moduleName, PythonDictionary globals, PythonDictionary locals, PythonTuple fromlist, int level)
        {
            var builtin = IronPython.Modules.Builtin.__import__(context, moduleName, globals, locals, fromlist, level);
            var module = builtin as PythonModule;
            foreach (var kvp in options)
                module?.__setattr__(context, kvp.Key, kvp.Value);
            return builtin;
        }
    }
    public delegate object ImportDelegate(CodeContext context, string moduleName, PythonDictionary globals, PythonDictionary locals, PythonTuple fromlist, int level);
}

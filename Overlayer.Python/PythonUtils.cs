using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Types;
using JSEngine;
using Microsoft.Scripting.Hosting;
using Overlayer.Core;
using Overlayer.Python.CustomLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
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
        public static Func<T> Compile<T>(string path)
        {
            var engine = Py.CreateEngine();
            ScriptScope scope = Py.GetBuiltinModule(engine);
            scope.SetVariable("__import__", new ImportDelegate(ResolveImport));
            ScriptScope eScope = engine.CreateScope();
            engine.SetSearchPaths(modulePaths);
            foreach (var tag in Overlayer.Main.AllTags)
                eScope.SetVariable(tag.Name, tag.GetterDelegate);
            var source = engine.CreateScriptSourceFromFile(path);
            var com = source.Compile();
            return () => com.Execute<T>(eScope);
        }
        public static Replacer.Tag CreateTag(string path)
        {
            Replacer tmp = new Replacer();
            var name = Path.GetFileNameWithoutExtension(path);
            var tag = tmp.CreateTag(name);
            tag.SourcePath = path;
            tag.SetGetter(Compile<object>(path));
            tag.Build();
            return tag;
        }
        public static object Execute(string path) => Compile<object>(path)();
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

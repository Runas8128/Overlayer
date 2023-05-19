using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Overlayer.Core;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Linq.Expressions;
using System;
using Py = IronPython.Hosting.Python;
using Overlayer.Core.ExceptionHandling;
using IronPython.Runtime.Types;
using Overlayer.Scripting.JS;
using System.Reflection;
using System.IO;

namespace Overlayer.Scripting.Python
{
    public static class PythonUtils
    {
        public static readonly Dictionary<string, object> apiOptions = new Dictionary<string, object>();
        static bool apiInitialized = false;
        public static readonly Dictionary<string, object> tagsOptions = new Dictionary<string, object>();
        static bool tagsInitialized = false;
        public static void Prepare()
        {
            if (!apiInitialized)
            {
                var delegates = Api.GetApiMethods(ScriptType.Python).Select(m => (m.Name, m.CreateDelegate(m.ReturnType != typeof(void) ? Expression.GetFuncType(m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType).ToArray()) : Expression.GetActionType(m.GetParameters().Select(p => p.ParameterType).ToArray()))));
                foreach (var (name, del) in delegates)
                    apiOptions.Add(name, del);
                var types = Api.GetApiTypesWithAttr(ScriptType.Python);
                foreach (var (attr, t) in types)
                    apiOptions.Add(attr.Name ?? t.Name, DynamicHelpers.GetPythonTypeFromType(t));
                apiInitialized = true;
            }
            if (!tagsInitialized)
            {
                foreach (var (name, del) in TagManager.All.Select(t => (t.Name, t.GetterDelegate)))
                    tagsOptions.Add(name, del);
                tagsInitialized = true;
            }
        }
        public static Result CompileExec(string path)
        {
            Prepare();
            var engine = CreateEngine(path, out var source);
            var scope = engine.CreateScope();
            var scr = source.Compile();
            return new Result(scr, scope);
        }
        static string[] modulePaths => new string[] { Main.Mod.Path, Main.ScriptPath, Main.ScriptModulePath };
        public static Result CompileEval(string path)
        {
            Prepare();
            var engine = CreateEngine(path, out var source);
            var scope = engine.CreateScope();
            var scr = source.Compile();
            return new Result(scr, scope);
        }
        public static Result CompileExecSource(string source)
        {
            Prepare();
            var engine = CreateEngineFromSource(source, out var scrSource);
            var scope = engine.CreateScope();
            var scr = scrSource.Compile();
            return new Result(scr, scope);
        }
        public static Result CompileEvalSource(string source)
        {
            Prepare();
            var engine = CreateEngineFromSource(source, out var scrSource);
            var scope = engine.CreateScope();
            var scr = scrSource.Compile();
            return new Result(scr, scope);
        }
        public static ScriptEngine CreateEngine(string path, out ScriptSource source)
        {
            var engine = Py.CreateEngine();
            source = engine.CreateScriptSourceFromFile(path, Encoding.UTF8, SourceCodeKind.AutoDetect);
            ScriptScope scope = Py.GetBuiltinModule(engine);
            scope.SetVariable("__import__", new ImportDelegate(ResolveImport));
            engine.SetSearchPaths(modulePaths);
            return engine;
        }
        public static ScriptEngine CreateEngineFromSource(string src, out ScriptSource source)
        {
            var engine = Py.CreateEngine();
            source = engine.CreateScriptSourceFromString(src, SourceCodeKind.AutoDetect);
            ScriptScope scope = Py.GetBuiltinModule(engine);
            scope.SetVariable("__import__", new ImportDelegate(ResolveImport));
            engine.SetSearchPaths(modulePaths);
            return engine;
        }
        private static object ResolveImport(CodeContext context, string moduleName, PythonDictionary globals, PythonDictionary locals, PythonTuple fromlist, int level)
        {
            object builtin = null;
            for (int i = level; i < 5; i++)
            {
                builtin = IronPython.Modules.Builtin.__import__(context, moduleName, globals, locals, fromlist, i);
                if (builtin != null) break;
            }
            if (builtin == null) return null;
            var module = builtin as PythonModule;
            moduleName = moduleName.Replace("Modules.", string.Empty);
            Main.Logger.Log($"moduleName: {moduleName}, level: {level}");
            switch (moduleName)
            {
                case "Api":
                    foreach (var kvp in apiOptions)
                        module?.__setattr__(context, kvp.Key, kvp.Value);
                    break;
                case "Tags":
                    foreach (var kvp in tagsOptions)
                        module?.__setattr__(context, kvp.Key, kvp.Value);
                    break;
            }
            return builtin;
        }
        public static void WriteType(Type type, StringBuilder sb, string alias = null)
        {
            sb.Append("class ");
            var tName = (alias ?? type.Name).RemoveAfter("`");
            sb.AppendLine($"{tName}():");
            #region Fields And Properties
            bool any = false;
            sb.AppendLine("  def __init__(self):");
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name.StartsWith("<"))
                    continue;
                if (field.IsStatic) continue;
                sb.AppendLine($"    self.{field.Name}:{PyModuleGenerator.GetTypeStr(field.FieldType, true)} = None");
                any = true;
            }
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.Name.StartsWith("<"))
                    continue;
                var name = prop.Name.Split('.').Last();
                sb.AppendLine($"    self.{prop.Name}:{PyModuleGenerator.GetTypeStr(prop.PropertyType, true)} = None");
                any = true;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.Name.StartsWith("<"))
                    continue;
                sb.AppendLine($"    {field.Name}:{PyModuleGenerator.GetTypeStr(field.FieldType, true)} = None");
                any = true;
            }
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (prop.Name.StartsWith("<"))
                    continue;
                var name = prop.Name.Split('.').Last();
                sb.AppendLine($"    {prop.Name}:{PyModuleGenerator.GetTypeStr(prop.PropertyType, true)} = None");
                any = true;
            }
            if (!any) sb.AppendLine("    pass");
            #endregion
            #region Methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).OrderBy(x => x.Name))
            {
                if (method.IsObjectDeclared()) continue;
                if (method.Name.StartsWith("<"))
                    continue;
                if (method.IsSpecialName && !method.Name.StartsWith("add_") && !method.Name.StartsWith("remove_"))
                    continue;
                var prms = method.GetParameters();
                if (method.IsStatic)
                    sb.AppendLine("  @staticmethod");
                if (prms.Length > 0)
                    sb.AppendLine($"  def {method.Name}({PyModuleGenerator.GetArgStr(prms)}) -> {PyModuleGenerator.GetTypeStr(method.ReturnType)}: {(method.ReturnType != typeof(void) ? "return " : "")}{tName}.{method.Name}({PyModuleGenerator.GetCallArgStr(prms)})");
                else
                    sb.AppendLine($"  def {method.Name}() -> {PyModuleGenerator.GetTypeStr(method.ReturnType)}: {(method.ReturnType != typeof(void) ? "return " : "")}{tName}.{method.Name}()");
            }
            #endregion
        }
    }
    public delegate object ImportDelegate(CodeContext context, string moduleName, PythonDictionary globals, PythonDictionary locals, PythonTuple fromlist, int level);
}
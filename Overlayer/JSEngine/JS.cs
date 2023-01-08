using System;
using System.Text;
using System.IO;
using JSEngine.Compiler;
using JSEngine.Library;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Overlayer;

namespace JSEngine
{
    /// <summary>
    /// Powered By <see href="https://github.com/paulbartrum/jurassic">Jurassic</see>
    /// </summary>
    public static class JS
    {
        public static readonly CompilerOptions Option = new CompilerOptions()
        {
            ForceStrictMode = false,
            EnableILAnalysis = false,
            CompatibilityMode = CompatibilityMode.Latest
        };
        public static Func<object> CompileEval(this string js, ScriptEngine engine)
        {
            var source = new TextSource(js, engine);
            if (source.IsProxy)
            {
                var pType = source.ProxyType;
                engine.SetGlobalValue(pType.Name, pType);
                return null;
            }
            var scr = CompiledEval.Compile(source, Option);
            return () => scr.EvaluateFastInternal(engine);
        }
        public static Action CompileExec(this string js, ScriptEngine engine)
        {
            var source = new TextSource(js, engine);
            if (source.IsProxy)
            {
                var pType = source.ProxyType;
                engine.SetGlobalValue(pType.Name, pType);
                return null;
            }
            var scr = CompiledScript.Compile(source, Option);
            return () => scr.ExecuteFastInternal(engine);
        }
        class TextSource : ScriptSource
        {
            public string str;
            public ScriptEngine engine;
            public bool IsProxy => ProxyType != null;
            public Type ProxyType { get; private set; }
            public TextSource(string str, ScriptEngine engine)
            {
                this.str = str;
                var reader = new StringReader(str);
                var firstLine = reader.ReadLine();
                if (firstLine.EndsWith(" Proxy"))
                    ProxyType = AccessTools.TypeByName(firstLine.Split(' ')[1]);
                reader.Close();
                this.engine = engine;
            }
            public override string Path => null;
            public override TextReader GetReader()
            {
                using (StringReader sr = new StringReader(str))
                {
                    StringBuilder sb = new StringBuilder();
                    string line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("import"))
                        {
                            line = line.Replace(";", "");
                            var f = GetAfter(line, "import").Trim();
                            if (f.StartsWith("\"") || f.StartsWith("'") || f.StartsWith("`"))
                                continue;
                            var orig = RemoveStartEnd(GetAfter(line, "from").Trim());
                            var module = System.IO.Path.Combine(Main.InitJSPath, orig);
                            module = System.IO.Path.GetFullPath(module);
                            if (!System.IO.Path.HasExtension(module))
                                module += ".js";
                            if (File.Exists(module))
                                RunExecInstantly(module, ParseAs(line, out string alias) ? alias : null, engine);
                            else if (File.Exists(orig))
                                RunExecInstantly(orig, null, engine);
                            else
                            {
                                orig = RemoveStartEnd(GetAfter(line, "from").Trim());
                                module = System.IO.Path.Combine(Main.CustomTagsPath, orig);
                                module = System.IO.Path.GetFullPath(module);
                                if (!System.IO.Path.HasExtension(module))
                                    module += ".js";
                                if (File.Exists(module))
                                    RunExecInstantly(module, ParseAs(line, out string alias) ? alias : null, engine);
                                else if (File.Exists(orig))
                                    RunExecInstantly(orig, null, engine);
                            }
                            continue;
                        }
                        sb.AppendLine(line);
                    }
                    return new StringReader(sb.ToString());
                }
            }
            static void RunExecInstantly(string path, string alias, ScriptEngine engine)
            {
                var js = File.ReadAllText(path);
                var src = new TextSource(js, engine);
                if (src.IsProxy)
                {
                    engine.SetGlobalValue(alias ?? src.ProxyType.Name, src.ProxyType);
                    SetNestedTypesRecursive(engine, src.ProxyType);
                }
                else CompileExec(src.str, engine)?.Invoke();
            }
            static void SetNestedTypesRecursive(ScriptEngine engine, Type type)
            {
                string ns = type.Namespace + "." ?? "";
                foreach (Type nType in type.GetNestedTypes())
                {
                    engine.SetGlobalValue(type.FullName.Replace(ns, "").Replace("+", "."), nType);
                    SetNestedTypesRecursive(engine, nType);
                }
            }
            static bool ParseAs(string braces, out string alias)
            {
                braces = GetBetween(braces, "{", "}").Trim();
                string[] asExpr = braces.Split(new string[] { "as" }, StringSplitOptions.None);
                alias = asExpr.Last();
                return alias != braces;
            }
            static string GetBetween(string str, string first, string second)
            {
                int fIndex = str.IndexOf(first);
                if (fIndex < 0) return null;
                int sIndex = str.IndexOf(second);
                if (sIndex < 0) return null;
                return str.Substring(fIndex, sIndex - fIndex);
            }
            static string GetAfter(string str, string after)
            {
                int index = str.IndexOf(after);
                if (index < 0) return null;
                return str.Remove(0, index + after.Length);
            }
            static string RemoveStartEnd(string str)
            {
                str = str.Remove(0, 1);
                return str.Remove(str.Length - 1, 1);
            }
        }
    }
    public class UDFWrapper
    {
        public UDFWrapper(UserDefinedFunction udf)
        {
            this.udf = udf;
            fd = (FunctionMethodGenerator.FunctionDelegate)udf.GeneratedMethod.GeneratedDelegate;
        }
        public readonly UserDefinedFunction udf;
        readonly FunctionMethodGenerator.FunctionDelegate fd;
        public object Call(object @this, params object[] arguments)
        {
            var context = ExecutionContext.CreateFunctionContext(
                engine: udf.Engine,
                parentScope: udf.ParentScope,
                thisValue: @this,
                executingFunction: udf);
            return fd(context, arguments);
        }
        public object Call(params object[] arguments)
        {
            var context = ExecutionContext.CreateFunctionContext(
                engine: udf.Engine,
                parentScope: udf.ParentScope,
                thisValue: udf.Prototype ?? (object)Undefined.Value,
                executingFunction: udf);
            return fd(context, arguments);
        }
        public object CallGlobal(params object[] arguments)
        {
            var context = ExecutionContext.CreateFunctionContext(
                engine: udf.Engine,
                parentScope: udf.ParentScope,
                thisValue: udf.Engine.Global,
                executingFunction: udf);
            return fd(context, arguments);
        }
        public static readonly MethodInfo CallMethod = typeof(UDFWrapper).GetMethod("Call", new[] { typeof(object[]) });
        public static readonly MethodInfo CallThisMethod = typeof(UDFWrapper).GetMethod("Call", new[] { typeof(object), typeof(object[]) });
        public static readonly MethodInfo CallGlobalMethod = typeof(UDFWrapper).GetMethod("CallGlobal");
    }
}

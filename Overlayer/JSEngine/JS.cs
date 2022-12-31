using System;
using System.Text;
using System.IO;
using JSEngine.Compiler;
using JSEngine.Library;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using ILGenerator = System.Reflection.Emit.ILGenerator;

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
        public static Func<object> CompileEval(this ScriptEngine engine, string js, string importPath = "")
        {
            var source = new TextSource(js, engine, importPath);
            if (source.IsProxy)
            {
                var pType = source.ProxyType;
                engine.SetGlobalValue(pType.Name, pType);
                return null;
            }
            var scr = CompiledEval.Compile(source, Option);
            return () => scr.EvaluateFastInternal(engine);
        }
        public static Action CompileExec(this ScriptEngine engine, string js, string importPath = "")
        {
            var source = new TextSource(js, engine, importPath);
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
            public string ImportPath { get; set; }
            public TextSource(string str, ScriptEngine engine, string importPath = "")
            {
                this.str = str;
                ImportPath = importPath;
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
                            var orig = RemoveStartEnd(GetAfter(line, "from").Trim());
                            var module = System.IO.Path.Combine(ImportPath, orig);
                            module = System.IO.Path.GetFullPath(module);
                            if (!System.IO.Path.HasExtension(module))
                                module += ".js";
                            if (File.Exists(module))
                                RunExecInstantly(module, ParseAs(line, out string alias) ? alias : null, engine);
                            else if (File.Exists(orig))
                                RunExecInstantly(orig, null, engine);
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
                else engine.CompileExec(src.str)?.Invoke();
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
        public static readonly AssemblyBuilder ass;
        public static readonly ModuleBuilder mod;
        public static int TypeCount { get; internal set; }
        static JS()
        {
            var assName = new AssemblyName("JSModManager.Patches");
            ass = AssemblyBuilder.DefineDynamicAssembly(assName, AssemblyBuilderAccess.Run);
            mod = ass.DefineDynamicModule(assName.Name);
        }
        public static LocalBuilder MakeArray<T>(this ILGenerator il, int length)
        {
            LocalBuilder array = il.DeclareLocal(typeof(T[]));
            il.Emit(OpCodes.Ldc_I4, length);
            il.Emit(OpCodes.Newarr, typeof(T));
            il.Emit(OpCodes.Stloc, array);
            return array;
        }
        static ParameterInfo[] SelectActualParams(MethodBase m, ParameterInfo[] p, string[] n)
        {
            Type dType = m.DeclaringType;
            List<ParameterInfo> pList = new List<ParameterInfo>();
            for (int i = 0; i < n.Length; i++)
            {
                int index = Array.FindIndex(p, pa => pa.Name == n[i]);
                if (index > 0)
                    pList.Add(p[index]);
                else
                {
                    string s = n[i];
                    switch (s)
                    {
                        case "__instance":
                            pList.Add(new CustomParameter(dType, s));
                            break;
                        case "__originalMethod":
                            pList.Add(new CustomParameter(typeof(MethodBase), s));
                            break;
                        case "__args":
                            pList.Add(new CustomParameter(typeof(MethodBase), s));
                            break;
                        case "__result":
                            pList.Add(new CustomParameter(m is MethodInfo mi ? mi.ReturnType : typeof(object), s));
                            break;
                        case "__exception":
                            pList.Add(new CustomParameter(typeof(Exception), s));
                            break;
                        case "__runOriginal":
                            pList.Add(new CustomParameter(typeof(bool), s));
                            break;
                        default:
                            if (s.StartsWith("__"))
                            {
                                if (int.TryParse(s.Substring(0, 2), out int num))
                                {
                                    if (num < 0 || num >= p.Length)
                                        return null;
                                    pList.Add(new CustomParameter(p[num].ParameterType, s));
                                }
                                else return null;
                            }
                            else if (s.StartsWith("___"))
                            {
                                string name = s.Substring(0, 3);
                                FieldInfo field = dType.GetField(name, AccessTools.all);
                                if (field == null)
                                    return null;
                            }
                            break;
                    }
                }
            }
            return pList.ToArray();
        }
        public static MethodInfo Wrap(this UserDefinedFunction udf, MethodBase target, bool rtIsBool)
        {
            if (udf == null) return null;
            UDFWrapper holder = new UDFWrapper(udf);
            TypeBuilder type = mod.DefineType(TypeCount++.ToString(), TypeAttributes.Public);
            ParameterInfo[] parameters = SelectActualParams(target, target.GetParameters(), udf.ArgumentNames.ToArray());
            if (parameters == null) return null;
            Type[] paramTypes = parameters.Select(p => p.ParameterType).ToArray();
            MethodBuilder methodB = type.DefineMethod("Wrapper", MethodAttributes.Public | MethodAttributes.Static, rtIsBool ? typeof(bool) : typeof(void), paramTypes);
            FieldBuilder holderfld = type.DefineField("holder", typeof(UDFWrapper), FieldAttributes.Public | FieldAttributes.Static);

            var il = methodB.GetILGenerator();
            LocalBuilder arr = il.DeclareLocal(typeof(object[]));
            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc, arr);

            int paramIndex = 1;
            foreach (ParameterInfo param in parameters)
            {
                Type pType = param.ParameterType;
                udf.Engine.SetGlobalValue(pType.Name, pType);
                methodB.DefineParameter(paramIndex++, ParameterAttributes.None, param.Name);
                int pIndex = paramIndex - 2;
                il.Emit(OpCodes.Ldloc, arr);
                il.Emit(OpCodes.Ldc_I4, pIndex);
                il.Emit(OpCodes.Ldarg, pIndex);
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.Emit(OpCodes.Ldsfld, holderfld);
            il.Emit(OpCodes.Ldloc, arr);
            il.Emit(OpCodes.Call, UDFWrapper.CallMethod);
            if (rtIsBool)
                il.Emit(OpCodes.Call, istrue);
            else il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);

            Type t = type.CreateType();
            t.GetField("holder").SetValue(null, holder);
            return t.GetMethod("Wrapper");
        }
        public static bool IsTrue(object obj) => obj == null || obj.Equals(true);
        static readonly MethodInfo istrue = typeof(JS).GetMethod("IsTrue", AccessTools.all);
    }
    public class CustomParameter : ParameterInfo
    {
        public CustomParameter(Type type, string name)
        {
            ClassImpl = type;
            NameImpl = name;
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

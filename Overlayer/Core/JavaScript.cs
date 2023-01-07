using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JSEngine;
using JSEngine.CustomLibrary;
using JSEngine.Library;
using System.Reflection.Emit;
using Overlayer.Core.Utils;
using System.IO;

namespace Overlayer.Core
{
    /// <summary>
    /// Powered By <see href="https://github.com/paulbartrum/jurassic">Jurassic</see>
    /// </summary>
    public static class JavaScript
    {
        public static void Init()
        {
            foreach (Type type in JSTypes)
            {
                JSUtils.BuildProxy(type, Main.CustomTagsPath, buildNestedTypes: true);
                JSUtils.BuildProxy(type, Main.InitJSPath, buildNestedTypes: true);
            }
        }
        static readonly List<Type> JSTypes = new List<Type>();
        public static void RegisterType(params Type[] ts) => JSTypes.AddRange(ts);
        static ScriptEngine PrepareEngine()
        {
            var engine = new ScriptEngine();
            engine.EnableExposedClrTypes = true;
            foreach (var tag in Main.AllTags)
                engine.SetGlobalFunction(tag.Name, tag.GetterDelegate);
            engine.SetGlobalValue("KeyCode", new Kcde(engine));
            engine.SetGlobalValue("Input", new Ipt(engine));
            engine.SetGlobalValue("Overlayer", new Ovlr(engine));
            engine.SetGlobalValue("Sprite", new Sprite(engine));
            engine.SetGlobalValue("Vector2", new Vector2Constructor(engine));
            engine.SetGlobalValue("GameObject", new GameObjectConstructor(engine));
            engine.SetGlobalValue("Color", new ColorConstructor(engine));
            engine.SetGlobalValue("HSV", new HSVConstructor(engine));
            engine.SetGlobalValue("Planet", new PlanetConstructor(engine));
            engine.SetGlobalValue("PlanetType", new PT(engine));
            engine.SetGlobalValue("Time", new Time(engine));
            engine.SetGlobalValue("Judgement", new Judgement(engine));

            engine.SetGlobalValue("Tile", new TileConstructor(engine));
            engine.SetGlobalValue("tiles", new TilesConstructor(engine).Construct());
            return engine;
        }
        public static Func<object> CompileEval(this string js) => js.CompileEval(PrepareEngine());
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
        public static MethodInfo Wrap(this FunctionInstance func, MethodBase target, bool rtIsBool)
        {
            UserDefinedFunction udf = func as UserDefinedFunction;
            if (udf == null) return null;
            UDFWrapper holder = new UDFWrapper(udf);

            TypeBuilder type = PatchUtils.mod.DefineType(PatchUtils.TypeCount++.ToString(), TypeAttributes.Public);
            ParameterInfo[] parameters = SelectActualParams(target, target.GetParameters(), udf.ArgumentNames.ToArray());
            if (parameters == null) return null;
            Type[] paramTypes = parameters.Select(p => p.ParameterType).ToArray();
            MethodBuilder methodB = type.DefineMethod("Wrapper", MethodAttributes.Public | MethodAttributes.Static, rtIsBool ? typeof(bool) : typeof(void), paramTypes);
            FieldBuilder holderfld = type.DefineField("holder", typeof(UDFWrapper), FieldAttributes.Public | FieldAttributes.Static);

            var il = methodB.GetILGenerator();
            LocalBuilder arr = il.MakeArray<object>(parameters.Length);
            int paramIndex = 1;
            foreach (ParameterInfo param in parameters)
            {
                Type pType = param.ParameterType;
                PatchUtils.IgnoreAccessCheck(pType);
                udf.Engine.SetGlobalValue(pType.Name, pType);
                methodB.DefineParameter(paramIndex++, ParameterAttributes.None, param.Name);
                int pIndex = paramIndex - 2;
                Main.Logger.Log($"pIndex:{pIndex}, pType:{pType}");
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
        public static bool IsTrue(object obj)
            => obj == null || obj.Equals(true);
        static readonly MethodInfo istrue = typeof(JavaScript).GetMethod("IsTrue", AccessTools.all);
        class CustomParameter : ParameterInfo
        {
            public CustomParameter(Type type, string name)
            {
                ClassImpl = type;
                NameImpl = name;
            }
        }
    }
}

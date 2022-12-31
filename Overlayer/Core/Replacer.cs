using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace Overlayer.Core
{
    public class Replacer
    {
        bool compiled;
        string str = "";
        HashSet<char> tagOpenChars;
        Func<string> compiledResult;
        List<Tag> tags;
        public string Source
        {
            get => str;
            set
            {
                str = value;
                compiled = false;
            }
        }
        public List<Tag> Tags => tags;
        public Replacer(List<Tag> tags = null)
        {
            tagOpenChars = new HashSet<char>();
            this.tags = tags ?? new List<Tag>();
        }
        public Replacer(string str, List<Tag> tags = null) : this(tags) => Source = str;
        public string Replace()
        {
            Compile();
            return compiledResult();
        }
        void Compile()
        {
            if (compiled) return;
            DynamicMethod result = new DynamicMethod("", typeof(string), Type.EmptyTypes, typeof(Replacer), true);
            ILGenerator il = result.GetILGenerator();
            StringBuilder stack = new StringBuilder();
            List<object> emits = new List<object>();
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                stack.Append(c);
                if (tagOpenChars.Contains(c))
                {
                    var info = ParseTag(c, ref i);
                    if (info != null)
                    {
                        stack.Remove(stack.Length - 1, 1);
                        emits.Add(stack.ToString());
                        emits.Add(info);
                        stack.Clear();
                        i++;
                    }
                }
            }
            emits.Add(stack.ToString());
            int arrIndex = 0;
            il.Emit(OpCodes.Ldc_I4, emits.Count);
            il.Emit(OpCodes.Newarr, typeof(string));
            foreach (object emit in emits)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, arrIndex++);
                if (emit is string str)
                    il.Emit(OpCodes.Ldstr, str);
                if (emit is TagInfo info)
                {
                    if (info.option != null)
                    {
                        il.Emit(OpCodes.Ldstr, info.option);
                        if (info.tag.OptionConverter != null)
                            il.Emit(OpCodes.Call, info.tag.OptionConverter);
                    }
                    else
                    {
                        if (info.tag.OptionConverter != null)
                            il.Emit(OpCodes.Ldc_I4_0);
                        else il.Emit(OpCodes.Ldnull);
                    }
                    il.Emit(OpCodes.Call, info.tag.Getter);
                }
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.Emit(OpCodes.Call, Concats);
            il.Emit(OpCodes.Ret);
            compiledResult = (Func<string>)result.CreateDelegate(typeof(Func<string>));
            compiled = true;
        }
        TagInfo ParseTag(char open, ref int index)
        {
            var t = tags.Where(tag => tag.Open == open);
            if (!t.Any()) return null;
            foreach (Tag tag in t)
            {
                int closeIndex = str.IndexOf(tag.Close, index);
                if (closeIndex < 0) continue;
                string subStr = str.Substring(index + 1, closeIndex - index - 1);
                string[] nameOpt = subStr.Split(tag.Separator);
                if (nameOpt[0] != tag.Name) continue;
                index += closeIndex - index - 1;
                return new TagInfo(tag, nameOpt.Length < 2 ? null : nameOpt[1]);
            }
            return null;
        }
        public Tag CreateTag(string name, char open = '{', char close = '}', char separator = ':') => new Tag(this, name, open, close, separator);
        public class Tag
        {
            public readonly string Name;
            public readonly char Open;
            public readonly char Close;
            public readonly char Separator;
            readonly List<Thread> Threads;
            readonly Replacer Replacer;
            readonly Harmony Harmony;
            readonly List<(MethodBase, Delegate)> Prefixes;
            readonly List<(MethodBase, Delegate)> Postfixes;
            readonly List<(MethodBase, Delegate)> Transpilers;
            readonly List<(MethodBase, Delegate)> Finalizers;
            public MethodInfo Getter { get; private set; }
            public MethodInfo OptionConverter { get; private set; }
            public Tag(Replacer replacer, string name, char open, char close, char separator)
            {
                Replacer = replacer;
                Name = name;
                Open = open;
                Close = close;
                Separator = separator;
                OptionConverter = null;
                Harmony = new Harmony($"{Open}{Name}{Close}_Harmony");
                Threads = new List<Thread>();
                Prefixes = new List<(MethodBase, Delegate)>();
                Postfixes = new List<(MethodBase, Delegate)>();
                Transpilers = new List<(MethodBase, Delegate)>();
                Finalizers = new List<(MethodBase, Delegate)>();
            }
            public Tag SetGetter(Func<string, string> getter)
            {
                Getter = Wrapper.Wrap(getter);
                return this;
            }
            public Tag SetGetter(MethodInfo getter)
            {
                if (CheckGetterSig(getter))
                    throw new InvalidOperationException("Return Type Must Be String Or Parameter's Length Must Be 1.");
                Type pType = getter.GetParameters()[0].ParameterType;
                if (pType != typeof(string))
                {
                    OptionConverter = StringToNumber.GetConverter(pType);
                    if (OptionConverter == null)
                        throw new NotSupportedException($"Option Type:{pType} Is Not Supported.");
                }
                Getter = getter;
                return this;
            }
            public Tag AddThread(ThreadStart thread)
            {
                Threads.Add(new Thread(thread));
                return this;
            }
            public Tag AddThread(ParameterizedThreadStart thread)
            {
                Threads.Add(new Thread(thread));
                return this;
            }
            public Tag Prefix(MethodBase target, Delegate prefix)
            {
                Prefixes.Add((target, prefix));
                return this;
            }
            public Tag Postfix(MethodBase target, Delegate prefix)
            {
                Postfixes.Add((target, prefix));
                return this;
            }
            public Tag Transpiler(MethodBase target, Delegate prefix)
            {
                Transpilers.Add((target, prefix));
                return this;
            }
            public Tag Finalizer(MethodBase target, Delegate prefix)
            {
                Finalizers.Add((target, prefix));
                return this;
            }
            public Replacer Build()
            {
                if (Getter == null)
                    throw new InvalidOperationException("Cannot Build Without Set Getter!");
                Prefixes.ForEach(i => Harmony.Patch(i.Item1, prefix: new HarmonyMethod(Wrapper.Wrap(i.Item2))));
                Postfixes.ForEach(i => Harmony.Patch(i.Item1, postfix: new HarmonyMethod(Wrapper.Wrap(i.Item2))));
                Transpilers.ForEach(i => Harmony.Patch(i.Item1, transpiler: new HarmonyMethod(Wrapper.Wrap(i.Item2))));
                Finalizers.ForEach(i => Harmony.Patch(i.Item1, finalizer: new HarmonyMethod(Wrapper.Wrap(i.Item2))));
                Threads.ForEach(t => t.Start(this));
                Replacer.tags.Add(this);
                Replacer.tagOpenChars.Add(Open);
                return Replacer;
            }
            public override string ToString() => $"{Open}{Name}{Close}";
            public static bool CheckGetterSig(MethodInfo method)
            {
                var prms = method.GetParameters();
                return method.ReturnType == typeof(string) && prms.Length == 1;
            }
        }
        public class TagInfo
        {
            public Tag tag;
            public string option;
            public TagInfo(Tag tag, string option)
            {
                this.tag = tag;
                this.option = option;
            }
        }
        public static class Wrapper
        {
            public static readonly AssemblyBuilder ass;
            public static readonly ModuleBuilder mod;
            public static int TypeCount { get; internal set; }
            static Wrapper()
            {
                var assName = new AssemblyName("Wrapper");
                ass = AssemblyBuilder.DefineDynamicAssembly(assName, AssemblyBuilderAccess.Run);
                mod = ass.DefineDynamicModule(assName.Name);
            }
            public static MethodInfo Wrap<T>(T del) where T : Delegate
            {
                Type delType = del.GetType();
                MethodInfo invoke = delType.GetMethod("Invoke");
                MethodInfo method = del.Method;
                TypeBuilder type = mod.DefineType(TypeCount++.ToString(), TypeAttributes.Public);
                ParameterInfo[] parameters = method.GetParameters();
                Type[] paramTypes = parameters.Select(p => p.ParameterType).ToArray();
                MethodBuilder methodB = type.DefineMethod("Wrapper", MethodAttributes.Public | MethodAttributes.Static, invoke.ReturnType, paramTypes);
                FieldBuilder delField = type.DefineField("function", delType, FieldAttributes.Public | FieldAttributes.Static);
                ILGenerator il = methodB.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, delField);
                int paramIndex = 1;
                foreach (ParameterInfo param in parameters)
                {
                    methodB.DefineParameter(paramIndex++, ParameterAttributes.None, param.Name);
                    il.Emit(OpCodes.Ldarg, paramIndex - 2);
                }
                il.Emit(OpCodes.Call, invoke);
                il.Emit(OpCodes.Ret);
                Type t = type.CreateType();
                t.GetField("function").SetValue(null, del);
                return t.GetMethod("Wrapper");
            }
        }
        public static class StringToNumber
        {
            public static unsafe sbyte ToInt8(string s)
            {
                sbyte result = 0;
                bool unary = s[0] == 45;
                fixed (char* v = s)
                {
                    char* c = v;
                    if (unary) c++;
                    while (*c != '\0')
                    {
                        result = (sbyte)(10 * result + (*c - 48));
                        c++;
                    }
                }
                if (unary)
                    return (sbyte)-result;
                return result;
            }
            public static unsafe short ToInt16(string s)
            {
                short result = 0;
                bool unary = s[0] == 45;
                fixed (char* v = s)
                {
                    char* c = v;
                    if (unary) c++;
                    while (*c != '\0')
                    {
                        result = (short)(10 * result + (*c - 48));
                        c++;
                    }
                }
                if (unary)
                    return (short)-result;
                return result;
            }
            public static unsafe int ToInt32(string s)
            {
                int result = 0;
                bool unary = s[0] == 45;
                fixed (char* v = s)
                {
                    char* c = v;
                    if (unary) c++;
                    while (*c != '\0')
                    {
                        result = 10 * result + (*c - 48);
                        c++;
                    }
                }
                if (unary)
                    return -result;
                return result;
            }
            public static unsafe long ToInt64(string s)
            {
                long result = 0;
                bool unary = s[0] == 45;
                fixed (char* v = s)
                {
                    char* c = v;
                    if (unary) c++;
                    while (*c != '\0')
                    {
                        result = 10 * result + (*c - 48);
                        c++;
                    }
                }
                if (unary)
                    return -result;
                return result;
            }
            public static unsafe byte ToUInt8(string s)
            {
                byte result = 0;
                fixed (char* v = s)
                {
                    char* c = v;
                    while (*c != '\0')
                    {
                        result = (byte)(10 * result + (*c - 48));
                        c++;
                    }
                }
                return result;
            }
            public static unsafe ushort ToUInt16(string s)
            {
                ushort result = 0;
                fixed (char* v = s)
                {
                    char* c = v;
                    while (*c != '\0')
                    {
                        result = (ushort)(10 * result + (*c - 48));
                        c++;
                    }
                }
                return result;
            }
            public static unsafe uint ToUInt32(string s)
            {
                uint result = 0;
                fixed (char* v = s)
                {
                    char* c = v;
                    while (*c != '\0')
                    {
                        result = (uint)(10 * result + (*c - 48));
                        c++;
                    }
                }
                return result;
            }
            public static unsafe ulong ToUInt64(string s)
            {
                ulong result = 0;
                fixed (char* v = s)
                {
                    char* c = v;
                    while (*c != '\0')
                    {
                        result = 10 * result + (*c - 48ul);
                        c++;
                    }
                }
                return result;
            }
            public static unsafe double ToDouble(string s)
            {
                double result = 0;
                bool isDot = false;
                int dCount = 1;
                bool unary = s[0] == 45;
                fixed (char* v = s)
                {
                    char* c = v;
                    if (unary) c++;
                    while (*c != '\0')
                    {
                        if (*c == '.')
                        {
                            isDot = true;
                            goto Continue;
                        }
                        if (!isDot)
                            result = 10 * result + (*c - 48);
                        else result += (*c - 48) / dPow[dCount++];
                        Continue:
                        c++;
                    }
                }
                if (unary)
                    return -result;
                return result;
            }
            public static unsafe float ToFloat(string s)
            {
                float result = 0;
                bool isDot = false;
                int dCount = 1;
                bool unary = s[0] == 45;
                fixed (char* v = s)
                {
                    char* c = v;
                    if (unary) c++;
                    while (*c != '\0')
                    {
                        if (*c == '.')
                        {
                            isDot = true;
                            goto Continue;
                        }
                        if (!isDot)
                            result = 10 * result + (*c - 48);
                        else result += (*c - 48) / fPow[dCount++];
                        Continue:
                        c++;
                    }
                }
                if (unary)
                    return -result;
                return result;
            }
            private static readonly double[] dPow = GetDoublePow();
            private static double[] GetDoublePow()
            {
                var max = 309;
                var exps = new double[max];
                for (var i = 0; i < max; i++)
                    exps[i] = Math.Pow(10, i);
                return exps;
            }
            private static readonly float[] fPow = GetFloatPow();
            private static float[] GetFloatPow()
            {
                var max = 39;
                var exps = new float[max];
                for (var i = 0; i < max; i++)
                    exps[i] = (float)Math.Pow(10, i);
                return exps;
            }
            public static MethodInfo GetConverter(Type numType)
            {
                if (numType == typeof(sbyte)) return Int8;
                else if (numType == typeof(short)) return Int16;
                else if (numType == typeof(int)) return Int32;
                else if (numType == typeof(long)) return Int64;
                else if (numType == typeof(byte)) return UInt8;
                else if (numType == typeof(ushort)) return UInt16;
                else if (numType == typeof(uint)) return UInt32;
                else if (numType == typeof(ulong)) return UInt64;
                else if (numType == typeof(float)) return Float;
                else if (numType == typeof(double)) return Double;
                else return null;
            }
            public static readonly MethodInfo Int8 = typeof(StringToNumber).GetMethod("ToInt8");
            public static readonly MethodInfo Int16 = typeof(StringToNumber).GetMethod("ToInt16");
            public static readonly MethodInfo Int32 = typeof(StringToNumber).GetMethod("ToInt32");
            public static readonly MethodInfo Int64 = typeof(StringToNumber).GetMethod("ToInt64");
            public static readonly MethodInfo UInt8 = typeof(StringToNumber).GetMethod("ToUInt8");
            public static readonly MethodInfo UInt16 = typeof(StringToNumber).GetMethod("ToUInt16");
            public static readonly MethodInfo UInt32 = typeof(StringToNumber).GetMethod("ToUInt32");
            public static readonly MethodInfo UInt64 = typeof(StringToNumber).GetMethod("ToUInt64");
            public static readonly MethodInfo Float = typeof(StringToNumber).GetMethod("ToFloat");
            public static readonly MethodInfo Double = typeof(StringToNumber).GetMethod("ToDouble");
        }
        public static readonly MethodInfo Concats = typeof(string).GetMethod("Concat", new[] { typeof(string[]) });
    }
}

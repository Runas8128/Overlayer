using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using System.Collections.Generic;
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
                compiled = false;
                str = value;
            }
        }
        public HashSet<Tag> References { get; private set; }
        public List<Tag> Tags => tags;
        public Replacer(List<Tag> tags = null)
        {
            this.tags = tags ?? new List<Tag>();
            References = new HashSet<Tag>();
            tagOpenChars = tags != null ? tags.Select(t => t.Open).Distinct().ToHashSet() : new HashSet<char>();
        }
        public Replacer(string str, List<Tag> tags = null) : this(tags) => Source = str;
        public string Replace()
        {
            Compile();
            return compiledResult();
        }
        public Replacer Compile()
        {
            if (compiled && compiledResult != null) return this;
            References = new HashSet<Tag>();
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
                        References.Add(info.tag);
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
                    if (info.tag.HasOption)
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
                            {
                                var param = info.tag.Getter.GetParameters()[0];
                                if (param.DefaultValue != DBNull.Value)
                                {
                                    switch (Type.GetTypeCode(param.ParameterType))
                                    {
                                        case TypeCode.Boolean:
                                            il.Emit(OpCodes.Ldc_I4, (bool)param.DefaultValue ? 1 : 0);
                                            break;
                                        case TypeCode.SByte:
                                        case TypeCode.Byte:
                                        case TypeCode.Int16:
                                        case TypeCode.UInt16:
                                        case TypeCode.Int32:
                                        case TypeCode.UInt32:
                                            il.Emit(OpCodes.Ldc_I4, (int)param.DefaultValue);
                                            break;
                                        case TypeCode.Int64:
                                        case TypeCode.UInt64:
                                            il.Emit(OpCodes.Ldc_I8, (long)param.DefaultValue);
                                            break;
                                        case TypeCode.Single:
                                            il.Emit(OpCodes.Ldc_R4, (float)param.DefaultValue);
                                            break;
                                        case TypeCode.Double:
                                            il.Emit(OpCodes.Ldc_R8, (double)param.DefaultValue);
                                            break;
                                        default:
                                            var pType = param.ParameterType;
                                            if (pType.IsValueType)
                                            {
                                                LocalBuilder defValue = il.DeclareLocal(pType);
                                                il.Emit(OpCodes.Ldloca, defValue);
                                                il.Emit(OpCodes.Initobj, defValue.LocalType);
                                                il.Emit(OpCodes.Ldloc);
                                            }
                                            else il.Emit(OpCodes.Ldnull);
                                            break;
                                    }
                                }
                                else
                                {
                                    var pType = param.ParameterType;
                                    if (pType.IsValueType)
                                    {
                                        LocalBuilder defValue = il.DeclareLocal(pType);
                                        il.Emit(OpCodes.Ldloca, defValue);
                                        il.Emit(OpCodes.Initobj, defValue.LocalType);
                                        il.Emit(OpCodes.Ldloc);
                                    }
                                    else il.Emit(OpCodes.Ldnull);
                                }
                            }
                            else il.Emit(OpCodes.Ldnull);
                        }
                    }
                    il.Emit(OpCodes.Call, info.tag.Getter);
                    if (info.tag.ReturnConverter != null)
                        il.Emit(OpCodes.Call, info.tag.ReturnConverter);
                }
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.Emit(OpCodes.Call, Concats);
            il.Emit(OpCodes.Ret);
            compiledResult = (Func<string>)result.CreateDelegate(typeof(Func<string>));
            compiled = true;
            TagManager.UpdateReference();
            return this;
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
                string[] nameOpt = subStr.Split(new char[] { tag.Separator }, 2);
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
            public MethodInfo Getter { get; private set; }
            public Delegate GetterDelegate { get; private set; }
            public MethodInfo OptionConverter { get; private set; }
            public MethodInfo ReturnConverter { get; private set; }
            public bool HasOption { get; private set; }
            // For CustomTag
            public string SourcePath = null;
            public Tag(Replacer replacer, string name, char open, char close, char separator)
            {
                Replacer = replacer;
                Name = name;
                Open = open;
                Close = close;
                Separator = separator;
                OptionConverter = null;
                Threads = new List<Thread>();
            }
            public Tag SetGetter(Func<string, string> getter)
            {
                Getter = Wrapper.Wrap(getter);
                GetterDelegate = getter;
                HasOption = true;
                return this;
            }
            public Tag SetGetter(Delegate getter)
            {
                var invoke = getter.GetType().GetMethod("Invoke");
                if (!CheckGetterSig(invoke))
                    throw new InvalidOperationException($"Parameter's Length Must Be Less Than 2. ({getter})");
                var prms = invoke.GetParameters();
                HasOption = prms.Length == 1;
                if (HasOption)
                {
                    Type pType = invoke.GetParameters()[0].ParameterType;
                    if (pType != typeof(string))
                    {
                        OptionConverter = StringConverter.GetToConverter(pType);
                        if (OptionConverter == null)
                            throw new NotSupportedException($"Option Type:{pType} Is Not Supported.");
                    }
                }
                if (invoke.ReturnType != typeof(string))
                    ReturnConverter = StringConverter.GetFromConverter(invoke.ReturnType);
                Getter = Wrapper.Wrap(getter);
                GetterDelegate = getter;
                return this;
            }
            public Tag SetGetter(MethodInfo getter)
            {
                if (!CheckGetterSig(getter))
                    throw new InvalidOperationException($"Parameter's Length Must Be Less Than 2. ({getter})");
                var prms = getter.GetParameters();
                HasOption = prms.Length == 1;
                if (HasOption)
                {
                    Type pType = getter.GetParameters()[0].ParameterType;
                    if (pType != typeof(string))
                    {
                        OptionConverter = StringConverter.GetToConverter(pType);
                        if (OptionConverter == null)
                            throw new NotSupportedException($"Option Type:{pType} Is Not Supported.");
                    }
                }
                if (getter.ReturnType != typeof(string))
                    ReturnConverter = StringConverter.GetFromConverter(getter.ReturnType);
                Getter = getter;
                if (HasOption)
                    GetterDelegate = getter.CreateDelegate(Expression.GetFuncType(prms[0].ParameterType, getter.ReturnType));
                else GetterDelegate = getter.CreateDelegate(Expression.GetFuncType(getter.ReturnType));
                return this;
            }
            public Tag AddThread(ThreadStart thread)
            {
                Threads.Add(new Thread(thread));
                return this;
            }
            public Tag AddThread(MethodInfo thread)
            {
                if (!CheckThreadSig(thread))
                    throw new InvalidOperationException("ReturnType Must Be Void And Parameter's Length Must Be 0.");
                Threads.Add(new Thread((ThreadStart)thread.CreateDelegate(typeof(ThreadStart))));
                return this;
            }
            public Replacer Build()
            {
                if (Getter == null)
                    throw new InvalidOperationException("Cannot Build Without Set Getter!");
                Threads.ForEach(t => t.Start());
                Replacer.tags.Add(this);
                Replacer.tagOpenChars.Add(Open);
                return Replacer;
            }
            public Tag Copy()
            {
                Tag tag = new Tag(Replacer, Name, Open, Close, Separator);
                tag.Getter = Getter;
                tag.GetterDelegate = GetterDelegate;
                tag.OptionConverter = OptionConverter;
                tag.ReturnConverter = ReturnConverter;
                Threads.ForEach(tag.Threads.Add);
                return tag;
            }
            public override string ToString() => $"{Open}{Name}{Close}";
            public static bool CheckGetterSig(MethodInfo method) => method.GetParameters().Length < 2;
            public static bool CheckThreadSig(MethodInfo method)
            {
                if (method.ReturnType != typeof(void))
                    return false;
                var prms = method.GetParameters();
                if (prms.Length < 1) return true;
                else return false;
            }
        }
        class TagInfo
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
        public static class StringConverter
        {
            public static unsafe sbyte ToInt8(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromInt8(sbyte s) => s.ToString();
            public static unsafe short ToInt16(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromInt16(short s) => s.ToString();
            public static unsafe int ToInt32(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromInt32(int s) => s.ToString();
            public static unsafe long ToInt64(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromInt64(long s) => s.ToString();
            public static unsafe byte ToUInt8(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromUInt8(byte s) => s.ToString();
            public static unsafe ushort ToUInt16(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromUInt16(ushort s) => s.ToString();
            public static unsafe uint ToUInt32(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromUInt32(uint s) => s.ToString();
            public static unsafe ulong ToUInt64(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromUInt64(ulong s) => s.ToString();
            public static unsafe double ToDouble(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromDouble(double s) => s.ToString();
            public static unsafe float ToFloat(string s)
            {
                if (s.Length == 0) return 0;
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
            public static string FromFloat(float s) => s.ToString();
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
            public static MethodInfo GetToConverter(Type numType)
            {
                if (numType == typeof(sbyte)) return TInt8;
                else if (numType == typeof(short)) return TInt16;
                else if (numType == typeof(int)) return TInt32;
                else if (numType == typeof(long)) return TInt64;
                else if (numType == typeof(byte)) return TUInt8;
                else if (numType == typeof(ushort)) return TUInt16;
                else if (numType == typeof(uint)) return TUInt32;
                else if (numType == typeof(ulong)) return TUInt64;
                else if (numType == typeof(float)) return TFloat;
                else if (numType == typeof(double)) return TDouble;
                else return null;
            }
            public static MethodInfo GetFromConverter(Type numType)
            {
                if (numType == typeof(sbyte)) return FInt8;
                else if (numType == typeof(short)) return FInt16;
                else if (numType == typeof(int)) return FInt32;
                else if (numType == typeof(long)) return FInt64;
                else if (numType == typeof(byte)) return FUInt8;
                else if (numType == typeof(ushort)) return FUInt16;
                else if (numType == typeof(uint)) return FUInt32;
                else if (numType == typeof(ulong)) return FUInt64;
                else if (numType == typeof(float)) return FFloat;
                else if (numType == typeof(double)) return FDouble;
                else return FObject;
            }
            public static string FromObject(object s) => s.ToString();
            public static readonly MethodInfo TInt8 = typeof(StringConverter).GetMethod("ToInt8");
            public static readonly MethodInfo TInt16 = typeof(StringConverter).GetMethod("ToInt16");
            public static readonly MethodInfo TInt32 = typeof(StringConverter).GetMethod("ToInt32");
            public static readonly MethodInfo TInt64 = typeof(StringConverter).GetMethod("ToInt64");
            public static readonly MethodInfo TUInt8 = typeof(StringConverter).GetMethod("ToUInt8");
            public static readonly MethodInfo TUInt16 = typeof(StringConverter).GetMethod("ToUInt16");
            public static readonly MethodInfo TUInt32 = typeof(StringConverter).GetMethod("ToUInt32");
            public static readonly MethodInfo TUInt64 = typeof(StringConverter).GetMethod("ToUInt64");
            public static readonly MethodInfo TFloat = typeof(StringConverter).GetMethod("ToFloat");
            public static readonly MethodInfo TDouble = typeof(StringConverter).GetMethod("ToDouble");
            public static readonly MethodInfo FInt8 = typeof(StringConverter).GetMethod("FromInt8");
            public static readonly MethodInfo FInt16 = typeof(StringConverter).GetMethod("FromInt16");
            public static readonly MethodInfo FInt32 = typeof(StringConverter).GetMethod("FromInt32");
            public static readonly MethodInfo FInt64 = typeof(StringConverter).GetMethod("FromInt64");
            public static readonly MethodInfo FUInt8 = typeof(StringConverter).GetMethod("FromUInt8");
            public static readonly MethodInfo FUInt16 = typeof(StringConverter).GetMethod("FromUInt16");
            public static readonly MethodInfo FUInt32 = typeof(StringConverter).GetMethod("FromUInt32");
            public static readonly MethodInfo FUInt64 = typeof(StringConverter).GetMethod("FromUInt64");
            public static readonly MethodInfo FFloat = typeof(StringConverter).GetMethod("FromFloat");
            public static readonly MethodInfo FDouble = typeof(StringConverter).GetMethod("FromDouble");
            public static readonly MethodInfo FObject = typeof(StringConverter).GetMethod("FromObject");
        }
        public static readonly MethodInfo Concats = typeof(string).GetMethod("Concat", new[] { typeof(string[]) });
    }
}

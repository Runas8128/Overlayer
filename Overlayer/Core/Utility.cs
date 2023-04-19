﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using UnityEngine.UI;
using UnityEngine;
using Random = System.Random;
using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using System.Runtime.InteropServices;
using static UnityModManagerNet.UnityModManager.UI;
using TMPro;
using Overlayer.Core.Translation;
using System.Diagnostics;

namespace Overlayer.Core
{
    public static class Utility
    {
        static Utility()
        {
            var assName = new AssemblyName("Overlayer.Utility");
            ass = AssemblyBuilder.DefineDynamicAssembly(assName, AssemblyBuilderAccess.Run);
            mod = ass.DefineDynamicModule(assName.Name);
            loadedAsss = AppDomain.CurrentDomain.GetAssemblies();
        }
        #region GUI
        public static void BeginIndent(float hIndent = 20f, float vIndent = 0f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(hIndent);
            GUILayout.BeginVertical();
            GUILayout.Space(vIndent);
        }
        public static void EndIndent()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        public static bool DrawColor(ref VertexGradient color, GUIStyle style = null, params GUILayoutOption[] option)
        {
            bool result = false;

            GUILayout.BeginHorizontal();
            GUILayout.Label(Main.Language[TranslationKeys.TopLeft]);
            GUILayout.Space(1);
            result |= DrawColor(ref color.topLeft, style, option);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Main.Language[TranslationKeys.TopRight]);
            GUILayout.Space(1);
            result |= DrawColor(ref color.topRight, style, option);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Main.Language[TranslationKeys.BottomLeft]);
            GUILayout.Space(1);
            result |= DrawColor(ref color.bottomLeft, style, option);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Main.Language[TranslationKeys.BottomRight]);
            GUILayout.Space(1);
            result |= DrawColor(ref color.bottomRight, style, option);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            return result;
        }
        public static bool DrawColor(ref Color color, GUIStyle style = null, params GUILayoutOption[] option)
        {
            float[] arr = new float[] { color.r, color.g, color.b, color.a };
            bool result = DrawColor(ref arr, style, option);
            if (result)
            {
                color.r = arr[0];
                color.g = arr[1];
                color.b = arr[2];
                color.a = arr[3];
            }
            return result;
        }
        public static bool DrawColor(ref float[] color, GUIStyle style = null, params GUILayoutOption[] option) 
            => DrawFloatMultiField(ref color, new string[]
            {
                "<color=#FF0000>R</color>",
                "<color=#00FF00>G</color>",
                "<color=#0000FF>B</color>",
                "A"
            }, style, option);
        public static bool DrawTextArea(ref string value, string label, GUIStyle style = null, params GUILayoutOption[] option)
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            GUILayout.Label(label, new GUILayoutOption[]  { GUILayout.ExpandWidth(false) });
            string text = GUILayout.TextArea(value, style ?? GUI.skin.textArea, option);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (text != value)
            {
                value = text;
                return true;
            }
            value = text;
            return false;
        }
        public static bool DrawTextField(ref string value, string label, GUIStyle style = null, params GUILayoutOption[] option)
        {
            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            GUILayout.Label(label, GUILayout.ExpandWidth(false));
            string text = GUILayout.TextField(value, style ?? GUI.skin.textArea, option);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (text != value)
            {
                value = text;
                return true;
            }
            value = text;
            return false;
        }
        public static bool DrawEnum<T>(string title, ref T @enum) where T : Enum
        {
            T[] values = (T[])Enum.GetValues(typeof(T));
            string[] names = values.Select(x => x.ToString()).ToArray();
            int selected = Array.IndexOf(values, @enum);
            bool result = PopupToggleGroup(ref selected, names, title);
            @enum = values[selected];
            return result;
        }
        public static bool RightToggle(bool value, string label, Action<bool> onChange = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            bool prev = value;
            value = GUILayout.Toggle(value, "");
            if (prev != value) onChange?.Invoke(value);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return value;
        }
        public static bool IsMouseHovering()
        {
            return Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition);
        }
        public static Color ShiftHue(Color color, float amount)
        {
            Color.RGBToHSV(color, out float hue, out _, out _);
            hue += amount;
            return Color.HSVToRGB(hue, 1, 1);
        }
        public static Color ShiftSaturation(Color color, float amount)
        {
            Color.RGBToHSV(color, out _, out float sat, out _);
            sat += amount;
            return Color.HSVToRGB(1, sat, 1);
        }
        public static Color ShiftValue(Color color, float amount)
        {
            Color.RGBToHSV(color, out _, out _, out float val);
            val += amount;
            return Color.HSVToRGB(1, 1, val);
        }
        #endregion
        #region Array
        public static R[] ActualElements<T, R>(this R[] array, T[] seed, Func<T, R, bool> selector)
        {
            R[] result = new R[array.Length];
            for (int i = 0; i < seed.Length; i++)
            {
                int index = Array.FindIndex(array, r => selector(seed[i], r));
                if (index > 0)
                    result = result.Add(array[index]);
            }
            return result.MakeTight();
        }
        public static T[] MakeTight<T>(this T[] array)
        {
            int index = -1, arrlen = array.Length;
            for (int i = 0; i < arrlen; i++)
            {
                var def = array[i].Equals(default(T));
                if (index < 0 && def)
                    index = i;
                else if (index > 0 && !def)
                    index = -1;
            }
            if (index < 0) return array;
            int defCount = arrlen - (index + 1);
            T[] result = new T[defCount];
            Array.Copy(array, result, arrlen - defCount);
            return result;
        }
        public static T[] Push<T>(this T[] array, T item)
        {
            return array.Add(item);
        }
        public static T[] Pop<T>(this T[] array, out T item)
        {
            int length = array.Length;
            if (length == 0)
            {
                item = default(T);
                return array;
            }
            item = array[length - 1];
            Array.Resize(ref array, length - 1);
            return array;
        }
        public static T[] Copy<T>(this T[] array)
        {
            var len = array.Length;
            T[] arr = new T[len];
            Array.Copy(array, 0, arr, 0, len);
            return arr;
        }
        public static T[] Add<T>(this T[] array, T item)
        {
            int length = array.Length;
            Array.Resize(ref array, length + 1);
            array[length] = item;
            return array;
        }
        public static T[] AddRange<T>(this T[] array, IEnumerable<T> items)
        {
            int count = items is Array arr ? arr.Length : items.Count();
            int length = array.Length;
            Array.Resize(ref array, length + count);
            int index = length;
            foreach (T item in items)
                array[index++] = item;
            return array;
        }
        public static T[] Insert<T>(this T[] array, int index, T item)
        {
            int length = array.Length;
            Array.Resize(ref array, length + 1);
            if (index < length)
                Array.Copy(array, index, array, index + 1, length - index);
            array[index] = item;
            return array;
        }
        public static T[] InsertRange<T>(this T[] array, int index, IEnumerable<T> items)
        {
            int length = array.Length;
            int count = items is Array a ? a.Length : items.Count();
            Array.Resize(ref array, count + length);
            if (index < length)
                Array.Copy(array, index, array, index + count, length - index);
            Array itemsArray = items is Array arr ? arr : items.ToArray();
            itemsArray.CopyTo(array, index);
            return array;
        }
        public static T[] MoveFirst<T>(this T[] array, T item)
        {
            Array.Copy(array, 0, array, 1, array.Length - 1);
            array[0] = item;
            return array;
        }
        public static T[] MoveLast<T>(this T[] array, T item)
        {
            int length = array.Length;
            Array.Copy(array, 1, array, 0, length - 1);
            array[length - 1] = item;
            return array;
        }

        public static void Push<T>(ref T[] array, T item)
            => array = array.Push(item);
        public static void Pop<T>(ref T[] array, out T item)
            => array = array.Pop(out item);
        public static void Copy<T>(this T[] array, out T[] result)
        {
            var len = array.Length;
            result = new T[len];
            Array.Copy(array, 0, result, 0, len);
        }
        public static void Add<T>(ref T[] array, T item)
            => array = array.Add(item);
        public static void AddRange<T>(ref T[] array, IEnumerable<T> items)
            => array = array.AddRange(items);
        public static void Insert<T>(ref T[] array, int index, T item)
            => array = array.Insert(index, item);
        public static void InsertRange<T>(ref T[] array, int index, IEnumerable<T> items)
            => array = array.InsertRange(index, items);
        public static void MoveFirst<T>(ref T[] array, T item)
            => array = array.MoveFirst(item);
        public static void MoveLast<T>(ref T[] array, T item)
            => array = array.MoveLast(item);
        public static T[] Empty<T>() => new T[0];
        #endregion
        #region String
        public const int Upper = 65;
        public const int UpperLast = 90;
        public const int Lower = 97;
        public const int LowerLast = 122;
        public const char Undefined = char.MinValue;
        public static readonly Random Random = new Random(DateTime.Now.Millisecond);
        public static readonly Regex English = new Regex("^[A-Za-z]*$", RegexOptions.Compiled);
        public static readonly Regex RichTagBreaker = new Regex(@"<(color|material|quad|size)=(.|\n)*?>|<\/(color|material|quad|size)>|<(b|i)>|<\/(b|i)>", RegexOptions.Compiled | RegexOptions.Multiline);
        public static readonly Regex RichTagBreakerWithoutSize = new Regex(@"<(color|material|quad)=(.|\n)*?>|<\/(color|material|quad)>|<(b|i)>|<\/(b|i)>", RegexOptions.Compiled | RegexOptions.Multiline);
        public static readonly int[] Tops = { 0x3131, 0x3132, 0x3134, 0x3137, 0x3138, 0x3139, 0x3141, 0x3142, 0x3143, 0x3145, 0x3146, 0x3147, 0x3148, 0x3149, 0x314a, 0x314b, 0x314c, 0x314d, 0x314e };
        public static readonly int[] Mids = { 0x314f, 0x3150, 0x3151, 0x3152, 0x3153, 0x3154, 0x3155, 0x3156, 0x3157, 0x3158, 0x3159, 0x315a, 0x315b, 0x315c, 0x315d, 0x315e, 0x315f, 0x3160, 0x3161, 0x3162, 0x3163 };
        public static readonly int[] Bots = { 0, 0x3131, 0x3132, 0x3133, 0x3134, 0x3135, 0x3136, 0x3137, 0x3139, 0x313a, 0x313b, 0x313c, 0x313d, 0x313e, 0x313f, 0x3140, 0x3141, 0x3142, 0x3144, 0x3145, 0x3146, 0x3147, 0x3148, 0x314a, 0x314b, 0x314c, 0x314d, 0x314e };
        public static readonly char[] TopChars = { 'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ' };
        public static readonly char[] MidChars = { 'ㅏ', 'ㅐ', 'ㅑ', 'ㅒ', 'ㅓ', 'ㅔ', 'ㅕ', 'ㅖ', 'ㅗ', 'ㅘ', 'ㅙ', 'ㅚ', 'ㅛ', 'ㅜ', 'ㅝ', 'ㅞ', 'ㅟ', 'ㅠ', 'ㅡ', 'ㅢ', 'ㅣ' };
        public static readonly char[] BotChars = { '\0', 'ㄱ', 'ㄲ', 'ㄳ', 'ㄴ', 'ㄵ', 'ㄶ', 'ㄷ', 'ㄹ', 'ㄺ', 'ㄻ', 'ㄼ', 'ㄽ', 'ㄾ', 'ㄿ', 'ㅀ', 'ㅁ', 'ㅂ', 'ㅄ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ' };
        public static bool IsUpper(this char c) => c >= Upper && c <= UpperLast;
        public static bool IsLower(this char c) => c >= Lower && c <= LowerLast;
        public static bool IsAlphabet(this char c) => IsUpper(c) || IsLower(c);
        public static char ToUpper(this char c) => c.IsUpper() ? c : (char)(c - 32);
        public static char ToLower(this char c) => c.IsLower() ? c : (char)(c + 32);
        public static char Invert(this char c) => IsUpper(c) ? c.ToLower() : c.ToUpper();
        public static unsafe string Invert(this string s)
        {
            if (s == null || s.Length <= 0) return s;
            fixed (char* ptr = s)
            {
                char* c = ptr;
                char v;
                while ((v = *c) != '\0')
                {
                    if (IsAlphabet(v))
                        *c = v.Invert();
                    c++;
                }
            }
            return s;
        }
        public static unsafe string InvertAlternately(this string s)
        {
            if (s == null || s.Length <= 0) return s;
            bool isLower = s[0].IsLower();
            fixed (char* ptr = s)
            {
                char* c = ptr;
                char v;
                while ((v = *c) != '\0')
                {
                    if (IsAlphabet(v))
                        *c = (isLower = !isLower) ? v.ToLower() : v.ToUpper();
                    c++;
                }
            }
            return s;
        }
        public static unsafe string ToUpperFast(this string s)
        {
            fixed (char* ptr = s)
            {
                char* c = ptr;
                char v;
                while ((v = *c) != '\0')
                {
                    if (IsAlphabet(v))
                        *c = v.ToUpper();
                    c++;
                }
            }
            return s;
        }
        public static unsafe string ToLowerFast(this string s)
        {
            fixed (char* ptr = s)
            {
                char* c = ptr;
                char v;
                while ((v = *c) != '\0')
                {
                    if (IsAlphabet(v))
                        *c = v.ToLower();
                    c++;
                }
            }
            return s;
        }
        public static string BreakRichTag(this string s)
            => RichTagBreaker.Replace(s, string.Empty);
        public static string BreakRichTagWithoutSize(this string s)
            => RichTagBreakerWithoutSize.Replace(s, string.Empty);
        public static string ToStringFast(this int i, int radix = 10)
        {
            const string chars = "0123456789ABCDEF";
            var str = new char[32];
            var idx = str.Length;
            bool isNegative = i < 0;
            if (i <= 0)
            {
                str[--idx] = chars[-(i % radix)];
                i = -(i / radix);
            }
            while (i != 0)
            {
                str[--idx] = chars[i % radix];
                i /= radix;
            }
            if (isNegative)
                str[--idx] = '-';
            return new string(str, idx, str.Length - idx);
        }
        public static string Escape(this string str) => str.Replace(@"\", @"\\").Replace(":", @"\:");
        public static string Unescape(this string str) => str.Replace(@"\:", ":").Replace(@"\\", @"\");
        public static string[] Split2(this string str, char separator)
        {
            int index = str.IndexOf(separator);
            if (index < 0) return new string[] { str };
            return new string[] { str.Substring(0, index), str.Substring(index + 1, str.Length - (index + 1)) };
        }
        public static string GetBetween(this string str, string start, string end)
        {
            int sIdx = str.IndexOf(start);
            int eIdx = str.LastIndexOf(end);
            if (sIdx < 0 || eIdx < 0) return null;
            return str.Substring(sIdx + 1, eIdx - sIdx - 1);
        }
        public static string GetBefore(this string str, string before)
        {
            int index = str.IndexOf(before);
            if (index < 0) return null;
            return str.Substring(0, index);
        }
        public static string GetAfter(this string str, string after)
        {
            int index = str.IndexOf(after);
            if (index < 0) return null;
            return str.Substring(index + 1, str.Length - index - 1);
        }
        public static string TrimBetween(this string str, string start, string end)
        {
            int sIdx = str.IndexOf(start);
            int eIdx = str.LastIndexOf(end);
            if (sIdx < 0 || eIdx < 0) return null;
            return str.Remove(sIdx + 1, eIdx - sIdx - 1);
        }
        #endregion
        #region Patch
        public static readonly AssemblyBuilder ass;
        public static readonly ModuleBuilder mod;
        public static int TypeCount { get; internal set; }
        public static MethodInfo Wrap<T>(this T del) where T : Delegate
        {
            Type delType = del.GetType();
            IgnoreAccessCheck(delType);
            MethodInfo invoke = delType.GetMethod("Invoke");
            MethodInfo method = del.Method;
            TypeBuilder type = mod.DefineType(TypeCount++.ToString(), TypeAttributes.Public);
            ParameterInfo[] parameters = method.GetParameters();
            Type[] paramTypes = parameters.Select(p => p.ParameterType).ToArray();
            MethodBuilder methodB = type.DefineMethod("Wrapper", MethodAttributes.Public | MethodAttributes.Static, invoke.ReturnType, paramTypes);
            FieldBuilder delField = type.DefineField("function", delType, FieldAttributes.Public | FieldAttributes.Static);
            IgnoreAccessCheck(invoke.ReturnType);
            ILGenerator il = methodB.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, delField);
            int paramIndex = 1;
            foreach (ParameterInfo param in parameters)
            {
                IgnoreAccessCheck(param.ParameterType);
                methodB.DefineParameter(paramIndex++, ParameterAttributes.None, param.Name);
                il.Emit(OpCodes.Ldarg, paramIndex - 2);
            }
            il.Emit(OpCodes.Call, invoke);
            il.Emit(OpCodes.Ret);
            Type t = type.CreateType();
            t.GetField("function").SetValue(null, del);
            return t.GetMethod("Wrapper");
        }
        public static MethodInfo Prefix<T>(this Harmony harmony, MethodBase target, T del) where T : Delegate
            => harmony.Patch(target, new HarmonyMethod(Wrap(del)));
        public static MethodInfo Postfix<T>(this Harmony harmony, MethodBase target, T del) where T : Delegate
            => harmony.Patch(target, postfix: new HarmonyMethod(Wrap(del)));
        static readonly HashSet<string> accessIgnored = new HashSet<string>();
        internal static void IgnoreAccessCheck(Type type)
        {
            var name = type.Assembly.GetName();
            if (name.Name.StartsWith("System"))
                return;
            if (accessIgnored.Add(name.Name))
                ass.SetCustomAttribute(GetIACT(name.Name));
        }
        static CustomAttributeBuilder GetIACT(string name) => new CustomAttributeBuilder(iact, new[] { name });
        static readonly ConstructorInfo iact = typeof(IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) });
        #endregion
        #region Emit
        public static IntPtr EmitObject<T>(this ILGenerator il, ref T obj)
        {
            IntPtr ptr = Type<T>.GetAddress(ref obj);
            if (IntPtr.Size == 4)
                il.Emit(OpCodes.Ldc_I4, ptr.ToInt32());
            else
                il.Emit(OpCodes.Ldc_I8, ptr.ToInt64());
            il.Emit(OpCodes.Ldobj, obj.GetType());
            return ptr;
        }
        public static GCHandle EmitObjectGC(this ILGenerator il, object obj)
        {
            GCHandle handle = GCHandle.Alloc(il);
            IntPtr ptr = GCHandle.ToIntPtr(handle);
            if (IntPtr.Size == 4)
                il.Emit(OpCodes.Ldc_I4, ptr.ToInt32());
            else
                il.Emit(OpCodes.Ldc_I8, ptr.ToInt64());
            il.Emit(OpCodes.Ldobj, obj.GetType());
            return handle;
        }
        public static LocalBuilder MakeArray<T>(this ILGenerator il, int length)
        {
            LocalBuilder array = il.DeclareLocal(typeof(T[]));
            il.Emit(OpCodes.Ldc_I4, length);
            il.Emit(OpCodes.Newarr, typeof(T));
            il.Emit(OpCodes.Stloc, array);
            return array;
        }
        public static void Convert(this ILGenerator il, Type to)
        {
            switch (Type.GetTypeCode(to))
            {
                case TypeCode.Object: 
                    il.Emit(OpCodes.Box);
                    break;
                case TypeCode.Char:
                case TypeCode.Int16:
                    il.Emit(OpCodes.Conv_I2);
                    break;
                case TypeCode.SByte:
                    il.Emit(OpCodes.Conv_I1);
                    break;
                case TypeCode.Byte:
                    il.Emit(OpCodes.Conv_U1);
                    break;
                case TypeCode.UInt16:
                    il.Emit(OpCodes.Conv_U2);
                    break;
                case TypeCode.Boolean:
                case TypeCode.Int32:
                    il.Emit(OpCodes.Conv_I4);
                    break;
                case TypeCode.UInt32:
                    il.Emit(OpCodes.Conv_U4);
                    break;
                case TypeCode.Int64:
                    il.Emit(OpCodes.Conv_I8);
                    break;
                case TypeCode.UInt64:
                    il.Emit(OpCodes.Conv_U8);
                    break;
                case TypeCode.Single:
                    il.Emit(OpCodes.Conv_R4);
                    break;
                case TypeCode.Double:
                    il.Emit(OpCodes.Conv_R8);
                    break;
                case TypeCode.String:
                    il.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new[] { to }));
                    break;
                default:
                    break;
            }
        }
        public static Type CopyMethods(string typeName, IEnumerable<MethodInfo> methods)
        {
            TypeBuilder t = mod.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
            foreach (MethodInfo method in methods)
            {
                var parameters = method.GetParameters();
                MethodBuilder m = t.DefineMethod(method.Name, method.Attributes, method.CallingConvention, method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
                ILGenerator il = m.GetILGenerator();
                foreach (var param in parameters)
                    il.Emit(OpCodes.Ldarg, param.Position);
                il.Emit(OpCodes.Call);
                il.Emit(OpCodes.Ret);
            }
            return t.CreateType();
        }
        #endregion
        #region Misc
        public static Assembly[] loadedAsss { get; private set; }
        public static Type[] loadedTypes { get; private set; }
        public static Assembly AssByName(string assName)
        {
            return loadedAsss.FirstOrDefault(t => t.FullName == assName) ??
                loadedAsss.FirstOrDefault(t => t.GetName().Name == assName);
        }
        public static Type TypeByName(string typeName)
        {
            if (loadedAsss == null)
                loadedAsss = AppDomain.CurrentDomain.GetAssemblies();
            if (loadedTypes == null)
                loadedTypes = loadedAsss.SelectMany(ass => ass.GetTypes()).ToArray();
            return Type.GetType(typeName, false) ??
                loadedTypes.FirstOrDefault(t => t.FullName == typeName) ?? 
                loadedTypes.FirstOrDefault(t => t.Name == typeName);
        }
        public static void ExecuteSafe(Action exec, out Exception ex)
        {
            ex = null;
            try { exec.Invoke(); }
            catch (Exception e) { ex = e; }
        }
        public static T ExecuteSafe<T>(Func<T> exec, out Exception ex)
        {
            ex = null;
            try { return exec.Invoke(); }
            catch (Exception e) { ex = e; return default; }
        }
        public static T ExecuteSafe<T>(Func<T> exec, T defaultValue, out Exception ex)
        {
            ex = null;
            try { return exec.Invoke(); }
            catch (Exception e) { ex = e; return defaultValue; }
        }
        public static TimeSpan MeasureTime(Action a)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            a.Invoke();
            watch.Stop();
            return watch.Elapsed;
        }
        public static Delegate CreateDelegateAuto(this MethodInfo method)
        {
            var prms = method.GetParameters().Select(p => p.ParameterType);
            if (method.ReturnType != typeof(void))
                return method.CreateDelegate(Expression.GetFuncType(prms.Append(method.ReturnType).ToArray()));
            return method.CreateDelegate(Expression.GetActionType(prms.ToArray()));
        }
        #endregion
        #region Extensions
        public static string IfNullOrEmpty(this string s, string other) => string.IsNullOrEmpty(s) ? other : s;
        public static string RemoveLast(this string s, int count) => s.Remove(s.Length - count - 1);
        public static int Map(this int value, int fromMin, int fromMax, int toMin, int toMax) => toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        public static double Map(this double value, double fromMin, double fromMax, double toMin, double toMax) => toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        public static float Map(this float value, float fromMin, float fromMax, float toMin, float toMax) => toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        public static double Round(this double value, int digits) => digits < 0 ? value : Math.Round(value,  digits);
        public static float Round(this float value, int digits) => digits < 0 ? value : (float)Math.Round((double)value,  digits);
        public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> tuple, out T1 key, out T2 value)
        {
            key = tuple.Key;
            value = tuple.Value;
        }
        public static T MakeFlexible<T>(this T comp) where T : Component
        {
            comp.gameObject.MakeFlexible();
            return comp;
        }
        public static GameObject MakeFlexible(this GameObject go)
        {
            ContentSizeFitter csf = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }
        #endregion
    }
    public static class Type<T>
    {
        delegate IntPtr AddrGetter(ref T obj);
        static readonly AddrGetter addrGetter;
        delegate int SizeGetter();
        static readonly SizeGetter sizeGetter;
        static Type()
        {
            addrGetter = CreateAddrGetter();
            sizeGetter = CreateSizeGetter();
        }
        static AddrGetter CreateAddrGetter()
        {
            DynamicMethod dm = new DynamicMethod($"{typeof(T).FullName}_Address", typeof(IntPtr), new[] { typeof(T).MakeByRefType() });
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Conv_U);
            il.Emit(OpCodes.Ret);
            return (AddrGetter)dm.CreateDelegate(typeof(AddrGetter));
        }
        static SizeGetter CreateSizeGetter()
        {
            DynamicMethod dm = new DynamicMethod($"{typeof(T).FullName}_Size", typeof(int), Type.EmptyTypes);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Sizeof, typeof(T));
            il.Emit(OpCodes.Ret);
            return (SizeGetter)dm.CreateDelegate(typeof(SizeGetter));
        }
        public static readonly Type Base = typeof(T);
        public static int Size => sizeGetter();
        public static IntPtr GetAddress(ref T obj) => addrGetter(ref obj);
    }
}
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }
        public string AssemblyName { get; }
    }
}
﻿using AdofaiMapConverter;
using HarmonyLib;
using Overlayer.Core.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Overlayer.Core
{
    public static class TagManager
    {
        static Dictionary<string, Tag> AllTags = new Dictionary<string, Tag>();
        static Dictionary<string, Tag> NotPlayingTags = new Dictionary<string, Tag>();
        static Dictionary<string, Tag> ReferencedTags = new Dictionary<string, Tag>();
        public static Tag GetTag(string name) => AllTags.TryGetValue(name, out var tag) ? tag : null;
        public static Tag GetReferencedTag(string name) => ReferencedTags.TryGetValue(name, out var tag) ? tag : null;
        public static bool IsReferenced(string name) => ReferencedTags.ContainsKey(name);
        public static void UpdateReference()
        {
            ReferencedTags = AllTags.Values.Where(t => t.Referenced).ToDictionary(t => t.Name, t => t);
        }
        public static void AddTag(Tag tag, bool notPlaying)
        {
            AllTags.Add(tag.Name, tag);
            if (notPlaying) 
                NotPlayingTags.Add(tag.Name, tag);
        }
        public static bool RemoveTag(string name)
        {
            bool result = AllTags.Remove(name);
            NotPlayingTags.Remove(name);
            ReferencedTags.Remove(name);
            return result;
        }
        public static IEnumerable<Tag> All => AllTags.Values;
        public static IEnumerable<Tag> NP => NotPlayingTags.Values;
        static void Prepare()
        {
            AllTags ??= new Dictionary<string, Tag>();
            NotPlayingTags??= new Dictionary<string, Tag>();
            ReferencedTags ??= new Dictionary<string, Tag>();
        }
        public static void Load(Assembly assembly, TagConfig config = null)
        {
            foreach (Type type in assembly.GetTypes())
                Load(type, config);
        }
        public static void Load(Type type, TagConfig config = null)
        {
            Prepare();
            ClassTagAttribute cTag = type.GetCustomAttribute<ClassTagAttribute>();
            var methods = type.GetMethods(AccessTools.all);
            var fields = type.GetFields(AccessTools.all);
            if (cTag != null)
            {
                var def = methods.FirstOrDefault(m => m.GetCustomAttribute<TagAttribute>()?.IsDefault ?? false);
                if (def == null) throw new InvalidOperationException("Default Tag Method Not Found.");
                Tag tag = new Tag(cTag.Name, config);
                foreach (var thread in cTag.GetThreads(type))
                    tag.AddThread(thread);
                tag.SetGetter(def).Build();
                AllTags.Add(cTag.Name, tag);
                if (cTag.NotPlaying)
                    NotPlayingTags.Add(cTag.Name, tag);
            }
            foreach (MethodInfo method in methods)
            {
                TagAttribute tagAttr = method.GetCustomAttribute<TagAttribute>();
                if (tagAttr == null) continue;
                Tag tag = new Tag(tagAttr.Name, config);
                tag.SetGetter(method).Build();
                AllTags.Add(tagAttr.Name, tag);
                if (tagAttr.NotPlaying)
                    NotPlayingTags.Add(tagAttr.Name, tag);
            }
            foreach (FieldInfo field in fields)
            {
                FieldTagAttribute tagAttr = field.GetCustomAttribute<FieldTagAttribute>();
                if (tagAttr == null) continue;
                Tag tag = new Tag(tagAttr.Name, config);
                var func = GenerateFieldTagWrapper(tagAttr, field);
                tag.SetGetter(func);
                tag.Build();
                AllTags.Add(tagAttr.Name, tag);
                if (tagAttr.NotPlaying)
                    NotPlayingTags.Add(tagAttr.Name, tag);
            }
        }
        public static void Release()
        {
            AllTags.Values.ForEach(t => t.Dispose());
            AllTags = NotPlayingTags = ReferencedTags = null;
        }
        static Delegate GenerateFieldTagWrapper(FieldTagAttribute fTag, FieldInfo field)
        {
            DynamicMethod dm;
            ILGenerator il;
            if (fTag.Round)
            {
                dm = new DynamicMethod($"{fTag.Name}Tag_Wrapper_Opt", typeof(string), new[] { typeof(int) });
                il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, field);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, round);
                il.Emit(OpCodes.Call, StringConverter.GetFromConverter(field.FieldType));
                il.Emit(OpCodes.Ret);
                return (Func<int, string>)dm.CreateDelegate(typeof(Func<int, string>));
            }
            dm = new DynamicMethod($"{fTag.Name}Tag_Wrapper", typeof(string), Type.EmptyTypes);
            il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            if (field.FieldType != typeof(string))
                il.Emit(OpCodes.Call, StringConverter.GetFromConverter(field.FieldType));
            il.Emit(OpCodes.Ret);
            return (Func<string>)dm.CreateDelegate(typeof(Func<string>));
        }
        static readonly MethodInfo round = typeof(Math).GetMethod("Round", new[] { typeof(double), typeof(int) });
    }
}

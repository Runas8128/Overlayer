﻿using AdofaiMapConverter;
using HarmonyLib;
using Newtonsoft.Json;
using Overlayer.Core.Tags;
using Overlayer.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ConstrainedExecution;

namespace Overlayer.Core
{
    public static class TagManager
    {
        public static event Action ReferenceUpdated = delegate { };
        static Dictionary<string, Tag> AllTags = new Dictionary<string, Tag>();
        static Dictionary<string, Tag> NotPlayingTags = new Dictionary<string, Tag>();
        static Dictionary<string, Tag> ReferencedTags = new Dictionary<string, Tag>();
        static Dictionary<Type, List<string>> TypeTagCache = new Dictionary<Type, List<string>>();
        static Dictionary<PatchInfo, List<Tag>> Patches = new Dictionary<PatchInfo, List<Tag>>(PatchInfo.Comparer);
        public static Tag GetTag(string name) => AllTags.TryGetValue(name, out var tag) ? tag : null;
        public static Tag GetReferencedTag(string name) => ReferencedTags.TryGetValue(name, out var tag) ? tag : null;
        public static bool IsReferenced(string name) => ReferencedTags.ContainsKey(name);
        public static void UpdateReference()
        {
            ReferencedTags = AllTags.Values.Where(t => t.Referenced).ToDictionary(t => t.Name, t => t);
            if (!Main.HasScripts)
                UpdatePatchReference(false);
            ReferenceUpdated();
        }
        public static void UpdatePatchReference(bool forceAll = true)
        {
            if (forceAll)
            {
                foreach (var (patch, tags) in Patches)
                {
                    if (patch.Patched) continue;
                    patch.Patch(Main.Harmony);
                }
            }
            else
            {
                foreach (var (patch, tags) in Patches)
                {
                    if (tags.All(t => !t.Referenced))
                        patch.Unpatch(Main.Harmony);
                    else patch.Patch(Main.Harmony);
                }
            }
        }
        public static void SetTag(Tag tag, bool notPlaying)
        {
            AllTags[tag.Name] = tag;
            if (notPlaying)
                NotPlayingTags[tag.Name] = tag;
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
            Patches ??= new Dictionary<PatchInfo, List<Tag>>(PatchInfo.Comparer);
            TypeTagCache ??= new Dictionary<Type, List<string>>();
        }
        public static void Load(Assembly assembly, TagConfig config = null)
        {
            foreach (Type type in assembly.GetTypes())
                Load(type, config);
        }
        public static void Load(Type type, TagConfig config = null)
        {
            Prepare();
            List<string> tags = new List<string>();
            ClassTagAttribute cTag = type.GetCustomAttribute<ClassTagAttribute>();
            var methods = type.GetMethods(AccessTools.all);
            var fields = type.GetFields(AccessTools.all);
            if (cTag != null)
            {
                OverlayerDebug.Log($"Loading ClassTag {cTag.Name}..");
                var def = methods.FirstOrDefault(m => m.GetCustomAttribute<TagAttribute>()?.IsDefault ?? false);
                if (def == null) throw new InvalidOperationException("Default Tag Method Not Found.");
                Tag tag = new Tag(cTag.Name, config);
                foreach (var thread in cTag.GetThreads(type))
                    tag.AddThread(thread);
                tag.SetCategory(cTag.Category);
                tag.SetGetter(def).Build();
                AllTags.Add(cTag.Name, tag);
                if (cTag.NotPlaying)
                    NotPlayingTags.Add(cTag.Name, tag);
                if (cTag.RelatedPatches != null)
                    AddPatches(tag, cTag.RelatedPatches);
                tags.Add(cTag.Name);
            }
            foreach (MethodInfo method in methods)
            {
                TagAttribute tagAttr = method.GetCustomAttribute<TagAttribute>();
                if (tagAttr == null) continue;
                if (cTag != null && tagAttr.IsDefault) continue;
                tagAttr.Name = tagAttr.Name ?? method.Name;
                OverlayerDebug.Log($"Loading Tag {tagAttr.Name}..");
                Tag tag = new Tag(tagAttr.Name, config);
                tag.SetCategory(tagAttr.Category);
                tag.SetGetter(method).Build();
                AllTags.Add(tagAttr.Name, tag);
                if (tagAttr.NotPlaying)
                    NotPlayingTags.Add(tagAttr.Name, tag);
                if (tagAttr.RelatedPatches != null)
                    AddPatches(tag, tagAttr.RelatedPatches);
                tags.Add(tagAttr.Name);
            }
            foreach (FieldInfo field in fields)
            {
                FieldTagAttribute tagAttr = field.GetCustomAttribute<FieldTagAttribute>();
                if (tagAttr == null) continue;
                tagAttr.Name = tagAttr.Name ?? field.Name;
                OverlayerDebug.Log($"Loading FieldTag {tagAttr.Name}..");
                Tag tag = new Tag(tagAttr.Name, config);
                tag.SetCategory(tagAttr.Category);
                Delegate func;
                if (tagAttr.Processor == null)
                    func = GenerateFieldTagWrapper(tagAttr, field, out _);
                else
                {
                    MethodInfo processor = AccessTools.Method(tagAttr.Processor);
                    func = GenerateFieldTagProcessingWrapper(tagAttr, field, processor, out _);
                }
                tag.SetGetter(func);
                tag.Build();
                AllTags.Add(tagAttr.Name, tag);
                if (tagAttr.NotPlaying)
                    NotPlayingTags.Add(tagAttr.Name, tag);
                if (tagAttr.RelatedPatches != null)
                    AddPatches(tag, tagAttr.RelatedPatches);
                tags.Add(tagAttr.Name);
            }
            TypeTagCache.Add(type, tags);
        }
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public static bool Unload(Assembly ass)
        {
            try
            {
                bool result = true;
                bool requireRefresh = false;
                foreach (Type type in ass.GetTypes())
                {
                    if (TypeTagCache.TryGetValue(type, out var tags))
                    {
                        foreach (var tag in tags)
                            result &= RemoveTag(tag);
                        TypeTagCache.Remove(type);
                        requireRefresh = true;
                    }
                }
                if (requireRefresh)
                {
                    UpdateReference();
                    TextManager.Refresh();
                }
                return result;
            }
            catch { return false; }
        }
        public static bool Unload(Type type)
        {
            if (TypeTagCache.TryGetValue(type, out var tags))
            {
                bool result = true;
                foreach (var tag in tags)
                    result &= RemoveTag(tag);
                UpdateReference();
                TextManager.Refresh();
                TypeTagCache.Remove(type);
                return result;
            }
            return false;
        }
        public static void Release()
        {
            AllTags?.Values.ForEach(t => t.Dispose());
            AllTags = NotPlayingTags = ReferencedTags = null;
            Patches = null;
            TypeTagCache = null;
        }
        public static string GetTagInfos()
        {
            TagInfoResult result = new TagInfoResult();
            var infos = result.Infos = new List<TagInfo>();
            foreach (var tag in AllTags.Values)
                infos.Add(new TagInfo() { Name = tag.Name, Category = tag.Category, HasOption = tag.HasOption });
            result.Count = infos.Count;
            return JsonConvert.SerializeObject(result);
        }
        static void AddPatches(Tag tag, string patchNames)
        {
            foreach (var info in ParsePatchNames(patchNames))
            {
                if (Patches.TryGetValue(info, out var tags))
                    tags.Add(tag);
                else Patches.Add(info, new List<Tag> { tag });
            }
        }
        static List<PatchInfo> ParsePatchNames(string patchNames)
        {
            string[] patches = patchNames.Split('|');
            List<PatchInfo> pInfos = new List<PatchInfo>();
            foreach (string patch in patches)
            {
                PatchInfo pInfo;
                string[] split = patch.Split2(':');
                var type = MiscUtils.TypeByName(split[0]);
                if (split.Length < 2)
                    pInfo = new PatchInfo(type);
                else pInfo = new PatchInfo(type.GetMethod(split[1], AccessTools.all));
                pInfos.Add(pInfo);
            }
            return pInfos;
        }
        static Delegate GenerateFieldTagWrapper(FieldTagAttribute fTag, FieldInfo field, out DynamicMethod dm)
        {
            ILGenerator il;
            if (fTag.Round)
            {
                dm = new DynamicMethod($"{fTag.Name}Tag_Wrapper_Opt", field.FieldType, new[] { typeof(int) }, true);
                dm.DefineParameter(1, System.Reflection.ParameterAttributes.None, "digits");
                il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, field);
                if (field.FieldType != typeof(double))
                    il.Emit(OpCodes.Conv_R8);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, round);
                if (field.FieldType != typeof(double))
il.Convert(field.FieldType);
                il.Emit(OpCodes.Ret);
                return dm.CreateDelegate(Expression.GetFuncType(new[] { typeof(int), field.FieldType }));
            }
            dm = new DynamicMethod($"{fTag.Name}Tag_Wrapper", field.FieldType, Type.EmptyTypes, true);
            il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            return dm.CreateDelegate(Expression.GetFuncType(field.FieldType));
        }
        static Delegate GenerateFieldTagProcessingWrapper(FieldTagAttribute fTag, FieldInfo field, MethodInfo processor, out DynamicMethod dm)
        {
            dm = null;
            ILGenerator il;
            var prms = processor.GetParameters();
            if (prms[0].ParameterType != field.FieldType) return null;
            dm = new DynamicMethod($"{fTag.Name}Tag_Wrapper_Processor", processor.ReturnType, new[] { prms[1].ParameterType }, true);
            il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, processor);
            il.Emit(OpCodes.Ret);
            return dm.CreateDelegate(Expression.GetFuncType(new[] { prms[1].ParameterType, processor.ReturnType }));
        }
        static readonly MethodInfo round = typeof(ExtUtils).GetMethod("Round", new[] { typeof(double), typeof(int) });
        public class TagInfoResult
        {
            public List<TagInfo> Infos;
            public int Count;
        }
        public class TagInfo
        {
            public string Name;
            public Category Category;
            public bool HasOption;
        }
    } 
}

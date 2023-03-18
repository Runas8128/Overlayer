using System;
using System.Linq;
using System.Reflection;
using Overlayer.Core.Utils;
using System.Collections.Generic;
using Tag = Overlayer.Core.Tag;
using UnityEngine;

namespace Overlayer.Core
{
    public static class TagManager
    {
        public static Dictionary<string, Tag> Dict = new Dictionary<string, Tag>();
        public static void LoadTags(this List<Tag> tags, Assembly assembly = null)
        {
            Dict = new Dictionary<string, Tag>();
            foreach (Type type in (assembly ?? Assembly.GetCallingAssembly()).GetTypes())
                LoadTags(tags, type);
        }
        public static void LoadTags(this List<Tag> tags, Type type)
        {
            ClassTagAttribute cTagAttr = type.GetCustomAttribute<ClassTagAttribute>();
            MethodInfo[] methods = type.GetMethods((BindingFlags)15420);
            if (cTagAttr != null)
            {
                MethodInfo valueGetter = methods.FirstOrDefault(m =>
                {
                    TagAttribute ta = m.GetCustomAttribute<TagAttribute>();
                    if (ta is TagAttribute f && f.IsDefault)
                        return true;
                    return false;
                });
                if (valueGetter == null)
                    throw new InvalidOperationException("ClassTag Must Have ValueGetter Method!");
                if (!valueGetter.IsStatic)
                    throw new InvalidOperationException("ValueGetter Must Be Static!");
                Tag tag = new Tag(cTagAttr.Name).SetGetter(valueGetter);
                cTagAttr.Threads?.ForEach(s => tag.AddThread(type.GetMethod(s)));
                tag.Build();
                tags.Add(tag);
                Dict.Add(tag.Name, tag);
            }
            foreach (MethodInfo method in methods)
            {
                TagAttribute tagAttr = method.GetCustomAttribute<TagAttribute>();
                if (tagAttr == null) continue;
                if (tagAttr.IsDefault) continue;
                var tag = new Tag(tagAttr.Name).SetGetter(method);
                tag.Build();
                tags.Add(tag);
                Dict.Add(tag.Name, tag);
            }
        }
        public static Tag FindTag(this List<Tag> tags, string name) => tags.Find(t => t.Name == name);
        public static void SetTag(this List<Tag> tags, string name, Tag tag)
        {
            int index = tags.FindIndex(t => t.Name == name);
            if (index < 0)
                tags.Add(tag);
            else tags[index] = tag;
            Dict[name] = tag;
        }
        public static void RemoveTag(this List<Tag> tags, string name)
        {
            int index = tags.FindIndex(t => t.Name == name);
            if (index >= 0)
                tags.RemoveAt(index);
            Dict.Remove(name);
        }
        public static void DescGUI(this List<Tag> tags)
        {
            GUILayout.BeginHorizontal();
            if (TagDesc = GUILayout.Toggle(TagDesc, "Tags"))
            {
                GUIUtils.IndentGUI(() =>
                {
                    for (int i = 0; i < tags.Count; i++)
                    {
                        Tag tag = tags[i];
                        if (Main.Language.dict.TryGetValue(tag.Name, out string desc))
                            GUILayout.Label($"{tag} {desc} ({(Type.GetTypeCode(tag.Getter.ReturnType) == TypeCode.String ? "String" : "Number")})");
                        else GUILayout.Label($"{tag} (Object)");
                        GUILayout.Space(1);
                    }
                }, 25f, 10f);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        public static bool TagDesc { get; private set; }
        static HashSet<Tag> References = new HashSet<Tag>();
        public static void UpdateReference()
        {
            References = new HashSet<Tag>();
            var global = OverlayerText.Global.Texts.SelectMany(t => t.PlayingCompiler.References);
            var groups = OverlayerText.Groups.SelectMany(g => g.Texts.SelectMany(t => t.PlayingCompiler.References));
            var references = global.Concat(groups);
            foreach (var reference in references)
                References.Add(reference);
        }
        public static Tag GetReference(string name)
        {
            foreach (var tag in References)
                if (tag.Name == name)
                    return tag;
            return null;
        }
        public static bool HasReference(string name)
            => GetReference(name) != null;
    }
}

using System;
using System.Linq;
using System.Reflection;
using Overlayer.Core.Utils;
using System.Collections.Generic;
using Tag = Overlayer.Core.Replacer.Tag;
using UnityEngine;

namespace Overlayer.Core
{
    public static class TagManager
    {
        public static void LoadTags(this List<Tag> tags, Assembly assembly = null)
        {
            foreach (Type type in (assembly ?? Assembly.GetCallingAssembly()).GetTypes())
                LoadTags(tags, type);
        }
        public static void LoadTags(this List<Tag> tags, Type type)
        {
            Replacer tmpRep = new Replacer();
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
                Tag tag = tmpRep.CreateTag(cTagAttr.Name).SetGetter(valueGetter);
                cTagAttr.Threads?.ForEach(s => tag.AddThread(type.GetMethod(s)));
                tag.Build();
                tags.Add(tag);
            }
            foreach (MethodInfo method in methods)
            {
                TagAttribute tagAttr = method.GetCustomAttribute<TagAttribute>();
                if (tagAttr == null) continue;
                if (tagAttr.IsDefault) continue;
                var tag = tmpRep.CreateTag(tagAttr.Name).SetGetter(method);
                tag.Build();
                tags.Add(tag);
            }
        }
        public static Tag FindTag(this List<Tag> tags, string name) => tags.Find(t => t.Name == name);
        public static void SetTag(this List<Tag> tags, string name, Tag tag)
        {
            int index = tags.FindIndex(t => t.Name == name);
            if (index < 0)
                tags.Add(tag);
            else tags[index] = tag;
        }
        public static void RemoveTag(this List<Tag> tags, string name)
        {
            int index = tags.FindIndex(t => t.Name == name);
            if (index >= 0)
                tags.RemoveAt(index);
        }
        public static void DescGUI(this List<Tag> tags)
        {
            GUILayout.BeginHorizontal();
            if (TagDesc = GUILayout.Toggle(TagDesc, "Tags"))
            {
                GUIUtils.IndentGUI(() =>
                {
                    foreach (Tag tag in tags)
                    {
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
    }
}

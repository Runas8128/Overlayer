using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Overlayer.Core;
using TMPro;

namespace Overlayer.Tags
{
    public static class Extensions
    {
        public static void CreateTagsFromAssembly(this Replacer replacer, Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                ClassTagAttribute classTag = type.GetCustomAttribute<ClassTagAttribute>();
                if (classTag != null)
                {
                    MethodInfo tagMethod = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<TagAttribute>() != null);
                    if (tagMethod != null)
                    {
                        Replacer.Tag tag = replacer.CreateTag(classTag.Name)
                            .SetGetter(tagMethod);
                        if (classTag.Threads != null)
                            for (int i = 0; i < classTag.Threads.Length; i++)
                                tag.AddThread((ThreadStart)type.GetMethod(classTag.Threads[i]).CreateDelegate(typeof(ThreadStart)));
                        tag.Build();
                    }
                }
                foreach (MethodInfo method in type.GetMethods())
                {
                    TagAttribute tagAttr = method.GetCustomAttribute<TagAttribute>();
                    if (tagAttr != null)
                    {
                        Replacer.Tag tag = replacer.CreateTag(tagAttr.Name)
                            .SetGetter(method);
                        tag.Build();
                    }
                }
            }
        }
    }
}

using Overlayer.Core;
using System.Reflection;
using static UnityModManagerNet.UnityModManager;
using System.IO;

namespace Overlayer.Python
{
    public static class Main
    {
        public static void LoadAllPyTags()
        {
            int success = 0, fail = 0;
            foreach (string path in Directory.GetFiles(Overlayer.Main.CustomTagsPath, "*.py"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == "Impl") continue;
                try
                {
                    var tag = PythonUtils.CreateTag(path);
                    Overlayer.Main.AllTags.SetTag(tag.Name, tag);
                    Overlayer.Main.NotPlayingTags.SetTag(tag.Name, tag);
                    Overlayer.Main.CustomTagCache.Add(tag.Name);
                    success++;
                }
                catch { fail++; }
            }
        }
        public static void Load(ModEntry modEntry)
        {
            Overlayer.Main.AllCustomTagsLoaded += LoadAllPyTags;
        }
    }
}

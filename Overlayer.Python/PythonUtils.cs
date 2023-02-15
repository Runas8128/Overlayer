using Overlayer.Core;
using System;
using System.IO;
using Py = IronPython.Hosting.Python;

namespace Overlayer.Python
{
    public static class PythonUtils
    {
        public static Replacer.Tag CreateTag(string path)
        {
            Replacer tmp = new Replacer();
            var engine = Py.CreateEngine();
            var source = engine.CreateScriptSourceFromFile(path);
            var com = source.Compile();
            var name = Path.GetFileNameWithoutExtension(path);
            return tmp.CreateTag(name).SetGetter(new Func<object>(() => com.Execute<object>()));
        }
        public static dynamic Execute(string path)
        {
            var engine = Py.CreateEngine();
            var source = engine.CreateScriptSourceFromFile(path);
            return source.Execute();
        }
    }
}

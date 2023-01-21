using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JSEngine.CustomLibrary
{
    public class Dictionary
    {
        private Dictionary<object, object> dict = new Dictionary<object, object>();
        public object get(object key) => dict.TryGetValue(key, out var value) ? value : null;
        public void set(object key, object value) => dict[key] = value;
        public void clear() => dict.Clear();
        public int count => dict.Count;
    }
    public class List
    {
        private List<object> list = new List<object>();
        public object get(int index) => list.Count <= index ? null : list[index];
        public void set(int index, object value)
        {
            if (list.Count <= index)
                list.Capacity = index + 1;
            list[index] = value;
        }
        public void clear() => list.Clear();
        public int count => list.Count;
    }
}

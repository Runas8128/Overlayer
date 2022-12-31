using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overlayer.Tags
{
    public class TagAttribute : Attribute
    {
        public string Name { get; }
        public TagAttribute() => Name = null;
        public TagAttribute(string name) => Name = name;
    }
    public class ClassTagAttribute : TagAttribute
    {
        public string[] Threads { get; set; }
        public ClassTagAttribute(string name) : base(name) { }
    }
}

﻿using System;
using System.Collections.Generic;

namespace Overlayer.Core.Tags
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class TagAttribute : Attribute
    {
        public TagAttribute() { }
        public TagAttribute(string name) => Name = name;
        public string Name { get; set; }
        public bool NotPlaying { get; set; }
        public string RelatedPatches = null;
        public bool IsDefault => Name == null;
    }
}

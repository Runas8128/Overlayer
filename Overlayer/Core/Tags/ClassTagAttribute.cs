﻿using System;
using Overlayer.Core;

namespace Overlayer.Core.Tags
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ClassTagAttribute : TagAttribute
    {
        public ClassTagAttribute(string name) : base(name) { }
        public string[] Threads;
    }
}

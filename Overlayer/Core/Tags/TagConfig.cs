﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overlayer.Core.Tags
{
    public class TagConfig
    {
        public char Open = '{';
        public char Close = '}';
        public char Separator = ':';
        public char? ArgOpen = null;
        public char? ArgClose = null;
        public bool IsArgument => ArgOpen != null && ArgClose != null;
        public bool IsNormal => !IsArgument;
        public bool IsValid => (IsArgument || IsNormal) && Open != 0 && Close != 0 && Separator != 0;
        public static TagConfig DefaultNormal => new TagConfig();
        public static TagConfig DefaultArgument => new TagConfig() { ArgOpen = '(', ArgClose = ')' };
    }
}

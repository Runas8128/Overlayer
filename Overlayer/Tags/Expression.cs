using System.Collections.Generic;
using System;
using Overlayer.Core.Tags;
using Overlayer.Scripting;
using Overlayer.Scripting.JS;
using Overlayer.Core.Utils;
using Overlayer.Core;

namespace Overlayer.Tags
{
    [ClassTag("Expression", Category = Category.Misc)]
    public static class Expression
    {
        public static readonly Dictionary<string, Result> expressions = new Dictionary<string, Result>();
        [Tag]
        public static object Expr(string expr)
        {
            if (!Main.HasScripts)
            {
                TagManager.UpdatePatchReference();
                Main.HasScripts = true;
            }
            if (expressions.TryGetValue(expr, out var res))
                return res.Eval();
            res = MiscUtils.ExecuteSafe(() => JSUtils.CompileSource(expr), out _);
            if (res == null) return null;
            return (expressions[expr] = res).Eval();
        }
    }
}

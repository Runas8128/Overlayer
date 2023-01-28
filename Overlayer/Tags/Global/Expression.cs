using JSEngine;
using Overlayer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overlayer.Tags.Global
{
    [ClassTag("Expression")]
    public static class Expression
    {
        public static readonly Dictionary<string, ExprHolder> expressions = new Dictionary<string, ExprHolder>();
        [Tag]
        public static object Expr(string expr)
        {
            if (expressions.TryGetValue(expr, out ExprHolder holder))
                return holder.Invoke();
            return (expressions[expr] = new ExprHolder(expr)).Invoke();
        }
    }
    public class ExprHolder
    {
        public readonly CompiledEval eval;
        public readonly ScriptEngine engine;
        public ExprHolder(string expr)
        {
            engine = JavaScript.PrepareEngine();
            eval = CompiledEval.Compile(new StringSource(expr), JS.Option);
        }
        public object Invoke() => eval.EvaluateFastInternal(engine);
    }
}

using Overlayer.Core;
using Overlayer.Core.Utils;
using Overlayer.Scripting.JS;
using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace Overlayer.Scripting.Python
{
    public class PyModuleGenerator : ModuleGenerator
    {
        public override ScriptType ScriptType => ScriptType.Python;
        public override string GenerateTagsModule()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var tag in TagManager.All)
            {
                ParameterInfo[] tagOptions = tag.Getter.GetParameters();
                if (tagOptions.Length > 0)
                    sb.AppendLine($"def {tag.Name}({GetArgStr(tagOptions)}) -> {GetTypeStr(tag.Getter.ReturnType)}: return {tag.Name}({GetCallArgStr(tagOptions)})");
                else
                    sb.AppendLine($"def {tag.Name}() -> {GetTypeStr(tag.Getter.ReturnType)}: return {tag.Name}()");
            }
            return sb.ToString();
        }
        public override string GenerateApiModule()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var (attr, t) in Api.GetApiTypesWithAttr(ScriptType))
                PythonUtils.WriteType(t, sb, attr.Name);
            foreach (var api in Api.GetApiMethods(ScriptType))
            {
                ParameterInfo[] options = api.GetParameters();
                if (options.Length > 0)
                    sb.AppendLine($"def {api.Name}({GetArgStr(options)}) -> {GetTypeStr(api.ReturnType)}: {(api.ReturnType != typeof(void) ? "return " : "")}{api.Name}({GetCallArgStr(options)})");
                else
                    sb.AppendLine($"def {api.Name}() -> {GetTypeStr(api.ReturnType)}: {(api.ReturnType != typeof(void) ? "return " : "")}{api.Name}()");
            }
            return sb.ToString();
        }
        public static string GetArgStr(ParameterInfo[] args, bool defaultTypeIsOriginal = false)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var arg in args)
                sb.Append($"{arg.Name.IfNullOrEmpty("digits")}:{GetTypeStr(arg.ParameterType, defaultTypeIsOriginal)}, ");
            var result = sb.ToString();
            return result.Remove(result.Length - 2);
        }
        public static string GetCallArgStr(ParameterInfo[] args)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var arg in args)
                sb.Append($"{arg.Name.IfNullOrEmpty("digits")}, ");
            var result = sb.ToString();
            return result.Remove(result.Length - 2);
        }
        public static string GetTypeStr(Type type, bool defaultIsOriginal = false)
        {
            if (type == typeof(void)) return "None";
            else if (type.IsArray) return "list";
            else if (typeof(IDictionary).IsAssignableFrom(type)) return "dict";
            string result;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Single:
                case TypeCode.Double:
                    result = "float";
                    break;
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16: 
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    result = "int";
                    break;
                case TypeCode.String:
                    result = "str";
                    break;
                case TypeCode.Boolean:
                    result =  "bool";
                    break;
                default:
                    var toSearch = type.IsArray ? type.GetElementType() : type;
                    if (Api.IsContains(ScriptType.Python, toSearch))
                        result = toSearch.GetCustomAttribute<ApiAttribute>().Name ?? type.Name;
                    else result = defaultIsOriginal ? type.Name.RemoveAfter("`") : "object";
                    break;
            }
            return result;
        }
    }
}

namespace Overlayer.Scripting
{
    public abstract class ModuleGenerator
    {
        public abstract ScriptType ScriptType { get; }
        public abstract string GenerateTagsModule();
        public abstract string GenerateApiModule();
    }
}

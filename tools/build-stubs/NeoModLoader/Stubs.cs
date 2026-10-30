// Заглушка NeoModLoader — только для компиляции.

using System.Collections.Generic;

namespace NeoModLoader.api
{
    public class ModConfigItem
    {
        public object GetValue() => null;
    }

    public class ModConfig
    {
        public Dictionary<string, ModConfigItem> this[string category] => null;
    }

    public class ModDeclare
    {
        public string FolderPath { get; set; }
        public string Name { get; set; }
    }

    public class BasicMod<T> where T : BasicMod<T>, new()
    {
        protected static void LogInfo(string message) { }
        protected static void LogError(string message) { }
        protected virtual void OnModLoad() { }
        protected ModConfig GetConfig() => null;
        public ModDeclare GetDeclaration() => null;
    }
}

namespace NeoModLoader.services
{
    public static class LogService
    {
        public static void LogInfo(string message) { }
        public static void LogError(string message) { }
    }
}

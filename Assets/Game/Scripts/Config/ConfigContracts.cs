using System;

namespace ImmortalLoot.Config
{
    public interface IConfigSource
    {
        string LoadText(string configName);
    }

    public interface IConfigRepository
    {
        GameConfigCatalog LoadAll();
    }

    public sealed class ConfigException : Exception
    {
        public ConfigException(string message) : base(message) { }
    }
}

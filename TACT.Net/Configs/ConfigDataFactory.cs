using System;
using System.Collections.Generic;
using System.Linq;

namespace TACT.Net.Configs
{
    using Lookup = Dictionary<ConfigType, Dictionary<string, uint>>;
    using LookupEntry = Dictionary<string, uint>;

    /// <summary>
    /// A config data generator
    /// </summary>
    internal static class ConfigDataFactory
    {
        #region Data Generators

        /// <summary>
        /// Populates a KeyValueConfig of <paramref name="type"/> with <paramref name="build"/> specific defaults
        /// </summary>
        /// <param name="type"></param>
        /// <param name="build"></param>
        /// <returns></returns>
        public static Dictionary<string, List<string>> GenerateData(ConfigType type, uint build)
        {
            var collection = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            switch (type)
            {
                case ConfigType.BuildConfig:
                    AddValue(collection, "build-name", "");
                    AddValue(collection, "build-uid", "");
                    AddValue(collection, "build-product", "WoW");
                    AddValue(collection, "build-playbuild-installer", "ngdptool_casc2");
                    AddValue(collection, "root", "");
                    AddValue(collection, "install", "", "");
                    AddValue(collection, "download", "", "");
                    AddValue(collection, "encoding", "", "");
                    break;
                case ConfigType.CDNConfig:
                    AddValue(collection, "archives");
                    AddValue(collection, "archives-index-size");
                    AddValue(collection, "patch-archives");
                    AddValue(collection, "patch-archives-index-size");
                    break;
                default:
                    throw new ArgumentException("Invalid KeyValueConfig type");
            }

            // build specific fields
            AddLookupValues(collection, type, build);

            return collection;
        }

        /// <summary>
        /// Populates a VariableConfig of <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static (string[] Fields, string[] Values) GenerateData(ConfigType type)
        {
            var collection = new Dictionary<string, string>();

            switch (type)
            {
                case ConfigType.CDNs:
                    AddValue(collection, "Name!STRING:0");
                    AddValue(collection, "Path!STRING:0", "tpr/wow");
                    AddValue(collection, "Hosts!STRING:0");
                    AddValue(collection, "Servers!STRING:0");
                    AddValue(collection, "ConfigPath!STRING:0", "tpr/configs/data");
                    break;
                case ConfigType.Versions:
                    AddValue(collection, "Region!STRING:0");
                    AddValue(collection, "BuildConfig!HEX:16");
                    AddValue(collection, "CDNConfig!HEX:16");
                    AddValue(collection, "KeyRing!HEX:16");
                    AddValue(collection, "BuildId!DEC:4", "00000");
                    AddValue(collection, "VersionsName!String:0", "0.0.0.00000");
                    AddValue(collection, "ProductConfig!HEX:16");
                    break;
                default:
                    throw new ArgumentException("Invalid VariableConfig type");
            }

            return (collection.Keys.ToArray(), collection.Values.ToArray());
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Generic-ish dictionary populator
        /// </summary>
        /// <param name="dictionay"></param>
        /// <param name="key"></param>
        /// <param name="values"></param>
        private static void AddValue(System.Collections.IDictionary dictionay, string key, params string[] values)
        {
            var valueType = dictionay.GetType().GetGenericArguments()[1];
            bool isList = valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(List<>);

            if (isList)
                dictionay[key] = new List<string>(values);
            else
                dictionay[key] = values.Length == 0 ? "" : values[0];
        }

        /// <summary>
        /// Adds build specific fields to the collection
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="type"></param>
        /// <param name="build"></param>
        private static void AddLookupValues(Dictionary<string, List<string>> collection, ConfigType type, uint build)
        {
            if (Lookup.Value.TryGetValue(type, out var fields))
            {
                string[] defaultValue = type == ConfigType.BuildConfig ? new[] { "", "" } : new string[0];

                foreach (var field in fields)
                    if (build > field.Value)
                        AddValue(collection, field.Key, defaultValue);
            }
        }

        #endregion

        #region Lookup

        private static readonly Lazy<Lookup> Lookup = new Lazy<Lookup>(() =>
        {
            return new Lookup
            {
                {
                    ConfigType.BuildConfig, new LookupEntry()
                    {
                        { "encoding-size", 18888 },
                        { "install-size", 22231 },
                        { "download-size", 22231 },
                        { "size", 22231 },
                        { "size-size", 27547 },
                    }
                },
                {
                    ConfigType.CDNConfig, new LookupEntry()
                    {
                        { "file-index", 27165 },
                        { "file-index-size", 27165 },
                        { "patch-file-index", 27165 },
                        { "patch-file-index-size", 27165 },
                    }
                }
            };
        });

        #endregion
    }
}

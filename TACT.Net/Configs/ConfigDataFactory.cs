using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TACT.Net.Configs
{
    // (key, minbuild, valuecount)
    using Lookup = Dictionary<ConfigType, (string, uint, int)[]>;

    /// <summary>
    /// A config data generator
    /// </summary>
    internal static class ConfigDataFactory
    {
        private static readonly string[] Empty = { "", "" };

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

            if (KeyValueLookup.Value.TryGetValue(type, out var fields))
            {
                foreach ((string key, uint minbuild, int size) in fields)
                {
                    if (build > minbuild)
                    {
                        AddValue(collection, type, key, Empty[..size]);
                    }
                }
            }

            // apply static values
            if (type == ConfigType.BuildConfig)
            {
                collection["build-product"][0] = "WoW";
                collection["build-playbuild-installer"][0] = "ngdptool_casc2";
            }

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
                    AddValue(collection, type, "Name!STRING:0");
                    AddValue(collection, type, "Path!STRING:0", "tpr/wow");
                    AddValue(collection, type, "Hosts!STRING:0");
                    AddValue(collection, type, "Servers!STRING:0");
                    AddValue(collection, type, "ConfigPath!STRING:0", "tpr/configs/data");
                    break;
                case ConfigType.Versions:
                    AddValue(collection, type, "Region!STRING:0");
                    AddValue(collection, type, "BuildConfig!HEX:16");
                    AddValue(collection, type, "CDNConfig!HEX:16");
                    AddValue(collection, type, "KeyRing!HEX:16");
                    AddValue(collection, type, "BuildId!DEC:4", "00000");
                    AddValue(collection, type, "VersionsName!String:0", "0.0.0.00000");
                    AddValue(collection, type, "ProductConfig!HEX:16");
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
        private static void AddValue(IDictionary dictionay, ConfigType type, string key, params string[] values)
        {
            if (type == ConfigType.CDNs || type == ConfigType.Versions)
                dictionay[key] = values.Length == 0 ? "" : values[0];
            else
                dictionay[key] = new List<string>(values);

        }

        #endregion

        #region Lookup

        private static readonly Lazy<Lookup> KeyValueLookup = new Lazy<Lookup>(() =>
        {
            return new Lookup
            {
                {
                    ConfigType.BuildConfig, new []
                    {
                        ("root", 0u, 1),
                        ("install", 0u, 2),
                        ("install-size", 22231u, 2),
                        ("download", 0u, 2),
                        ("download-size", 22231u, 2),
                        ("size", 22231u, 2),
                        ("size-size", 27547u, 2),
                        ("encoding", 0u, 2),
                        ("encoding-size", 18888u, 2),
                        ("build-name", 0u, 1),
                        ("build-uid", 0u, 1),
                        ("build-product", 0u, 1),
                        ("build-playbuild-installer", 0u, 1)
                    }
                },
                {
                    ConfigType.CDNConfig, new []
                    {
                        ("archives", 0u, 1),
                        ("archives-index-size", 0u, 1),
                        ("patch-archives", 0u, 1),
                        ("patch-archives-index-size", 0u, 1),
                        ("file-index", 27165u, 0),
                        ("file-index-size", 27165u, 0),
                        ("patch-file-index", 27165u, 0),
                        ("patch-file-index-size", 27165u, 0)
                    }
                }
            };
        });

        #endregion
    }
}

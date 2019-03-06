using System;
using System.Collections.Generic;
using System.Linq;

namespace TACT.Net.Configs
{
    using Lookup = Dictionary<ConfigType, Dictionary<string, uint>>;
    using LookupEntry = Dictionary<string, uint>;

    internal static class ConfigDataFactory
    {
        #region Data Generators

        public static Dictionary<string, List<string>> GenerateData(ConfigType type, uint build)
        {
            var data = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            switch (type)
            {
                case ConfigType.BuildConfig:
                    AddValue(data, "build-name", "");
                    AddValue(data, "build-uid", "");
                    AddValue(data, "build-product", "WoW");
                    AddValue(data, "build-playbuild-installer", "ngdptool_casc2");
                    AddValue(data, "root", "");
                    AddValue(data, "install", "", "");
                    AddValue(data, "download", "", "");
                    AddValue(data, "encoding", "", "");
                    break;
                case ConfigType.CDNConfig:
                    AddValue(data, "archives");
                    AddValue(data, "archives-index-size");
                    AddValue(data, "patch-archives");
                    AddValue(data, "patch-archives-index-size");
                    break;
                default:
                    throw new ArgumentException("Invalid KeyValueConfig type");
            }

            // build specific fields
            if (Lookup.Value.TryGetValue(type, out var fields))
            {
                string[] defaultValue = type == ConfigType.BuildConfig ? new[] { "", "" } : new string[0];

                foreach (var field in fields)
                    if (build > field.Value)
                        AddValue(data, field.Key, defaultValue);
            }

            return data;
        }

        public static (string[] Fields, string[] Values) GenerateData(ConfigType type)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            switch (type)
            {
                case ConfigType.CDNs:
                    AddValue(data, "Name!STRING:0");
                    AddValue(data, "Path!STRING:0", "tpr/wow");
                    AddValue(data, "Hosts!STRING:0");
                    AddValue(data, "Servers!STRING:0");
                    AddValue(data, "ConfigPath!STRING:0", "tpr/configs/data");
                    break;
                case ConfigType.Versions:
                    AddValue(data, "Region!STRING:0");
                    AddValue(data, "BuildConfig!HEX:16");
                    AddValue(data, "CDNConfig!HEX:16");
                    AddValue(data, "KeyRing!HEX:16");
                    AddValue(data, "BuildId!DEC:4", "00000");
                    AddValue(data, "VersionsName!String:0", "0.0.0.00000");
                    AddValue(data, "ProductConfig!HEX:16");
                    break;
                default:
                    throw new ArgumentException("Invalid VariableConfig type");
            }

            return (data.Keys.ToArray(), data.Values.ToArray());
        }

        #endregion

        #region Helpers

        private static void AddValue(Dictionary<string, List<string>> collection, string key, params string[] values)
        {
            collection[key] = values.Length == 0 ? new List<string>() : new List<string>(values);
        }

        private static void AddValue(Dictionary<string, string> collection, string key, string value = "")
        {
            collection[key] = value;
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

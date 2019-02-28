using System;
using System.Collections.Generic;
using System.Linq;

namespace TACT.Net.Configs
{
    internal static class ConfigDataFactory
    {
        #region Data Generators

        public static Dictionary<string, List<string>> GenerateKeyValueData(ConfigType type)
        {
            var data = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            switch (type)
            {
                case ConfigType.BuildConfig:
                    AddValue(data, "root", "");
                    AddValue(data, "install", "", "");
                    AddValue(data, "install-size", "", "");
                    AddValue(data, "download", "", "");
                    AddValue(data, "download-size", "", "");
                    AddValue(data, "size", "", "");
                    AddValue(data, "size-size", "", "");
                    AddValue(data, "encoding", "", "");
                    AddValue(data, "encoding-size", "", "");
                    AddValue(data, "build-name", "");
                    AddValue(data, "build-uid", "wow");
                    AddValue(data, "build-product", "WoW");
                    AddValue(data, "build-playbuild-installer", "ngdptool_casc2");
                    break;
                case ConfigType.CDNConfig:
                    AddValue(data, "archives");
                    AddValue(data, "archives-index-size");
                    AddValue(data, "patch-archives");
                    AddValue(data, "patch-archives-index-size");
                    AddValue(data, "file-index");
                    AddValue(data, "file-index-size");
                    AddValue(data, "patch-file-index");
                    AddValue(data, "patch-file-index-size");
                    break;
                default:
                    throw new ArgumentException("Invalid KeyValueConfig type");
            }

            return data;
        }

        public static (string[] Fields, string[] Values) GenerateVarData(ConfigType type)
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
    }
}

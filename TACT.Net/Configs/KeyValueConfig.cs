using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;

namespace TACT.Net.Configs
{
    /// <summary>
    /// A key-value pair config with multiple values per key
    /// </summary>
    public class KeyValueConfig : IConfig
    {
        public ConfigType Type { get; private set; }

        /// <summary>
        /// Hash of the file
        /// </summary>
        public MD5Hash Checksum { get; private set; }

        public List<string> this[string key]
        {
            get
            {
                _data.TryGetValue(key, out var values);
                return values;
            }
        }


        private readonly Dictionary<string, List<string>> _data;

        #region Constructors

        private KeyValueConfig()
        {
            _data = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a new config of <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        public KeyValueConfig(ConfigType type) : this()
        {
            Type = type;

            switch (Type)
            {
                case ConfigType.BuildConfig:
                    _data.Add("root", new List<string> { "" });
                    _data.Add("install", new List<string> { "", "" });
                    _data.Add("install-size", new List<string> { "", "" });
                    _data.Add("download", new List<string> { "", "" });
                    _data.Add("download-size", new List<string> { "", "" });
                    _data.Add("size", new List<string>() { "", "" });
                    _data.Add("size-size", new List<string>() { "", "" });
                    _data.Add("encoding", new List<string> { "", "" });
                    _data.Add("encoding-size", new List<string> { "", "" });
                    _data.Add("build-name", new List<string> { "" });
                    _data.Add("build-uid", new List<string> { "wow" });
                    _data.Add("build-product", new List<string> { "WoW" });
                    _data.Add("build-playbuild-installer", new List<string> { "ngdptool_casc2" });
                    break;
                case ConfigType.CDNConfig:
                    _data.Add("archives", new List<string> { "" });
                    _data.Add("archives-index-size", new List<string> { "" });
                    _data.Add("patch-archives", new List<string> { "" });
                    _data.Add("patch-archives-index-size", new List<string> { "" });
                    _data.Add("file-index", new List<string> { "" });
                    _data.Add("file-index-size", new List<string> { "" });
                    _data.Add("patch-file-index", new List<string> { "" });
                    _data.Add("patch-file-index-size", new List<string> { "" });
                    break;
                default:
                    throw new ArgumentException("Invalid KeyValueConfig type");
            }
        }

        /// <summary>
        /// Loads an existing config of <paramref name="type"/>
        /// </summary>
        /// <param name="hash">File hash</param>
        /// <param name="directory">Root directory</param>
        /// <param name="type"></param>
        public KeyValueConfig(string hash, string directory, ConfigType type) : this()
        {
            string path = Helpers.GetCDNPath(hash, "config", directory);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Unable to load {type}");

            Type = type;
            Checksum = new MD5Hash(hash);

            using (var sr = new StreamReader(path))
                Read(sr);
        }

        public KeyValueConfig(Stream stream, ConfigType type) : this()
        {
            Type = type;

            using (var sr = new StreamReader(stream))
                Read(sr);
        }

        #endregion

        #region Getters and Setters

        /// <summary>
        /// Returns all values
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<string> GetValues(string key)
        {
            _data.TryGetValue(key, out var val);
            return val;
        }

        /// <summary>
        /// Returns the specific value at <paramref name="index"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public string GetValue(string key, int index)
        {
            return GetValues(key)?[index];
        }

        /// <summary>
        /// Replaces all values
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void SetValue(string key, List<string> values)
        {
            if (_data.ContainsKey(key))
                _data[key] = values;
        }

        /// <summary>
        /// Replaces the value at <paramref name="index"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="index"></param>
        public void SetValue(string key, object value, int index)
        {
            if (_data.ContainsKey(key))
                _data[key][index] = value.ToString();
        }

        /// <summary>
        /// Appends a value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddValue(string key, string value)
        {
            if (_data.ContainsKey(key))
                _data[key].Add(value);
        }

        /// <summary>
        /// Appends a value at <paramref name="index"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="index"></param>
        public void InsertValue(string key, string value, int index)
        {
            if (_data.ContainsKey(key))
                _data[key].Insert(index, value);
        }

        /// <summary>
        /// Removes the specified <paramref name="value"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void RemoveValue(string key, string value)
        {
            if (_data.ContainsKey(key))
                _data[key].Remove(value);
        }

        /// <summary>
        /// Removes the value at <paramref name="index"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        public void RemoveValue(string key, int index)
        {
            if (_data.ContainsKey(key))
                _data[key].RemoveAt(index);
        }

        #endregion

        #region IO

        private void Read(TextReader reader)
        {
            string line;
            string[] tokens = null;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                tokens = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2)
                    throw new Exception("Invalid config");

                _data.Add(tokens[0].Trim(), tokens[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList());
            }
        }

        /// <summary>
        /// Saves the config
        /// </summary>
        /// <param name="directory">Root Directory</param>
        public void Write(string directory)
        {
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms))
            {
                // append the comment line
                switch (Type)
                {
                    case ConfigType.BuildConfig:
                        sw.WriteLine("# Build Configuration");
                        break;
                    case ConfigType.CDNConfig:
                        sw.WriteLine("# CDN Configuration");
                        break;
                    case ConfigType.PatchConfig:
                        sw.WriteLine("# Patch Configuration");
                        break;
                }

                // spacer
                sw.WriteLine();

                // write the token and values skipping blanks
                foreach (var data in _data)
                    if (!data.Value.All(x => string.IsNullOrWhiteSpace(x)))
                        sw.WriteLine($"{data.Key} = {string.Join(" ", data.Value)}");

                Checksum = ms.MD5Hash();

                string saveLocation = Helpers.GetCDNPath(Checksum.ToString(), "config", directory);
                File.WriteAllBytes(saveLocation, ms.ToArray());
            }
        }

        #endregion

    }
}

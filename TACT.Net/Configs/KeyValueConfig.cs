using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Configs
{
    using StringCollection = Dictionary<string, List<string>>;

    /// <summary>
    /// A one-to-many KeyValue pair config
    /// </summary>
    public class KeyValueConfig : IConfig
    {
        public ConfigType Type { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public uint SequenceNumber;

        public List<string> this[string key]
        {
            get
            {
                _data.TryGetValue(key, out var values);
                return values;
            }
        }

        private readonly StringCollection _data;

        #region Constructors

        private KeyValueConfig()
        {
            _data = new StringCollection(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a new config of <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        public KeyValueConfig(ConfigType type) : this()
        {
            Type = type;
            _data = ConfigDataFactory.GenerateKeyValueData(type);
        }

        /// <summary>
        /// Loads an existing config of <paramref name="type"/>
        /// </summary>
        /// <param name="hash">File hash</param>
        /// <param name="directory">Root directory</param>
        /// <param name="type"></param>
        public KeyValueConfig(string hash, string directory, ConfigType type) : this()
        {
            Type = type;
            Checksum = new MD5Hash(hash);

            using (var sr = new StreamReader(Helpers.GetCDNPath(hash, "config", directory)))
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
                // skip blank
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // skip comments
                if (line[0] == '#')
                {
                    // grab the sequence number
                    if (line.StartsWith("## seqn", StringComparison.OrdinalIgnoreCase))
                        uint.TryParse(line.Split(' ').Last(), out SequenceNumber);

                    continue;
                }

                tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // special case for PatchConfig's patch entries; store the entry as the SystemFile type
                if (Type == ConfigType.PatchConfig && line.StartsWith("patch-entry"))
                    _data.Add(tokens[2].Trim(), tokens.Skip(3).ToList());
                else
                    _data.Add(tokens[0].Trim(), tokens.Skip(2).ToList());
            }
        }

        /// <summary>
        /// Saves the config
        /// </summary>
        /// <param name="directory">Root Directory</param>
        public void Write(string directory)
        {
            // sort the CDN Config entries
            SortEntries();

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

                // sequence number
                if (SequenceNumber > 0)
                    sw.WriteLine("## seqn " + SequenceNumber);

                // write the token and values skipping blanks
                foreach (var data in _data)
                {
                    if (data.Value.Count > 0 && !data.Value.All(x => string.IsNullOrWhiteSpace(x)))
                    {
                        // special case for PatchConfig's patch entries
                        if (Type == ConfigType.PatchConfig && !data.Key.StartsWith("patch"))
                            sw.WriteLine($"patch-entry = {data.Key} {string.Join(" ", data.Value)}");
                        else
                            sw.WriteLine($"{data.Key} = {string.Join(" ", data.Value)}");
                    }
                }

                sw.Flush();
                Checksum = ms.MD5Hash();

                string saveLocation = Helpers.GetCDNPath(Checksum.ToString(), "config", directory, true);
                File.WriteAllBytes(saveLocation, ms.ToArray());
            }
        }

        #endregion

        #region Helpers

        private void SortEntries()
        {
            if (Type != ConfigType.CDNConfig)
                return;

            string[] items = new[] { "patch-file-index", "file-index", "patch-archives", "archives" };
            foreach (var item in items)
            {
                if (_data.ContainsKey(item) && _data[item].Count > 0)
                    _data[item].Sort(new MD5HashComparer());
            }
        }

        #endregion

    }
}

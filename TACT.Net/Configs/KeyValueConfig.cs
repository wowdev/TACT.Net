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
        public KeyValueConfig(ConfigType type, uint build = 99999)
        {
            Type = type;
            _data = ConfigDataFactory.GenerateData(type, build);
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

            string path = Helpers.GetCDNPath(hash, "config", directory);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Unable to load {type} config", path);

            using (var sr = new StreamReader(path))
                Read(sr);
        }

        /// <summary>
        /// Loads an existing config of <paramref name="type"/>
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        public KeyValueConfig(Stream stream, ConfigType type) : this()
        {
            Type = type;

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new NotSupportedException($"Unable to read {type} stream");

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
        public string GetValue(string key, int index = 0)
        {
            var values = GetValues(key);
            if (values == null || values.Count <= index)
                return null;

            return values[index];
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
        public void SetValue(string key, object value, int index = 0)
        {
            if (_data.TryGetValue(key, out var values))
                if (values.Count > index)
                    values[index] = value.ToString();
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
            if (_data.TryGetValue(key, out var values))
                values.Insert(index, value);
        }

        /// <summary>
        /// Removes the specified <paramref name="value"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public bool RemoveValue(string key, string value)
        {
            return _data.ContainsKey(key) && _data[key].Remove(value);
        }

        /// <summary>
        /// Removes the value at <paramref name="index"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        public bool RemoveValue(string key, int index)
        {
            if (_data.TryGetValue(key, out var values))
            {
                if (values.Count > index)
                {
                    values.RemoveAt(index);
                    return true;
                }                    
            }

            return false;               
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

        /// <summary>
        /// Sorts the CDN Config archive names
        /// </summary>
        private void SortEntries()
        {
            if (Type != ConfigType.CDNConfig)
                return;

            // archive collection - size collection
            string[,] entries = new string[,]
            {
                { "patch-file-index", "patch-file-index-size"  },
                { "file-index",       "file-index-size"  },
                { "patch-archives",   "patch-archives-index-size"  },
                { "archives",         "archives-index-size"  },
            };

            var comparer = new MD5HashComparer();

            for (int i = 0; i < entries.GetLength(0); i++)
            {
                if (_data.TryGetValue(entries[i, 0], out var values) && values.Count > 1)
                {
                    if (!_data.TryGetValue(entries[i, 1], out var sizes))
                    {
                        values.Sort(comparer);
                    }
                    else
                    {
                        // sizes and values must maintain same index
                        var sort = values.Zip(sizes, (v, s) => (v, s)).OrderBy(x => x.v, comparer).ToArray();
                        for (int j = 0; j < sort.Length; j++)
                        {
                            values[j] = sort[j].v;
                            sizes[j] = sort[j].s;
                        }
                    }
                }
            }

        }

        #endregion

    }
}

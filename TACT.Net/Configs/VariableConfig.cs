using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TACT.Net.Common;

namespace TACT.Net.Configs
{
    /// <summary>
    /// A data table config with a Locale primary key
    /// </summary>
    public class VariableConfig : IConfig
    {
        public ConfigType Type { get; private set; }

        public Dictionary<string, string> this[Locale locale]
        {
            get
            {
                _data.TryGetValue(locale, out var values);
                return values;
            }
        }

        private readonly Dictionary<Locale, Dictionary<string, string>> _data;
        private string[] _fields;
        private readonly string _localeKey;

        #region Constructors

        /// <summary>
        /// Creates a new config of <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        public VariableConfig(ConfigType type)
        {
            Type = type;
            _data = new Dictionary<Locale, Dictionary<string, string>>();
            _localeKey = type == ConfigType.CDNs ? "Name" : "Region";

            string[] tokens;
            switch (Type)
            {
                case ConfigType.CDNs:
                    _fields = "Name!STRING:0|Path!STRING:0|Hosts!STRING:0|Servers!STRING:0|ConfigPath!STRING:0".Split('|');
                    tokens = new[] { "", "tpr/wow", "", "", "tpr/configs/data" };
                    break;
                case ConfigType.Versions:
                    _fields = "Region!STRING:0|BuildConfig!HEX:16|CDNConfig!HEX:16|KeyRing!HEX:16|BuildId!DEC:4|VersionsName!String:0|ProductConfig!HEX:16".Split('|');
                    tokens = new[] { "", "", "", "", "00000", "0.0.0.00000", "" };
                    break;
                default:
                    throw new ArgumentException("Invalid VariableConfig type");
            }

            // generate all Locales
            string[] fields = _fields.Select(x => x.Split('!')[0].Replace(" ", "")).ToArray();
            foreach (var locale in Enum.GetValues(typeof(Locale)))
            {
                tokens[0] = locale.ToString();
                PopulateCollection(fields, tokens, (Locale)locale);
            }
        }

        /// <summary>
        /// Loads an existing config file
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <param name="type"></param>
        public VariableConfig(string directory, ConfigType type)
        {
            Type = type;
            _data = new Dictionary<Locale, Dictionary<string, string>>();
            _localeKey = type == ConfigType.CDNs ? "Name" : "Region";

            string path = Path.Combine(directory, type.ToString());
            if (!File.Exists(path))
                throw new FileNotFoundException($"Unable to load {type}");

            Type = type;

            using (var sr = new StreamReader(path))
                Read(sr);
        }

        /// <summary>
        /// Loads an existing config from a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        public VariableConfig(Stream stream, ConfigType type)
        {
            Type = type;
            _data = new Dictionary<Locale, Dictionary<string, string>>();
            _localeKey = type == ConfigType.CDNs ? "Name" : "Region";

            using (var sr = new StreamReader(stream))
                Read(sr);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets a field's value for all locales
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void SetValue(string field, string value)
        {
            foreach (var collection in _data.Values)
                if (collection.ContainsKey(field))
                    collection[field] = value;
        }

        /// <summary>
        /// Sets a field's value for a specific locale
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="locale"></param>
        public void SetValue(string field, string value, Locale locale)
        {
            if (_data.TryGetValue(locale, out var collection) && collection.ContainsKey(field))
                collection[field] = value;
        }

        /// <summary>
        /// Gets the field value for all locales
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public IEnumerable<string> GetValues(string field)
        {
            foreach (var collection in _data.Values)
                if (collection.ContainsKey(field))
                    yield return collection[field];
        }

        /// <summary>
        /// Gets the field value for a specific locale
        /// </summary>
        /// <param name="field"></param>
        /// <param name="locale"></param>
        /// <returns></returns>
        public string GetValue(string field, Locale locale)
        {
            string val = "";
            if (_data.TryGetValue(locale, out var collection))
                collection.TryGetValue(field, out val);

            return val;
        }

        /// <summary>
        /// Adds a new locale with the provided <paramref name="values"/>
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="values"></param>
        public void AddLocale(Locale locale, string[] values)
        {
            if (_data.ContainsKey(locale))
                return;

            string[] fields = _data.First().Value.Values.ToArray();
            if (fields.Length != values.Length)
                throw new ArgumentException($"Invalid values count. Expecting {fields.Length} got {values.Length}");

            PopulateCollection(fields, values, locale);
        }

        public bool RemoveLocale(Locale locale) => _data.Remove(locale);

        public bool HasLocale(Locale locale) => _data.ContainsKey(locale);

        #endregion

        #region IO

        private void Read(TextReader reader)
        {
            string line;
            string[] tokens, fields = null;
            int localeIndex = -1;

            while ((line = reader.ReadLine()) != null)
            {
                // skip blank lines and comments
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                    continue;

                // split line into tokens
                tokens = line.Split('|');

                if (fields == null)
                {
                    // define fields and locale index
                    fields = tokens.Select(x => x.Split('!')[0].Replace(" ", "")).ToArray();
                    localeIndex = Array.FindIndex(fields, t => t.Equals(_localeKey, StringComparison.OrdinalIgnoreCase));
                    _fields = tokens;

                    if (localeIndex == -1)
                        throw new FormatException("Config malformed. Missing Locale informaion.");
                }
                else
                {
                    if (Enum.TryParse(typeof(Locale), tokens[localeIndex], true, out var _locale))
                        PopulateCollection(fields, tokens, (Locale)_locale);
                }
            }
        }

        /// <summary>
        /// Saves the config
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <param name="product"></param>
        public void Write(string directory, string product)
        {
            using (var md5 = MD5.Create())
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms))
            {
                sw.NewLine = "\n";

                // write the field tokens
                sw.WriteLine(string.Join("|", _fields));
                sw.WriteLine();

                // write the values for each locale
                foreach (var collection in _data.Values)
                    sw.WriteLine(string.Join("|", collection.Values));

                sw.Flush();

                string saveLocation = Path.Combine(directory, product, md5.ComputeHash(ms).ToHex());
                File.WriteAllBytes(saveLocation, ms.ToArray());
            }
        }

        #endregion

        #region Helpers

        private void PopulateCollection(string[] fields, string[] values, Locale locale)
        {
            var collection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < fields.Length; i++)
                collection.Add(fields[i], values[i]);

            _data.Add(locale, collection);
        }

        #endregion
    }
}

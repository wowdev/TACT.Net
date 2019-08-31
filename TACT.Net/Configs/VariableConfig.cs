using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;

namespace TACT.Net.Configs
{
    using StringPair = Dictionary<string, string>;

    /// <summary>
    /// A data table config with a Locale primary key
    /// </summary>
    public class VariableConfig : IConfig
    {
        public ConfigType Type { get; private set; }

        public StringPair this[Locale locale]
        {
            get
            {
                _data.TryGetValue(locale, out var values);
                return values;
            }
        }

        private readonly Dictionary<Locale, StringPair> _data;
        private string[] _fields;

        #region Constructors

        private VariableConfig()
        {
            _data = new Dictionary<Locale, StringPair>();
        }

        /// <summary>
        /// Creates a new config of <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        public VariableConfig(ConfigType type) : this()
        {
            Type = type;

            var (Fields, Values) = ConfigDataFactory.GenerateData(type);

            // set the field names with structual descriptions
            _fields = Fields;

            // generate all Locales
            var fields = DestructFieldNames(Fields);
            foreach (var locale in Enum.GetValues(typeof(Locale)))
            {
                Values[0] = locale.ToString().ToLower();
                PopulateCollection(fields, Values, (Locale)locale);
            }
        }

        /// <summary>
        /// Loads an existing config file of <paramref name="type"/>
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <param name="type"></param>
        public VariableConfig(string directory, ConfigType type) : this()
        {
            Type = type;

            string path = Path.Combine(directory, type.ToString().ToLowerInvariant());
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
        public VariableConfig(Stream stream, ConfigType type) : this()
        {
            Type = type;

            if(stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new NotSupportedException($"Unable to read {type} stream");

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
        public void SetValue(string field, object value)
        {
            foreach (var collection in _data.Values)
                if (collection.ContainsKey(field))
                    collection[field] = value.ToString();
        }

        /// <summary>
        /// Sets a field's value for a specific locale
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="locale"></param>
        public void SetValue(string field, object value, Locale locale)
        {
            if (_data.TryGetValue(locale, out var collection) && collection.ContainsKey(field))
                collection[field] = value.ToString();
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
        public bool AddLocale(Locale locale, object[] values)
        {
            if (_data.ContainsKey(locale))
                return false;

            var fields = DestructFieldNames(_fields);
            if (fields.Length != values.Length)
                throw new ArgumentException($"Invalid values count. Expecting {fields.Length} got {values.Length}");

            PopulateCollection(fields, Array.ConvertAll(values, x => x.ToString()), locale);
            return true;
        }

        /// <summary>
        /// Removes a specific locale from the collection
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
        public bool RemoveLocale(Locale locale) => _data.Remove(locale);

        /// <summary>
        /// Determines if a specific locale exists
        /// </summary>
        /// <param name="locale"></param>
        /// <returns></returns>
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
                    // set the field names with structual descriptions
                    _fields = tokens;

                    // extract the field names without their structual descriptions
                    fields = DestructFieldNames(tokens);

                    // get the index of the primary key
                    localeIndex = fields.IndexOf(Type == ConfigType.CDNs ? "Name" : "Region");
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
            string saveDir = Directory.CreateDirectory(Path.Combine(directory, product)).FullName;
            string saveLocation = Path.Combine(saveDir, Type.ToString().ToLowerInvariant());

            using (var sw = new StreamWriter(saveLocation))
            {
                sw.NewLine = "\n";

                // write the field information
                sw.WriteLine(string.Join("|", _fields));

                // write the values for each locale
                foreach (var collection in _data.Values)
                    sw.WriteLine(string.Join("|", collection.Values));
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Adds a new locale group to the collection
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="values"></param>
        /// <param name="locale"></param>
        private void PopulateCollection(string[] fields, string[] values, Locale locale)
        {
            // unused combination
            if (Type == ConfigType.CDNs && locale == Locale.XX)
                return;

            var collection = new StringPair(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < fields.Length; i++)
                collection.Add(fields[i], values[i]);

            _data.Add(locale, collection);
        }

        /// <summary>
        /// Removes the format information from the fields
        /// </summary>
        /// <param name="fields"></param>
        /// <returns></returns>
        private string[] DestructFieldNames(string[] fields)
        {
            return fields.Select(x => x.Split('!')[0]).ToArray();
        }

        #endregion
    }
}

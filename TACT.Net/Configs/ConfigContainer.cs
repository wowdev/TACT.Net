using System;
using TACT.Net.Common.Cryptography;
using TACT.Net.SystemFiles.Shared;

namespace TACT.Net.Configs
{
    /// <summary>
    /// A container for the various configs used within TACT
    /// </summary>
    public class ConfigContainer : SystemFileBase
    {
        #region Configs

        /// <summary>
        /// Lists key file hashes and sizes plus product details
        /// </summary>
        public KeyValueConfig BuildConfig { get; private set; }
        /// <summary>
        /// Lists all data archives including patches and loose files
        /// </summary>
        public KeyValueConfig CDNConfig { get; private set; }
        /// <summary>
        /// Lists the various patch files and their size, encoding and checksums
        /// </summary>
        public KeyValueConfig PatchConfig { get; private set; }
        /// <summary>
        /// Information for downloading files by region
        /// </summary>
        public VariableConfig CDNsFile { get; private set; }
        /// <summary>
        /// Lists the Build and CDN configs and product details by region
        /// </summary>
        public VariableConfig VersionsFile { get; private set; }

        #endregion

        #region Keys

        public MD5Hash BuildConfigMD5 => TryGetKey(VersionsFile, "buildconfig");
        public MD5Hash CDNConfigMD5 => TryGetKey(VersionsFile, "cdnconfig");
        public MD5Hash PatchConfigMD5 => TryGetKey(VersionsFile, "patch-config");
        public MD5Hash RootMD5 => TryGetKey(BuildConfig, "root");
        public MD5Hash EncodingMD5 => TryGetKey(BuildConfig, "encoding");
        public MD5Hash EncodingEKey => TryGetKey(BuildConfig, "encoding", 1);
        public MD5Hash InstallMD5 => TryGetKey(BuildConfig, "install", 1);
        public MD5Hash DownloadMD5 => TryGetKey(BuildConfig, "download", 1);
        public MD5Hash DownloadSizeMD5 => TryGetKey(BuildConfig, "size", 1);
        public MD5Hash PatchMD5 => TryGetKey(BuildConfig, "patch", 1);

        #endregion

        /// <summary>
        /// The Blizzard Product Code
        /// </summary>
        public readonly string Product;
        /// <summary>
        /// Current Locale
        /// </summary>
        public Locale Locale;

        #region Constructors

        public ConfigContainer(string product, TACT container = null) : base(container)
        {
            Product = product;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new set of configs
        /// </summary>
        public void Create()
        {
            CDNsFile = new VariableConfig(ConfigType.CDNs);
            VersionsFile = new VariableConfig(ConfigType.Versions);
            BuildConfig = new KeyValueConfig(ConfigType.BuildConfig);
            CDNConfig = new KeyValueConfig(ConfigType.CDNConfig);

            Locale = Locale.US;
        }

        /// <summary>
        /// Opens an existing set of configs
        /// </summary>
        /// <param name="directory"></param>
        public void Open(string directory, Locale locale)
        {
            // load the primary configs
            CDNsFile = new VariableConfig(directory, Product, ConfigType.CDNs);
            VersionsFile = new VariableConfig(directory, Product, ConfigType.Versions);

            if (!VersionsFile.HasLocale(locale))
                throw new Exception($"Versions missing {locale} locale");

            // set the current locale
            Locale = locale;

            // load the localised configs
            BuildConfig = new KeyValueConfig(BuildConfigMD5.ToString(), directory, ConfigType.BuildConfig);
            CDNConfig = new KeyValueConfig(CDNConfigMD5.ToString(), directory, ConfigType.CDNConfig);
            PatchConfig = new KeyValueConfig(PatchConfigMD5.ToString(), directory, ConfigType.PatchConfig);
        }

        /// <summary>
        /// Saves the configs using to the Blizzard standard location
        /// </summary>
        /// <param name="directory"></param>
        public void Save(string directory)
        {
            // save the localised configs
            BuildConfig.Write(directory);
            CDNConfig.Write(directory);

            // update the hashes
            VersionsFile.SetValue("buildconfig", BuildConfig.Checksum.ToString());
            VersionsFile.SetValue("cdnconfig", CDNConfig.Checksum.ToString());

            // save the primary configs
            CDNsFile.Write(directory, Product);
            VersionsFile.Write(directory, Product);
        }

        #endregion

        #region Helpers

        private MD5Hash TryGetKey(IConfig config, string identifier, int index = 0)
        {
            MD5Hash hash = default(MD5Hash);

            if (config is VariableConfig _varConf)
                MD5Hash.TryParse(_varConf.GetValue(identifier, Locale), out hash);
            else if (config is KeyValueConfig _keyConf)
                MD5Hash.TryParse(_keyConf.GetValue(identifier, index), out hash);

            return hash;
        }

        #endregion
    }
}

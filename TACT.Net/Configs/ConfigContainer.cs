using System;
using TACT.Net.Cryptography;
using TACT.Net.SystemFiles;

namespace TACT.Net.Configs
{
    /// <summary>
    /// A container for the various configs used within TACT
    /// </summary>
    public class ConfigContainer : ISystemFile
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
        public MD5Hash PatchConfigMD5 => TryGetKey(BuildConfig, "patch-config");
        public MD5Hash RootMD5 => TryGetKey(BuildConfig, "root");
        public MD5Hash EncodingMD5 => TryGetKey(BuildConfig, "encoding");
        public MD5Hash EncodingEKey => TryGetKey(BuildConfig, "encoding", 1);
        public MD5Hash InstallMD5 => TryGetKey(BuildConfig, "install", 1);
        public MD5Hash DownloadMD5 => TryGetKey(BuildConfig, "download", 1);
        public MD5Hash DownloadSizeMD5 => TryGetKey(BuildConfig, "size", 1);
        public MD5Hash PatchMD5 => TryGetKey(BuildConfig, "patch");

        #endregion

        /// <summary>
        /// The Blizzard Product Code
        /// </summary>
        public readonly string Product;
        /// <summary>
        /// Current Locale
        /// </summary>
        public readonly Locale Locale;

        #region Constructors

        public ConfigContainer(string product, Locale locale)
        {
            Product = product;
            Locale = locale;
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
        }

        /// <summary>
        /// Opens the CDNs, Versions and config files from disk
        /// </summary>
        /// <param name="cdnverDirectory">Directory containing the CDNs and Versions files</param>
        /// <param name="configDirectory">Directory containing the config files</param>
        public void OpenLocal(string cdnverDirectory, string configDirectory)
        {
            CDNsFile = new VariableConfig(cdnverDirectory, ConfigType.CDNs);
            VersionsFile = new VariableConfig(cdnverDirectory, ConfigType.Versions);

            LoadConfigs(configDirectory);
        }

        /// <summary>
        /// Opens the CDNs and Versions files from Ribbit and the config files from disk
        /// </summary>
        public void OpenRemote(string directory)
        {
            var ribbit = new Ribbit.RibbitClient(Locale);
            CDNsFile = new VariableConfig(ribbit.GetStream($"v1/products/{Product}/cdns"), ConfigType.CDNs);
            VersionsFile = new VariableConfig(ribbit.GetStream($"v1/products/{Product}/versions"), ConfigType.Versions);

            LoadConfigs(directory);
        }

        /// <summary>
        /// Loads the Build, CDN and Patch configs
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="locale"></param>
        private void LoadConfigs(string directory)
        {
            if (VersionsFile == null || CDNsFile == null)
                throw new Exception("Versions and CDNs files must be loaded first");

            if (!VersionsFile.HasLocale(Locale))
                throw new Exception($"Versions missing {Locale} locale");

            if (!BuildConfigMD5.IsEmpty)
                BuildConfig = new KeyValueConfig(BuildConfigMD5.ToString(), directory, ConfigType.BuildConfig);

            if (!CDNConfigMD5.IsEmpty)
                CDNConfig = new KeyValueConfig(CDNConfigMD5.ToString(), directory, ConfigType.CDNConfig);

            if (!PatchConfigMD5.IsEmpty)
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
            MD5Hash hash = default;

            if (config is VariableConfig _varConf)
                MD5Hash.TryParse(_varConf.GetValue(identifier, Locale), out hash);
            else if (config is KeyValueConfig _keyConf)
                MD5Hash.TryParse(_keyConf.GetValue(identifier, index), out hash);

            return hash;
        }

        #endregion
    }
}

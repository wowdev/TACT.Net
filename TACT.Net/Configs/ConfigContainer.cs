using System;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Network;

namespace TACT.Net.Configs
{
    /// <summary>
    /// A container for the various configs used within TACT
    /// </summary>
    public class ConfigContainer
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
        public KeyValueConfig PatchConfig { get; set; }
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
        public MD5Hash RootCKey => TryGetKey(BuildConfig, "root");
        public MD5Hash EncodingCKey => TryGetKey(BuildConfig, "encoding");
        public MD5Hash EncodingEKey => TryGetKey(BuildConfig, "encoding", 1);
        public MD5Hash InstallCKey => TryGetKey(BuildConfig, "install");
        public MD5Hash InstallEKey => TryGetKey(BuildConfig, "install", 1);
        public MD5Hash DownloadCKey => TryGetKey(BuildConfig, "download");
        public MD5Hash DownloadEKey => TryGetKey(BuildConfig, "download", 1);
        public MD5Hash DownloadSizeCKey => TryGetKey(BuildConfig, "size");
        public MD5Hash DownloadSizeEKey => TryGetKey(BuildConfig, "size", 1);
        public MD5Hash PatchEKey => TryGetKey(BuildConfig, "patch");

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
        /// <param name="build">Optionally loads build specific config values</param>
        public void Create(uint build = 0)
        {
            CDNsFile = new VariableConfig(ConfigType.CDNs);
            VersionsFile = new VariableConfig(ConfigType.Versions);
            BuildConfig = new KeyValueConfig(ConfigType.BuildConfig);
            CDNConfig = new KeyValueConfig(ConfigType.CDNConfig);
        }

        /// <summary>
        /// Opens the CDNs, Versions and config files from disk
        /// </summary>
        /// <param name="directory">Directory containing the config files</param>
        public void OpenLocal(string directory)
        {
            CDNsFile = new VariableConfig(Path.Combine(directory, Product), ConfigType.CDNs);
            VersionsFile = new VariableConfig(Path.Combine(directory, Product), ConfigType.Versions);

            LoadConfigs(directory);
        }

        /// <summary>
        /// Opens the CDNs, Versions from Ribbit and the config files from Blizzard's CDN
        /// </summary>
        public void OpenRemote()
        {
            var ribbit = new RibbitClient(Locale);

            using (var cdnstream = ribbit.GetStream(RibbitCommand.CDNs, Product).Result)
            using (var verstream = ribbit.GetStream(RibbitCommand.Versions, Product).Result)
            {
                CDNsFile = new VariableConfig(cdnstream, ConfigType.CDNs);
                VersionsFile = new VariableConfig(verstream, ConfigType.Versions);

                if (!VersionsFile.HasLocale(Locale))
                    throw new Exception($"Versions missing {Locale} locale");

                var cdnClient = new CDNClient(this);

                if (BuildConfigMD5.Value != null)
                {
                    string configUrl = Helpers.GetCDNUrl(BuildConfigMD5.ToString(), "config");
                    BuildConfig = new KeyValueConfig(cdnClient.OpenStream(configUrl).Result, ConfigType.BuildConfig);
                }

                if (CDNConfigMD5.Value != null)
                {
                    string configUrl = Helpers.GetCDNUrl(CDNConfigMD5.ToString(), "config");
                    CDNConfig = new KeyValueConfig(cdnClient.OpenStream(configUrl).Result, ConfigType.CDNConfig);
                }

                if (PatchConfigMD5.Value != null)
                {
                    string configUrl = Helpers.GetCDNUrl(PatchConfigMD5.ToString(), "config");
                    PatchConfig = new KeyValueConfig(cdnClient.OpenStream(configUrl).Result, ConfigType.PatchConfig);
                }
            }
        }

        /// <summary>
        /// Download and load the config files from remote
        /// </summary>
        /// <param name="directory"></param>
        public void DownloadRemote(string directory)
        {
            OpenRemote();
            Save(directory);
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

            if (BuildConfigMD5.Value != null)
                BuildConfig = new KeyValueConfig(BuildConfigMD5.ToString(), directory, ConfigType.BuildConfig);

            if (CDNConfigMD5.Value != null)
                CDNConfig = new KeyValueConfig(CDNConfigMD5.ToString(), directory, ConfigType.CDNConfig);

            // optionally load the patch config
            if (PatchConfigMD5.Value != null)
            {
                string path = Helpers.GetCDNPath(PatchConfigMD5.ToString(), "config", directory);
                if (File.Exists(path))
                    PatchConfig = new KeyValueConfig(PatchConfigMD5.ToString(), directory, ConfigType.PatchConfig);
            }
        }

        /// <summary>
        /// Saves the configs using to the Blizzard standard location
        /// </summary>
        /// <param name="directory"></param>
        public void Save(string directory)
        {
            // save and update patch config value
            if (PatchConfig != null)
            {
                PatchConfig?.Write(directory);
                BuildConfig?.SetValue("patch-config", PatchConfig.Checksum.ToString());
            }

            // save the localised configs
            BuildConfig?.Write(directory);
            CDNConfig?.Write(directory);

            // update the hashes
            VersionsFile.SetValue("buildconfig", BuildConfig.Checksum.ToString());
            VersionsFile.SetValue("cdnconfig", CDNConfig.Checksum.ToString());

            // save the primary configs
            CDNsFile.Write(directory, Product);
            VersionsFile.Write(directory, Product);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the lookup hash for the supplied SystemFile type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MD5Hash GetSystemFileHash<T>() where T : ISystemFile
        {
            var type = typeof(T);

            switch (true)
            {
                case true when type == typeof(Root.RootFile):
                    return TryGetKey(BuildConfig, "root");
                case true when type == typeof(Encoding.EncodingFile):
                    return TryGetKey(BuildConfig, "encoding", 1);
                case true when type == typeof(Install.InstallFile):
                    return TryGetKey(BuildConfig, "install");
                case true when type == typeof(Download.DownloadFile):
                    return TryGetKey(BuildConfig, "download");
                case true when type == typeof(Download.DownloadSizeFile):
                    return TryGetKey(BuildConfig, "size");
                case true when type == typeof(Patch.PatchFile):
                    return TryGetKey(BuildConfig, "patch");
            }

            return default;
        }

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

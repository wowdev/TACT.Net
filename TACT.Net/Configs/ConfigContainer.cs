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

        #endregion

        #region Keys

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

        #region Methods

        /// <summary>
        /// Creates a new set of configs
        /// </summary>
        /// <param name="build">Optionally loads build specific config values</param>
        public void Create()
        {
            BuildConfig = new KeyValueConfig(ConfigType.BuildConfig);
            CDNConfig = new KeyValueConfig(ConfigType.CDNConfig);
        }

        /// <summary>
        /// Loads the Build, CDN and Patch configs from disk
        /// </summary>
        /// <param name="directory">Directory containing the config files</param>
        public void OpenLocal(string directory, ManifestContainer manifestContainer)
        {
            if (manifestContainer?.VersionsFile == null || manifestContainer?.CDNsFile == null)
                throw new Exception("Versions and CDNs files must be loaded first");

            if (!manifestContainer.VersionsFile.HasLocale(manifestContainer.Locale))
                throw new Exception($"Versions missing {manifestContainer.Locale} locale");

            if (manifestContainer.BuildConfigMD5.Value != null)
                BuildConfig = new KeyValueConfig(manifestContainer.BuildConfigMD5.ToString(), directory, ConfigType.BuildConfig);

            if (manifestContainer.CDNConfigMD5.Value != null)
                CDNConfig = new KeyValueConfig(manifestContainer.CDNConfigMD5.ToString(), directory, ConfigType.CDNConfig);

            // optionally load the patch config
            if (PatchConfigMD5.Value != null)
            {
                string path = Helpers.GetCDNPath(PatchConfigMD5.ToString(), "config", directory);
                if (File.Exists(path))
                    PatchConfig = new KeyValueConfig(PatchConfigMD5.ToString(), directory, ConfigType.PatchConfig);
            }
        }

        /// <summary>
        /// Opens the config files from disk
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="buildConfigMD5"></param>
        /// <param name="cdnConfigMD5"></param>
        /// <param name="patchConfigMD5"></param>
        public void OpenLocal(string directory, string buildConfigMD5, string cdnConfigMD5, string patchConfigMD5 = null)
        {
            if (!string.IsNullOrWhiteSpace(buildConfigMD5))
                BuildConfig = new KeyValueConfig(buildConfigMD5, directory, ConfigType.BuildConfig);

            if (!string.IsNullOrWhiteSpace(cdnConfigMD5))
                CDNConfig = new KeyValueConfig(cdnConfigMD5, directory, ConfigType.CDNConfig);

            // optionally load the patch config
            if (!string.IsNullOrWhiteSpace(patchConfigMD5))
            {
                string path = Helpers.GetCDNPath(patchConfigMD5, "config", directory);
                if (File.Exists(path))
                    PatchConfig = new KeyValueConfig(patchConfigMD5, directory, ConfigType.PatchConfig);
            }
        }

        /// <summary>
        /// Opens the CDNs, Versions from Ribbit and the config files from Blizzard's CDN
        /// </summary>
        public void OpenRemote(ManifestContainer manifestContainer)
        {
            if (manifestContainer?.VersionsFile == null || manifestContainer?.CDNsFile == null)
                throw new Exception("Versions and CDNs files must be loaded first");

            if (!manifestContainer.VersionsFile.HasLocale(manifestContainer.Locale))
                throw new Exception($"Versions missing {manifestContainer.Locale} locale");

            var cdnClient = new CDNClient(manifestContainer);

            if (manifestContainer.BuildConfigMD5.Value != null)
            {
                string configUrl = Helpers.GetCDNUrl(manifestContainer.BuildConfigMD5.ToString(), "config");
                BuildConfig = new KeyValueConfig(cdnClient.OpenStream(configUrl).Result, ConfigType.BuildConfig);
            }

            if (manifestContainer.CDNConfigMD5.Value != null)
            {
                string configUrl = Helpers.GetCDNUrl(manifestContainer.CDNConfigMD5.ToString(), "config");
                CDNConfig = new KeyValueConfig(cdnClient.OpenStream(configUrl).Result, ConfigType.CDNConfig);
            }

            if (PatchConfigMD5.Value != null)
            {
                string configUrl = Helpers.GetCDNUrl(PatchConfigMD5.ToString(), "config");
                PatchConfig = new KeyValueConfig(cdnClient.OpenStream(configUrl).Result, ConfigType.PatchConfig);
            }
        }

        /// <summary>
        /// Download and load the config files from remote
        /// </summary>
        /// <param name="directory"></param>
        public void DownloadRemote(string directory, ManifestContainer manifestContainer)
        {
            OpenRemote(manifestContainer);
            Save(directory);
        }

        /// <summary>
        /// Saves the configs using to the Blizzard standard location
        /// </summary>
        /// <param name="directory"></param>
        public void Save(string directory, ManifestContainer manifestContainer = null)
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
            manifestContainer?.VersionsFile.SetValue("buildconfig", BuildConfig.Checksum.ToString());
            manifestContainer?.VersionsFile.SetValue("cdnconfig", CDNConfig.Checksum.ToString());
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

        private MD5Hash TryGetKey(KeyValueConfig config, string identifier, int index = 0)
        {
            MD5Hash.TryParse(config.GetValue(identifier, index), out MD5Hash hash);
            return hash;
        }

        #endregion
    }
}

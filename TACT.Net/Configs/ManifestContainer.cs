using System.IO;
using TACT.Net.Cryptography;
using TACT.Net.Network;

namespace TACT.Net.Configs
{
    public class ManifestContainer
    {
        #region Manifests

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

        public ManifestContainer(string product, Locale locale)
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
        public void Create()
        {
            CDNsFile = new VariableConfig(ConfigType.CDNs);
            VersionsFile = new VariableConfig(ConfigType.Versions);
        }

        /// <summary>
        /// Opens the CDNs and Versions, as files, from disk
        /// </summary>
        /// <param name="directory">Directory containing the config files</param>
        public void OpenLocal(string directory)
        {
            CDNsFile = new VariableConfig(directory, ConfigType.CDNs);
            VersionsFile = new VariableConfig(directory, ConfigType.Versions);
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
        /// Saves the configs using to the Blizzard standard location
        /// </summary>
        /// <param name="directory"></param>
        public void Save(string directory)
        {
            CDNsFile?.Write(directory, Product);
            VersionsFile?.Write(directory, Product);
        }

        #endregion


        #region Helpers

        private MD5Hash TryGetKey(VariableConfig config, string identifier)
        {
            MD5Hash.TryParse(config.GetValue(identifier, Locale), out MD5Hash hash);
            return hash;
        }

        #endregion
    }
}

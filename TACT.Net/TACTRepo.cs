using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Network;

[assembly: InternalsVisibleTo("TACT.Net.Tests")]
namespace TACT.Net
{
    public sealed class TACTRepo
    {
        public readonly string BaseDirectory;

        public uint Build { get; private set; }

        #region System Files

        public Configs.ConfigContainer ConfigContainer { get; set; }
        public Indices.IndexContainer IndexContainer { get; set; }
        public Encoding.EncodingFile EncodingFile { get; set; }
        public Root.RootFile RootFile { get; set; }
        public Download.DownloadFile DownloadFile { get; set; }
        public Download.DownloadSizeFile DownloadSizeFile { get; set; }
        public Install.InstallFile InstallFile { get; set; }
        public Patch.PatchFile PatchFile { get; set; }

        #endregion

        #region Constructors

        public TACTRepo(string baseDirectory = "")
        {
            BaseDirectory = baseDirectory;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new TACT container populated with: defaulted configs, an index container 
        /// and an empty root, encoding, install and download file
        /// </summary>
        /// <param name="product"></param>
        /// <param name="locale"></param>
        /// <param name="build"></param>
        public void Create(string product, Locale locale, uint build)
        {
            Build = build;

            ConfigContainer = new Configs.ConfigContainer(product, locale);
            ConfigContainer.Create();

            IndexContainer = new Indices.IndexContainer();
            RootFile = new Root.RootFile();
            EncodingFile = new Encoding.EncodingFile();
            InstallFile = new Install.InstallFile();
            DownloadFile = new Download.DownloadFile();

            ApplyVersionSpecificSettings(build);

            // set the default tag entries
            InstallFile.SetDefaultTags(build);
            DownloadFile.SetDefaultTags(build);
            DownloadSizeFile?.SetDefaultTags(build);
        }

        /// <summary>
        /// Opens an existing TACT container and loads the Root and Encoding files
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="product"></param>
        /// <param name="locale"></param>
        public void Open(string directory, string product, Locale locale)
        {
            ConfigContainer = new Configs.ConfigContainer(product, locale);
            ConfigContainer.OpenLocal(directory);

            if (uint.TryParse(ConfigContainer?.VersionsFile?.GetValue("BuildId", locale), out uint build))
                Build = build;

            ApplyVersionSpecificSettings(build);

            IndexContainer = new Indices.IndexContainer();
            IndexContainer.Open(directory);

            if (!ConfigContainer.EncodingEKey.IsEmpty)
            {
                EncodingFile = new Encoding.EncodingFile(BaseDirectory, ConfigContainer.EncodingEKey);

                // Open RootFile
                if (ConfigContainer.RootCKey.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.RootCKey, out var rootEKey))
                    RootFile = new Root.RootFile(BaseDirectory, rootEKey.EKey);

                // Open InstallFile
                if (ConfigContainer.InstallCKey.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.InstallCKey, out var installEKey))
                    InstallFile = new Install.InstallFile(BaseDirectory, installEKey.EKey);

                // Open DownloadFile
                if (ConfigContainer.DownloadCKey.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadCKey, out var downloadEKey))
                    DownloadFile = new Download.DownloadFile(BaseDirectory, downloadEKey.EKey);

                // Open DownloadSizeFile
                if (ConfigContainer.DownloadSizeCKey.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadSizeCKey, out var downloadSizeEKey))
                    DownloadSizeFile = new Download.DownloadSizeFile(BaseDirectory, downloadSizeEKey.EKey);
            }

            // Open PatchFile
            if (ConfigContainer.PatchEKey.Value != null)
                PatchFile = new Patch.PatchFile(BaseDirectory, ConfigContainer.PatchEKey);

            ApplyVersionSpecificSettings(Build);
        }

        /// <summary>
        /// Streams an existing TACT container from an external CDN
        /// </summary>
        /// <param name="product"></param>
        /// <param name="locale"></param>
        public void OpenRemote(string product, Locale locale)
        {
            ConfigContainer = new Configs.ConfigContainer(product, locale);
            ConfigContainer.OpenRemote();

            if (uint.TryParse(ConfigContainer?.VersionsFile?.GetValue("BuildId", locale), out uint build))
                Build = build;

            // stream Indicies
            IndexContainer = new Indices.IndexContainer();
            IndexContainer.OpenRemote(ConfigContainer, true);

            var cdnClient = new CDNClient(ConfigContainer);

            if (ConfigContainer.EncodingEKey.Value != null)
            {
                // Stream EncodingFile
                EncodingFile = new Encoding.EncodingFile(cdnClient, ConfigContainer.EncodingEKey);

                // Stream RootFile
                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.RootCKey, out var entry))
                    RootFile = new Root.RootFile(cdnClient, entry.EKey);

                // Stream InstallFile
                if (ConfigContainer.InstallEKey.Value != null)
                    InstallFile = new Install.InstallFile(cdnClient, ConfigContainer.InstallEKey);
                else if (EncodingFile.TryGetCKeyEntry(ConfigContainer.InstallCKey, out entry))
                    InstallFile = new Install.InstallFile(cdnClient, entry.EKey);

                // Stream DownloadFile
                if (ConfigContainer.DownloadEKey.Value != null)
                    DownloadFile = new Download.DownloadFile(cdnClient, ConfigContainer.DownloadEKey);
                else if (EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadCKey, out entry))
                    DownloadFile = new Download.DownloadFile(cdnClient, entry.EKey);

                // Stream DownloadSizeFile
                if (ConfigContainer.DownloadSizeEKey.Value != null)
                    DownloadSizeFile = new Download.DownloadSizeFile(cdnClient, ConfigContainer.DownloadSizeEKey);
                else if (EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadSizeCKey, out entry))
                    DownloadSizeFile = new Download.DownloadSizeFile(cdnClient, entry.EKey);
            }

            // Stream PatchFile
            if (ConfigContainer.PatchEKey.Value != null)
                PatchFile = new Patch.PatchFile(cdnClient, ConfigContainer.PatchEKey);

            ApplyVersionSpecificSettings(Build);
        }

        /// <summary>
        /// Download and open an remote TACT container
        /// <para>Note: This will download the entire CDN so will take a while</para>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="directory"></param>
        /// <param name="product"></param>
        /// <param name="locale"></param>
        public void DownloadRemote(string directory, string product, Locale locale)
        {
            ConfigContainer = new Configs.ConfigContainer(product, locale);
            ConfigContainer.DownloadRemote(directory);

            var cdnClient = new CDNClient(ConfigContainer);
            var queuedDownload = new QueuedDownloader(directory, cdnClient);

            if (ConfigContainer.EncodingEKey.Value != null)
            {
                // Download encoding file
                var encodingEKey = DownloadSystemFile(ConfigContainer.EncodingEKey, cdnClient, directory);
                if (encodingEKey.Value != null)
                    EncodingFile = new Encoding.EncodingFile(BaseDirectory, encodingEKey, true);

                // Download PatchFile
                DownloadSystemFile(ConfigContainer.PatchEKey, cdnClient, directory, "patch");

                // Download RootFile
                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.RootCKey, out var ekeyEntry))
                    queuedDownload.Enqueue(ekeyEntry.EKey.ToString());

                // Download InstallFile
                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.InstallCKey, out ekeyEntry))
                    queuedDownload.Enqueue(ekeyEntry.EKey.ToString());

                // Download DownloadFile
                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadCKey, out ekeyEntry))
                    queuedDownload.Enqueue(ekeyEntry.EKey.ToString());

                // Download DownloadSizeFile
                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadSizeCKey, out ekeyEntry))
                    queuedDownload.Enqueue(ekeyEntry.EKey.ToString());

                queuedDownload.Download("data");
            }

            // Download Indices and archives
            IndexContainer = new Indices.IndexContainer();
            IndexContainer.DownloadRemote(directory, ConfigContainer);

            Open(directory, product, locale);
        }

        public void Save(string directory)
        {
            // if this field exists and mismatches the generated file; the client will error
            // if this field is missing the client will generate the file and variable itself
            ConfigContainer?.CDNConfig?.GetValues("archive-group")?.Clear();

            IndexContainer?.Save(directory, ConfigContainer);
            RootFile?.Write(directory, this);
            DownloadFile?.Write(directory, this);
            DownloadSizeFile?.Write(directory, this);
            InstallFile?.Write(directory, this);
            EncodingFile?.Write(directory, ConfigContainer);
            ConfigContainer?.Save(directory);

            RootFile?.FileLookup?.Close();
        }

        #endregion

        #region Helpers

        private void ApplyVersionSpecificSettings(uint build)
        {
            if (build > 24473 && DownloadFile != null)
                DownloadFile.DownloadHeader.Version = 2;

            if (build > 27547)
            {
                if (DownloadFile != null)
                    DownloadFile.DownloadHeader.Version = 3;

                DownloadSizeFile = new Download.DownloadSizeFile();
            }

            if (build >= 30080 && RootFile != null)
                RootFile.RootHeader.Version = 2;
        }

        private MD5Hash DownloadSystemFile(MD5Hash key, CDNClient client, string directory, string dataFolder = "data")
        {
            if (key.Value == null)
                return default;

            string systemFileUrl = Helpers.GetCDNUrl(key.ToString(), dataFolder);
            client.DownloadFile(systemFileUrl, Helpers.GetCDNPath(key.ToString(), dataFolder, directory, true)).Wait();
            return key;
        }

        #endregion
    }
}

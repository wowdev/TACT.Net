using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using TACT.Net.Common;

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

            // TODO check this
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

            IndexContainer = new Indices.IndexContainer();
            IndexContainer.Open(directory);

            if (!ConfigContainer.EncodingEKey.IsEmpty)
            {
                EncodingFile = new Encoding.EncodingFile(BaseDirectory, ConfigContainer.EncodingEKey);

                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.RootMD5, out var rootCEntry))
                    RootFile = new Root.RootFile(BaseDirectory, rootCEntry.EKey);
            }
        }

        /// <summary>
        /// Download and open an remote TACT container
        /// </summary>
        /// <param name="url"></param>
        /// <param name="directory"></param>
        /// <param name="product"></param>
        /// <param name="locale"></param>
        public void DownloadRemote(string url, string directory, string product, Locale locale)
        {
            ConfigContainer = new Configs.ConfigContainer(product, locale);
            ConfigContainer.DownloadRemote(url, directory);

            if (!ConfigContainer.EncodingEKey.IsEmpty)
            {
                var webClient = new WebClient();

                // Download encoding file
                string encodingUrl = String.Format("{0}/{1}", url.TrimEnd('/'), Helpers.GetCDNPath(ConfigContainer.EncodingEKey.ToString(), "data", "", false, true));
                webClient.DownloadFile(encodingUrl, Helpers.GetCDNPath(ConfigContainer.EncodingEKey.ToString(), "data", directory, true));

                EncodingFile = new Encoding.EncodingFile(BaseDirectory, ConfigContainer.EncodingEKey);

                if (EncodingFile.TryGetCKeyEntry(ConfigContainer.RootMD5, out var rootCEntry))
                {
                    // Download root file
                    string rootUrl = String.Format("{0}/{1}", url.TrimEnd('/'), Helpers.GetCDNPath(rootCEntry.EKey.ToString(), "data", "", false, true));
                    webClient.DownloadFile(rootUrl, Helpers.GetCDNPath(rootCEntry.EKey.ToString(), "data", directory, true));

                    RootFile = new Root.RootFile(BaseDirectory, rootCEntry.EKey);
                }
            }
        }

        public void Save(string directory)
        {
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
            if (build > 24473)
                DownloadFile.DownloadHeader.Version = 2;

            if (build > 27547)
            {
                DownloadFile.DownloadHeader.Version = 3;
                DownloadSizeFile = new Download.DownloadSizeFile();
            }

            if (build >= 30080)
                RootFile.RootHeader.Version = 2;
        }

        #endregion
    }
}

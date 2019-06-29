using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using TACT.Net.Common;
using TACT.Net.Cryptography;

[assembly: InternalsVisibleTo("TACT.Net.Tests")]
namespace TACT.Net
{
    public sealed class TACTRepo
    {
        public readonly string BaseDirectory;
        private WebClient WebClient = new WebClient();

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
                if (ConfigContainer.RootMD5.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.RootMD5, out var rootEKey))
                    RootFile = new Root.RootFile(BaseDirectory, rootEKey.EKey);

                // Open InstallFile
                if (ConfigContainer.InstallMD5.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.InstallMD5, out var installEKey))
                    InstallFile = new Install.InstallFile(BaseDirectory, installEKey.EKey);

                // Open DownloadFile
                if (ConfigContainer.DownloadMD5.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadMD5, out var downloadEKey))
                    DownloadFile = new Download.DownloadFile(BaseDirectory, downloadEKey.EKey);

                // Open DownloadSizeFile
                if (ConfigContainer.DownloadSizeMD5.Value != null && EncodingFile.TryGetCKeyEntry(ConfigContainer.DownloadSizeMD5, out var downloadSizeEKey))
                    DownloadSizeFile = new Download.DownloadSizeFile(BaseDirectory, downloadSizeEKey.EKey);
            }

            // Open PatchFile
            if (ConfigContainer.PatchMD5.Value != null)
                PatchFile = new Patch.PatchFile(BaseDirectory, ConfigContainer.PatchMD5);

            ApplyVersionSpecificSettings(Build);
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

            if (uint.TryParse(ConfigContainer?.VersionsFile?.GetValue("BuildId", locale), out uint build))
                Build = build;

            IndexContainer = new Indices.IndexContainer();
            IndexContainer.Open(directory);

            if (ConfigContainer.EncodingEKey.Value != null)
            {
                // Download encoding file
                MD5Hash encodingEKey = DownloadSystemFile(ConfigContainer.EncodingEKey, url, directory);
                if (encodingEKey.Value != null)
                    EncodingFile = new Encoding.EncodingFile(BaseDirectory, encodingEKey);

                // Download RootFile
                if (ConfigContainer.RootMD5.Value != null)
                {
                    MD5Hash rootEKey = DownloadSystemFile(ConfigContainer.RootMD5, url, directory, EncodingFile);
                    if (rootEKey.Value != null)
                        RootFile = new Root.RootFile(BaseDirectory, rootEKey);
                }

                // Download InstallFile
                if (ConfigContainer.InstallMD5.Value != null)
                {
                    MD5Hash installEKey = DownloadSystemFile(ConfigContainer.InstallMD5, url, directory, EncodingFile);
                    if (installEKey.Value != null)
                        InstallFile = new Install.InstallFile(BaseDirectory, installEKey);
                }

                // Download DownloadFile
                if (ConfigContainer.DownloadMD5.Value != null)
                {
                    MD5Hash downloadEKey = DownloadSystemFile(ConfigContainer.DownloadMD5, url, directory, EncodingFile);
                    if (downloadEKey.Value != null)
                        DownloadFile = new Download.DownloadFile(BaseDirectory, downloadEKey);
                }

                // Download DownloadSizeFile
                if (ConfigContainer.DownloadSizeMD5.Value != null)
                {
                    MD5Hash downloadSizeEKey = DownloadSystemFile(ConfigContainer.DownloadSizeMD5, url, directory, EncodingFile);
                    if (downloadSizeEKey.Value != null)
                        DownloadSizeFile = new Download.DownloadSizeFile(BaseDirectory, downloadSizeEKey);
                }
            }

            // Download PatchFile
            if (ConfigContainer.PatchMD5.Value != null)
            {
                MD5Hash patchEKey = DownloadSystemFile(ConfigContainer.PatchMD5, url, directory, null, "patch");
                if (patchEKey.Value != null)
                    PatchFile = new Patch.PatchFile(BaseDirectory, patchEKey);
            }

            ApplyVersionSpecificSettings(Build);
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

        private MD5Hash DownloadSystemFile(MD5Hash key, string url, string directory, Encoding.EncodingFile encodingFile = null, string dataFolder = "data")
        {
            if (encodingFile != null)
            {
                if (encodingFile.TryGetCKeyEntry(key, out var encodingKey))
                    key = encodingKey.EKey;
                else
                    return new MD5Hash();
            }

            string systemFileUrl = string.Format("{0}/{1}", url.TrimEnd('/'), Helpers.GetCDNPath(key.ToString(), dataFolder, "", false, true));
            WebClient.DownloadFile(systemFileUrl, Helpers.GetCDNPath(key.ToString(), dataFolder, directory, true));

            return key;
        }

        #endregion
    }
}

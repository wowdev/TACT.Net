using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TACT.Net.Tests")]
namespace TACT.Net
{
    public sealed class TACT
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

        public TACT(string baseDirectory = "")
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

            if (build > 27547)
                DownloadSizeFile = new Download.DownloadSizeFile();

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

                if (EncodingFile.TryGetContentEntry(ConfigContainer.RootMD5, out var rootCEntry))
                    RootFile = new Root.RootFile(BaseDirectory, rootCEntry.EKey);
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
        }

        public void Clean()
        {
            var usedHashes = new HashSet<Cryptography.MD5Hash>();

            if (ConfigContainer != null)
            {
                usedHashes.Add(ConfigContainer.BuildConfigMD5);
                usedHashes.Add(ConfigContainer.CDNConfigMD5);
                usedHashes.Add(ConfigContainer.PatchConfigMD5);
                usedHashes.Add(ConfigContainer.RootMD5);
                usedHashes.Add(ConfigContainer.EncodingMD5);
                usedHashes.Add(ConfigContainer.InstallMD5);
                usedHashes.Add(ConfigContainer.DownloadMD5);
                usedHashes.Add(ConfigContainer.DownloadSizeMD5);
                usedHashes.Add(ConfigContainer.PatchMD5);
            }

            if (RootFile != null)
            {
                var blocks = RootFile.GetBlocks(Root.LocaleFlags.All_WoW, Root.ContentFlags.None);
                foreach (var block in blocks)
                    usedHashes.UnionWith(block.Records.Select(x => x.Value.CKey));
            }

            if (InstallFile != null)
            {
                usedHashes.UnionWith(InstallFile.Files.Select(x => x.CKey));
            }
        }


        #endregion
    }
}

using System;
using System.Collections.Generic;

namespace TACT.Net
{
    public sealed class TACT
    {
        public readonly string BaseDirectory;

        #region System Files

        public Configs.ConfigContainer ConfigContainer { get; set; }
        public Indices.IndexContainer IndexContainer { get; set; }
        public Encoding.EncodingFile EncodingFile { get; set; }
        public Root.RootFile RootFile { get; set; }
        public Download.DownloadFile DownloadFile {get;set;}
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
        /// Creates a new TACT container populated with the necessary base files
        /// </summary>
        /// <param name="product"></param>
        /// <param name="locale"></param>
        public void Create(string product, Locale locale)
        {
            ConfigContainer = new Configs.ConfigContainer(product, locale);
            ConfigContainer.Create();

            IndexContainer = new Indices.IndexContainer();
            RootFile = new Root.RootFile(this);
            EncodingFile = new Encoding.EncodingFile();
            InstallFile = new Install.InstallFile();
            DownloadFile = new Download.DownloadFile();
        }

        ///// <summary>
        ///// Opens an existing TACT container and loads the Root and Encoding files
        ///// </summary>
        ///// <param name="directory"></param>
        ///// <param name="product"></param>
        ///// <param name="locale"></param>
        //public void Open(string directory, string product, Locale locale)
        //{
        //    var configContainer = new Configs.ConfigContainer(product, this);
        //    configContainer.Open(directory, locale);

        //    var archiveContainer = new Archives.ArchiveContainer(this);
        //    archiveContainer.Open(directory);

        //    new Root.RootFile(archiveContainer.OpenFile(configContainer.RootMD5), this);
        //    new Encoding.EncodingFile(archiveContainer.OpenFile(configContainer.EncodingEKey), this);
        //}


        #endregion
    }
}

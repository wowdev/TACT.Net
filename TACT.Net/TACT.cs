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

        ///// <summary>
        ///// Creates a new TACT container populated with the necessary base files
        ///// </summary>
        ///// <param name="product"></param>
        ///// <param name="locale"></param>
        //public void Create(string product, Locale locale)
        //{
        //    new Configs.ConfigContainer(product, this);
        //    new Archives.ArchiveContainer(this);
        //    new Root.RootFile(this);
        //    new Encoding.EncodingFile(this);
        //    new Install.InstallFile(this);
        //    new Download.DownloadFile(this);
        //}

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

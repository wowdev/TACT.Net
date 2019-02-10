using System.IO;
using System.Linq;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Download
{
    /// <summary>
    /// Lists all files their download priority
    /// <para>Tags determine the conditions for a file to be downloaded</para>
    /// </summary>
    public class DownloadFile : DownloadFileBase<DownloadFileEntry>
    {
        public DownloadHeader DownloadHeader { get; private set; }

        private readonly EMap[] _EncodingMap;

        #region Constructors

        /// <summary>
        /// Creates a new DownloadFile
        /// </summary>
        public DownloadFile()
        {
            DownloadHeader = new DownloadHeader();

            _EncodingMap = new[]
            {
                new EMap(EType.None, 6),
                new EMap(EType.None, 6),
                new EMap(EType.ZLib, 9),
            };
        }

        /// <summary>
        /// Loads an existing DownloadFile
        /// </summary>
        /// <param name="path">BLTE encoded file path</param>
        public DownloadFile(string path) : this()
        {
            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing DownloadFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="hash">DownloadFile MD5</param>
        public DownloadFile(string directory, MD5Hash hash) : this(Helpers.GetCDNPath(hash.ToString(), "data", directory)) { }

        /// <summary>
        /// Loads an existing DownloadFile
        /// </summary>
        /// <param name="stream"></param>
        public DownloadFile(BlockTableStreamReader stream) : this()
        {
            Read(stream);
        }

        #endregion

        #region IO

        protected override void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                // parse the header
                DownloadHeader.Read(br);

                _FileEntries.EnsureCapacity((int)DownloadHeader.EntryCount);
                for (int i = 0; i < DownloadHeader.EntryCount; i++)
                {
                    var fileEntry = new DownloadFileEntry();
                    fileEntry.Read(br, DownloadHeader);
                    _FileEntries[fileEntry.EKey] = fileEntry;
                }
                _FileEntries.TrimExcess();

                // Tags
                ReadTags(br, DownloadHeader.TagCount, DownloadHeader.EntryCount);

                // HACK do we know what this is?
                DownloadHeader.IncludeChecksum = false;

                // store checksum
                Checksum = stream.MD5Hash();
            }
        }

        /// <summary>
        /// Saves the DownloadFile to disk and optionally updates the BuildConfig
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <param name="configContainer"></param>
        /// <returns></returns>
        public override CASRecord Write(string directory, Configs.ConfigContainer configContainer = null)
        {
            CASRecord record;
            using (var bt = new BlockTableStreamWriter(_EncodingMap[0]))
            using (var bw = new BinaryWriter(bt))
            {
                // Header
                DownloadHeader.EntryCount = (uint)_FileEntries.Count;
                DownloadHeader.TagCount = (ushort)_TagEntries.Count;
                DownloadHeader.Write(bw);

                // File Entries
                bt.AddBlock(_EncodingMap[1]);
                foreach (var fileEntry in _FileEntries.Values.OrderBy(x => x.Priority))
                    fileEntry.Write(bw, DownloadHeader);

                // Tag Entries
                bt.AddBlock(_EncodingMap[2]);
                WriteTags(bw);

                // finalise
                record = bt.Finalise();

                // save
                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                    bt.WriteTo(fs);
            }

            // update the build config with the new values
            if (configContainer?.BuildConfig != null)
            {
                configContainer.BuildConfig.SetValue("download-size", record.EBlock.DecompressedSize, 0);
                configContainer.BuildConfig.SetValue("download-size", record.EBlock.CompressedSize, 1);
                configContainer.BuildConfig.SetValue("download", record.CKey, 0);
                configContainer.BuildConfig.SetValue("download", record.EKey, 1);
            }

            Checksum = record.CKey;
            return record;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a CASRecord, this will overwrite existing entries
        /// </summary>
        /// <param name="record"></param>
        /// <param name="priority">0 = highest, 2 = lowest, -1 for system files</param>
        /// <param name="tags"></param>
        public void AddOrUpdate(CASRecord record, sbyte priority, params string[] tags)
        {
            var entry = new DownloadFileEntry()
            {
                EKey = record.EKey,
                CompressedSize = record.EBlock.CompressedSize,
                Flags = new byte[DownloadHeader.FlagSize],
                Priority = priority,
                Checksum = 0 // TODO do we know what this is?
            };

            AddOrUpdate(entry, tags);
        }

        #endregion
    }
}

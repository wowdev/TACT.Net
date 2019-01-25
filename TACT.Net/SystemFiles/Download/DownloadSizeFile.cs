using System.IO;
using System.Linq;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;

namespace TACT.Net.Download
{
    /// <summary>
    /// Lists keys and sizes for all files
    /// <para>Tags determine the conditions for a file to be downloaded</para>
    /// </summary>
    public class DownloadSizeFile : DownloadFileBase<DownloadSizeFileEntry>
    {
        public DownloadSizeHeader DownloadSizeHeader { get; private set; }
        public MD5Hash Checksum { get; private set; }

        private readonly EMap[] _EncodingMap;

        #region Constructors

        /// <summary>
        /// Creates a new DownloadSizeFile
        /// </summary>
        public DownloadSizeFile(TACT container = null) : base(container)
        {
            DownloadSizeHeader = new DownloadSizeHeader();
            ((HashComparer)_FileEntries.Comparer).KeySize = DownloadSizeHeader.EKeySize;

            _EncodingMap = new[]
            {
                new EMap(EType.None, 6),
                new EMap(EType.ZLib, 9),
                new EMap(EType.None, 6)
            };
        }

        /// <summary>
        /// Loads an existing DownloadSizeFile
        /// </summary>
        /// <param name="path">BLTE encoded file path</param>
        public DownloadSizeFile(string path, TACT container = null) : this(container)
        {
            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing DownloadSizeFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="hash">DownloadSizeFile MD5</param>
        public DownloadSizeFile(string directory, MD5Hash hash, TACT container = null) :
            this(Helpers.GetCDNPath(hash.ToString(), "data", directory), container)
        { }

        /// <summary>
        /// Loads an existing DownloadSizeFile
        /// </summary>
        /// <param name="stream"></param>
        public DownloadSizeFile(BlockTableStreamReader stream, TACT container = null) : this(container)
        {
            Read(stream);
        }
        #endregion

        #region IO

        protected override void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                DownloadSizeHeader.Read(br);
                ((HashComparer)_FileEntries.Comparer).KeySize = DownloadSizeHeader.EKeySize;

                // Tags
                ReadTags(br, DownloadSizeHeader.TagCount, DownloadSizeHeader.EntryCount);

                // File Entries
                for (int i = 0; i < DownloadSizeHeader.EntryCount; i++)
                {
                    var fileEntry = new DownloadSizeFileEntry();
                    fileEntry.Read(br, DownloadSizeHeader);
                    _FileEntries[fileEntry.EKey] = fileEntry;
                }

                Checksum = stream.MD5Hash();
            }
        }

        /// <summary>
        /// Saves the DownloadSizeFile to disk and updates the BuildConfig
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <returns></returns>
        public override CASRecord Write(string directory)
        {
            CASRecord record;
            using (var bt = new BlockTableStreamWriter(_EncodingMap[0]))
            using (var bw = new BinaryWriter(bt))
            using (var ms = new MemoryStream())
            {
                // Header
                DownloadSizeHeader.EntryCount = (uint)_FileEntries.Count;
                DownloadSizeHeader.TagCount = (ushort)_TagEntries.Count;
                DownloadSizeHeader.TotalSize = (ulong)_FileEntries.Values.Sum(x => x.CompressedSize);
                DownloadSizeHeader.Write(bw);

                // Tag Entries
                bt.AddBlock(_EncodingMap[1]);
                WriteTags(bw);

                // File Entries
                WriteFileEntries(bt);

                // finalise
                record = bt.Finalise();

                // save
                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                    bt.WriteTo(fs);
            }

            // update the build config with the new values
            if (Container != null && Container.TryResolve<Configs.ConfigContainer>(out var configContainer))
            {
                configContainer.BuildConfig?.SetValue("size-size", record.EBlock.DecompressedSize, 0);
                configContainer.BuildConfig?.SetValue("size-size", record.EBlock.CompressedSize, 1);
                configContainer.BuildConfig?.SetValue("size", record.CKey, 0);
                configContainer.BuildConfig?.SetValue("size", record.EKey, 1);
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
        /// <param name="tags"></param>
        public void AddOrUpdate(CASRecord record, params string[] tags)
        {
            var entry = new DownloadSizeFileEntry()
            {
                EKey = record.EKey,
                CompressedSize = record.EBlock.CompressedSize,
            };

            AddOrUpdate(entry, tags);
        }

        #endregion

        #region Helpers

        private void WriteFileEntries(BlockTableStreamWriter bt)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // ordered by descending size
                foreach (var fileEntry in _FileEntries.Values.OrderByDescending(x => x.CompressedSize))
                    fileEntry.Write(bw, DownloadSizeHeader);

                // batched into 0xFFFF size uncompressed blocks
                // this is for client performance and isn't mandatory
                ms.Position = 0;
                byte[] buffer = new byte[0xFFFF];
                int read;
                while ((read = ms.Read(buffer)) != 0)
                {
                    bt.AddBlock(_EncodingMap[2]);
                    bt.Write(buffer, 0, read);
                }
            }
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;
using TACT.Net.Shared.Tags;

namespace TACT.Net.Install
{
    /// <summary>
    /// Lists all files that are stored outside of the archives
    /// <para>Tags determine the conditions for a file to be extracted</para>
    /// </summary>
    public class InstallFile : TagFileBase
    {
        public InstallHeader InstallHeader { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public IEnumerable<InstallFileEntry> Files => _FileEntries.Values;

        private readonly Dictionary<string, InstallFileEntry> _FileEntries;
        private readonly EMap[] _EncodingMap;

        #region Constructors

        public InstallFile(TACT container = null) : base(container)
        {
            InstallHeader = new InstallHeader();
            _FileEntries = new Dictionary<string, InstallFileEntry>(StringComparer.OrdinalIgnoreCase);

            _EncodingMap = new[]
            {
                new EMap(EType.ZLib, 9),
                new EMap(EType.None, 6),
            };
        }

        public InstallFile(string path, TACT container = null) : this(container)
        {
            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        public InstallFile(BlockTableStreamReader stream, TACT container = null) : this(container)
        {
            Read(stream);
        }

        #endregion

        #region IO

        private void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                InstallHeader.Read(br);

                // Tags
                ReadTags(br, InstallHeader.TagCount, InstallHeader.EntryCount);

                // Files
                for (int i = 0; i < InstallHeader.EntryCount; i++)
                {
                    var fileEntry = new InstallFileEntry();
                    fileEntry.Read(br, InstallHeader);
                    _FileEntries.Add(fileEntry.FilePath, fileEntry);
                }

                Checksum = stream.MD5Hash();
            }
        }

        public CASRecord Write(string directory)
        {
            CASRecord record;

            using (var bt = new BlockTableStreamWriter(_EncodingMap[0]))
            using (var bw = new BinaryWriter(bt))
            {
                // Header and Tag block
                InstallHeader.EntryCount = (uint)_FileEntries.Count;
                InstallHeader.TagCount = (ushort)_TagEntries.Count;
                InstallHeader.Write(bw);
                WriteTags(bw);

                // File Entry block
                bt.AddBlock(_EncodingMap[1]);
                foreach (var fileEntry in _FileEntries.Values)
                    fileEntry.Write(bw);

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
                configContainer.BuildConfig?.SetValue("install-size", record.EBlock.DecompressedSize, 0);
                configContainer.BuildConfig?.SetValue("install-size", record.EBlock.CompressedSize, 1);
                configContainer.BuildConfig?.SetValue("install", record.CKey, 0);
                configContainer.BuildConfig?.SetValue("install", record.EKey, 1);
            }

            Checksum = record.CKey;
            return record;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a CASRecord to the InstallFile, this will overwrite existing entries
        /// </summary>
        /// <param name="record"></param>
        /// <param name="tags"></param>
        public void AddOrUpdate(CASRecord record, params string[] tags)
        {
            var entry = new InstallFileEntry()
            {
                FilePath = record.FileName,
                CKey = record.CKey,
                DecompressedSize = record.EBlock.DecompressedSize,
            };

            AddOrUpdate(entry, tags);
        }

        public void AddOrUpdate(InstallFileEntry fileEntry, params string[] tags)
        {
            _FileEntries[fileEntry.FilePath] = fileEntry;

            // update the tag masks
            int index = _FileEntries.IndexOfKey(x => x.IndexOf(fileEntry.FilePath, StringComparison.OrdinalIgnoreCase) >= 0);
            SetTags(index, true, tags);
        }

        public void AddOrUpdate(TagEntry tagEntry)
        {
            AddOrUpdateTag(tagEntry, _FileEntries.Count);
        }

        /// <summary>
        /// Removes a fileEntry from the InstallFile
        /// </summary>
        /// <param name="fileEntry"></param>
        public void Remove(InstallFileEntry fileEntry)
        {
            int index = _FileEntries.IndexOfKey(x => x.IndexOf(fileEntry.FilePath, StringComparison.OrdinalIgnoreCase) >= 0);
            if (index > -1)
            {
                _FileEntries.Remove(fileEntry.FilePath);
                RemoveFile(index);
            }
        }

        /// <summary>
        /// Returns a InstallFileEntry by name
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        public bool TryGet(string filename, out InstallFileEntry fileEntry)
        {
            return _FileEntries.TryGetValue(filename, out fileEntry);
        }

        /// <summary>
        /// Determines whether the specific filename exists
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool ContainsFilename(string filename) => _FileEntries.ContainsKey(filename);

        /// <summary>
        /// Returns the Tags associated to a file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public IEnumerable<string> GetTags(string filename)
        {
            int index = _FileEntries.IndexOfKey(x => x.IndexOf(filename, StringComparison.OrdinalIgnoreCase) >= 0);
            return GetTags(index);
        }

        /// <summary>
        /// Sets the value for the specified file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="value"></param>
        /// <param name="tags"></param>
        public void SetTags(string filename, bool value, params string[] tags)
        {
            int index = _FileEntries.IndexOfKey(x => x.IndexOf(filename, StringComparison.OrdinalIgnoreCase) >= 0);
            SetTags(index, value, tags);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Tags;

namespace TACT.Net.Install
{
    /// <summary>
    /// Lists all files that are stored outside of the archives
    /// <para>Tags determine the conditions for a file to be extracted</para>
    /// </summary>
    public class InstallFile : TagFileBase
    {
        public InstallHeader InstallHeader { get; private set; }
        public IEnumerable<InstallFileEntry> Files => _FileEntries.Values;

        private readonly Dictionary<string, InstallFileEntry> _FileEntries;
        private readonly EMap[] _EncodingMap;

        #region Constructors

        /// <summary>
        /// Creates a new InstallFile
        /// </summary>
        public InstallFile()
        {
            InstallHeader = new InstallHeader();
            _FileEntries = new Dictionary<string, InstallFileEntry>(StringComparer.OrdinalIgnoreCase);

            _EncodingMap = new[]
            {
                new EMap(EType.ZLib, 9),
                new EMap(EType.None, 6),
            };
        }

        /// <summary>
        /// Loads an existing InstallFile
        /// </summary>
        /// <param name="path">BLTE encoded file path</param>
        public InstallFile(string path) : this()
        {
            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing InstallFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="hash">InstallFile MD5</param>
        public InstallFile(string directory, MD5Hash hash) : this(Helpers.GetCDNPath(hash.ToString(), "data", directory)) { }

        /// <summary>
        /// Loads an existing InstallFile
        /// </summary>
        /// <param name="stream"></param>
        public InstallFile(BlockTableStreamReader stream) : this()
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

        /// <summary>
        /// Saves the InstallFile to disk and optionally updates the BuildConfig
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <param name="configContainer"></param>
        /// <returns></returns>
        public CASRecord Write(string directory, TACT tactInstance = null)
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
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                }
            }

            // insert the record into the encoding and the download files
            if (tactInstance != null)
            {
                tactInstance.EncodingFile?.AddOrUpdate(record);
                tactInstance.DownloadFile?.AddOrUpdate(record, -1);
                tactInstance.DownloadSizeFile?.AddOrUpdate(record);

                // update the build config with the new values
                if (tactInstance.ConfigContainer?.BuildConfig != null)
                {
                    tactInstance.ConfigContainer.BuildConfig.SetValue("install-size", record.EBlock.DecompressedSize, 0);
                    tactInstance.ConfigContainer.BuildConfig.SetValue("install-size", record.EBlock.CompressedSize, 1);
                    tactInstance.ConfigContainer.BuildConfig.SetValue("install", record.CKey, 0);
                    tactInstance.ConfigContainer.BuildConfig.SetValue("install", record.EKey, 1);
                }
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
        /// <param name="tactInstance">If provided, will add the entry to all relevant system files</param>
        /// <param name="tags"></param>
        public void AddOrUpdate(CASRecord record, TACT tactInstance = null, params string[] tags)
        {
            var entry = new InstallFileEntry()
            {
                FilePath = record.FileName,
                CKey = record.CKey,
                DecompressedSize = record.EBlock.DecompressedSize,
            };

            AddOrUpdate(entry, tags);

            // add the record to all referenced files
            if (tactInstance != null)
            {
                tactInstance.EncodingFile?.AddOrUpdate(record);
                tactInstance.IndexContainer?.Enqueue(record);
                tactInstance.DownloadFile?.AddOrUpdate(record, 2);
                tactInstance.DownloadSizeFile?.AddOrUpdate(record);
            }
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

        /// <summary>
        /// Resets the Tags to the BfA default values and clears all file associations
        /// </summary>
        public void SetDefaultTags()
        {
            SetDefaultTags(_FileEntries.Count);
        }

        #endregion
    }
}

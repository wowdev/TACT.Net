using System;
using System.Collections.Generic;
using System.IO;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Network;
using TACT.Net.Tags;

namespace TACT.Net.Install
{
    /// <summary>
    /// Lists all files that are stored outside of the archives
    /// <para>Tags determine the conditions for a file to be extracted</para>
    /// </summary>
    public class InstallFile : TagFileBase, ISystemFile
    {
        public string FilePath { get; private set; }
        public InstallHeader InstallHeader { get; private set; }
        public IEnumerable<InstallFileEntry> Files => _FileEntries.Values;
        public MD5Hash Checksum { get; private set; }

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
            if (!File.Exists(path))
                throw new FileNotFoundException("Unable to open InstallFile", path);

            FilePath = path;

            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing InstallFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="ekey">InstallFile MD5</param>
        public InstallFile(string directory, MD5Hash ekey) :
            this(Helpers.GetCDNPath(ekey.ToString(), "data", directory))
        { }

        /// <summary>
        /// Loads an existing InstallFile from a remote CDN
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ekey"></param>
        public InstallFile(CDNClient client, MD5Hash ekey) : this()
        {
            string url = Helpers.GetCDNUrl(ekey.ToString(), "data");

            using (var stream = client.OpenStream(url).Result)
            using (var bt = new BlockTableStreamReader(stream))
                Read(bt);
        }

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
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead || stream.Length <= 1)
                throw new NotSupportedException($"Unable to read InstallFile stream");

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
                    _FileEntries.TryAdd(fileEntry.FilePath, fileEntry);
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
        public CASRecord Write(string directory, TACTRepo tactRepo = null)
        {
            CASRecord record;

            using (var bt = new BlockTableStreamWriter(_EncodingMap[0]))
            using (var bw = new BinaryWriter(bt))
            {
                // Header and Tag block
                InstallHeader.EntryCount = (uint)_FileEntries.Count;
                InstallHeader.TagCount = (ushort)_TagEntries.Count;
                InstallHeader.Write(bw);
                WriteTags(bw, _FileEntries.Count);

                // File Entry block
                bt.AddBlock(_EncodingMap[1]);
                foreach (var fileEntry in _FileEntries.Values)
                    fileEntry.Write(bw);

                // finalise
                record = bt.Finalise();
                record.DownloadPriority = -1;

                // save
                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                }
            }

            // insert the record into the encoding and the download files
            if (tactRepo != null)
            {
                tactRepo.EncodingFile?.AddOrUpdate(record, tactRepo);

                // update the build config with the new values
                if (tactRepo.ConfigContainer?.BuildConfig != null)
                {
                    tactRepo.ConfigContainer.BuildConfig.SetValue("install-size", record.EBlock.DecompressedSize, 0);
                    tactRepo.ConfigContainer.BuildConfig.SetValue("install-size", record.EBlock.CompressedSize, 1);
                    tactRepo.ConfigContainer.BuildConfig.SetValue("install", record.CKey, 0);
                    tactRepo.ConfigContainer.BuildConfig.SetValue("install", record.EKey, 1);
                }
            }

            Checksum = record.CKey;
            FilePath = record.BLTEPath;
            return record;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a CASRecord to the InstallFile, this will overwrite existing entries
        /// </summary>
        /// <param name="record"></param>
        /// <param name="tactRepo">If provided, will add the entry to all relevant system files</param>
        /// <param name="tags"></param>
        public void AddOrUpdate(CASRecord record, TACTRepo tactRepo = null)
        {
            var entry = new InstallFileEntry()
            {
                FilePath = record.FileName,
                CKey = record.CKey,
                DecompressedSize = record.EBlock.DecompressedSize,
            };

            AddOrUpdate(entry, record.Tags);

            // add the record to the Encoding File
            tactRepo?.EncodingFile?.AddOrUpdate(record, tactRepo);
        }

        public void AddOrUpdate(InstallFileEntry fileEntry, params string[] tags)
        {
            int index;
            if (!_FileEntries.ContainsKey(fileEntry.FilePath))
            {
                index = _FileEntries.Count;
                _FileEntries.Add(fileEntry.FilePath, fileEntry);
            }
            else
            {
                index = _FileEntries.IndexOfKey(x => x.IndexOf(fileEntry.FilePath, StringComparison.OrdinalIgnoreCase) >= 0);
                _FileEntries[fileEntry.FilePath] = fileEntry;
            }

            // update the tag masks
            SetTags(index, true, tags);
        }

        public void AddOrUpdate(TagEntry tagEntry)
        {
            AddOrUpdateTag(tagEntry, _FileEntries.Count);
        }


        /// <summary>
        /// Removes a InstallFileEntry from the InstallFile
        /// </summary>
        /// <param name="fileEntry"></param>
        public bool Remove(InstallFileEntry fileEntry)
        {
            return Remove(fileEntry.FilePath);
        }

        /// <summary>
        /// Removes a InstallFileEntry from the InstallFile
        /// </summary>
        /// <param name="filePath"></param>
        public bool Remove(string filePath)
        {
            int index = _FileEntries.IndexOfKey(x => x.IndexOf(filePath, StringComparison.OrdinalIgnoreCase) >= 0);
            if (index > -1)
            {
                _FileEntries.Remove(filePath);
                return RemoveFile(index);
            }

            return true;
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
        /// Resets the Tags to the build specific default values and clears all file associations
        /// </summary>
        public void SetDefaultTags(uint build = 99999)
        {
            SetDefaultTags(build, _FileEntries.Count);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Encoding;
using TACT.Net.Network;
using TACT.Net.SystemFiles.Install;
using TACT.Net.Tags;

namespace TACT.Net.Install
{
    /// <summary>
    /// Lists all files that are stored outside of the archives
    /// <para>Tags determine the conditions for a file to be extracted</para>
    /// </summary>
    public class InstallFile : TagFileBase, ISystemFile
    {
        private const StringComparison StrCmp = StringComparison.OrdinalIgnoreCase;

        public string FilePath { get; private set; }
        public InstallHeader InstallHeader { get; private set; }
        public IEnumerable<InstallFileEntry> Files => _FileEntries;
        public MD5Hash Checksum { get; private set; }

        private readonly List<InstallFileEntry> _FileEntries;
        private readonly EMap[] _EncodingMap;

        #region Constructors

        /// <summary>
        /// Creates a new InstallFile
        /// </summary>
        public InstallFile()
        {
            InstallHeader = new InstallHeader();
            _FileEntries = new List<InstallFileEntry>();

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

            using var fs = File.OpenRead(path);
            using var bt = new BlockTableStreamReader(fs);
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

            using var stream = client.OpenStream(url).Result;
            using var bt = new BlockTableStreamReader(stream);
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

            using var br = new BinaryReader(stream);
            InstallHeader.Read(br);

            // Tags
            ReadTags(br, InstallHeader.TagCount, InstallHeader.EntryCount);

            // Files
            _FileEntries.Capacity = (int)InstallHeader.EntryCount;
            for (int i = 0; i < InstallHeader.EntryCount; i++)
            {
                var fileEntry = new InstallFileEntry();
                fileEntry.Read(br, InstallHeader);
                _FileEntries.Add(fileEntry);
            }

            Checksum = stream.MD5Hash();
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
                foreach (var fileEntry in _FileEntries)
                    fileEntry.Write(bw);

                // finalise
                record = bt.Finalise();
                record.DownloadPriority = -1;

                // save
                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using var fs = File.Create(saveLocation);
                bt.WriteTo(fs);
                record.BLTEPath = saveLocation;
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

        /// <summary>
        /// Adds a InstallFileEntry to the InstallFile, this will overwrite existing entries
        /// <para>
        /// WARNING: This will replace all duplicated InstallFileEntries and override their tags
        /// </para>
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <param name="tags"></param>
        public void AddOrUpdate(InstallFileEntry fileEntry, params string[] tags)
        {
            bool found = false;
            for(var i = 0; i < _FileEntries.Count; i++)
            {
                if(_FileEntries[i].Equals(fileEntry))
                {
                    _FileEntries[i] = fileEntry;
                    SetTags(i, true, tags);
                    found = true;
                }
            }

            if(!found)
            {
                var index = _FileEntries.Count;
                _FileEntries.Add(fileEntry);
                SetTags(index, true, tags);
            }            
        }

        public void AddOrUpdate(TagEntry tagEntry)
        {
            AddOrUpdateTag(tagEntry, _FileEntries.Count);
        }


        /// <summary>
        /// Removes an InstallFileEntry from the InstallFile
        /// </summary>
        /// <param name="fileEntry"></param>
        public bool Remove(InstallFileEntry fileEntry)
        {
            var index = _FileEntries.IndexOf(fileEntry);
            if (index > -1)
            {
                _FileEntries.RemoveAt(index);
                RemoveFile(index);
            }

            return false;
        }

        /// <summary>
        /// Removes all InstallFileEntries with a specific filename
        /// </summary>
        /// <param name="filePath"></param>
        public bool Remove(string filePath)
        {
            bool found = false;

            for (var i = 0; i < _FileEntries.Count; i++)
            {
                if (_FileEntries[i].FilePath.Equals(filePath, StrCmp))
                {
                    _FileEntries.RemoveAt(i);
                    RemoveFile(i);
                    found = true;
                    i--;
                }
            }

            return found;
        }


        /// <summary>
        /// Returns all InstallFileEntries with the supplied filepath
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        public IEnumerable<InstallFileEntry> Get(string filePath)
        {
            for (var i = 0; i < _FileEntries.Count; i++)
            {
                if (_FileEntries[i].FilePath.Equals(filePath, StrCmp))
                {
                    yield return _FileEntries[i];
                }
            }
        }
        /// <summary>
        /// Returns all InstallFileEntries with the supplied CKey
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        public IEnumerable<InstallFileEntry> Get(MD5Hash ckey)
        {
            for (var i = 0; i < _FileEntries.Count; i++)
            {
                if (_FileEntries[i].CKey == ckey)
                {
                    yield return _FileEntries[i];
                }
            }
        }

        /// <summary>
        /// Returns a specific InstallFileEntry based on it's filepath 
        /// filtered by platform and architecture
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public InstallFileEntry Get(string filePath, Platforms platform, Architectures arch)
        {
            if (!_TagEntries.TryGetValue(platform.ToString(), out var osTag))
                return null;
            if (!_TagEntries.TryGetValue(arch.ToString(), out var archTag))
                return null;

            for (var i = 0; i < _FileEntries.Count; i++)
            {
                if (_FileEntries[i].FilePath.Equals(filePath, StrCmp) &&
                    osTag.FileMask[i] &&
                    archTag.FileMask[i])
                {
                    return _FileEntries[i];
                }
            }

            return null;
        }
        /// <summary>
        /// Returns a specific InstallFileEntry based on it's CKey 
        /// filtered by platform and architecture
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public InstallFileEntry Get(MD5Hash ckey, Platforms platform, Architectures arch)
        {
            if (!_TagEntries.TryGetValue(platform.ToString(), out var osTag))
                return null;
            if (!_TagEntries.TryGetValue(arch.ToString(), out var archTag))
                return null;

            for (var i = 0; i < _FileEntries.Count; i++)
            {
                if (_FileEntries[i].CKey == ckey && osTag.FileMask[i] && archTag.FileMask[i])
                {
                    return _FileEntries[i];
                }
            }

            return null;
        }


        /// <summary>
        /// Determines whether the specific filename exists
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool ContainsFilename(string filePath)
        {
            return _FileEntries.FindIndex(x => x.FilePath.Equals(filePath, StrCmp)) >= 0;
        }
        /// <summary>
        /// Determines whether the specific CKey exists
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public bool ContainsCKey(MD5Hash ckey)
        {
            return _FileEntries.FindIndex(x => x.CKey == ckey) >= 0;
        }

        /// <summary>
        /// Opens a stream to the data of the supplied InstallFileEntry. Returns null if not found
        /// </summary>
        /// <param name="rootRecord"></param>
        /// <returns></returns>
        public Stream OpenFile(InstallFileEntry fileEntry, TACTRepo tactRepo)
        {
            if (fileEntry == null || tactRepo == null)
                return null;

            if (tactRepo.EncodingFile != null && tactRepo.IndexContainer != null)
            {
                if (tactRepo.EncodingFile.TryGetCKeyEntry(fileEntry.CKey, out EncodingContentEntry encodingCKey))
                    return tactRepo.IndexContainer.OpenFile(encodingCKey.EKeys[0]);
            }

            return null;
        }


        /// <summary>
        /// Returns the Tags associated to a file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public IEnumerable<string> GetTags(InstallFileEntry fileEntry)
        {
            return GetTags(_FileEntries.IndexOf(fileEntry));
        }

        /// <summary>
        /// Sets the value for the specified file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="value"></param>
        /// <param name="tags"></param>
        public void SetTags(InstallFileEntry fileEntry, bool value, params string[] tags)
        {
            SetTags(_FileEntries.IndexOf(fileEntry), value, tags);
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

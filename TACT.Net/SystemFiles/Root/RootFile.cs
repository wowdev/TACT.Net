using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Encoding;
using TACT.Net.FileLookup;
using TACT.Net.Network;
using TACT.Net.Root.Blocks;

namespace TACT.Net.Root
{
    /// <summary>
    /// A catalogue of all files stored in the data archives
    /// <para>Blocks contain variants of each file seperated by their Locale and Content flags</para>
    /// </summary>
    public class RootFile : ISystemFile
    {
        /// <summary>
        /// File lookup for mapping FileDataIds to Filenames
        /// <para>Note: Open will be called if IsLoaded is false when assigned</para>
        /// </summary>
        public IFileLookup FileLookup
        {
            get => _fileLookup;
            set
            {
                _fileLookup = value;
                if (!_fileLookup.IsLoaded)
                    _fileLookup.Open();
            }
        }
        public string FilePath { get; private set; }
        /// <summary>
        /// LocaleFlags to target a specific locale
        /// </summary>
        public LocaleFlags LocaleFlags { get; set; } = LocaleFlags.enUS;
        /// <summary>
        /// ContentFlags to target a specific file set
        /// </summary>
        public ContentFlags ContentFlags { get; set; } = ContentFlags.None;
        public MD5Hash Checksum { get; private set; }
        public RootHeader RootHeader;

        private readonly EMap[] _EncodingMap = new[] { new EMap(EType.ZLib, 9) };
        private readonly List<IRootBlock> _blocks;
        private readonly Lookup3 _lookup3;
        private readonly Dictionary<ulong, uint> _idLookup;
        private IFileLookup _fileLookup;

        #region Constructors
        /// <summary>
        /// Creates a new RootFile
        /// </summary>
        public RootFile()
        {
            RootHeader = new RootHeader();

            _idLookup = new Dictionary<ulong, uint>();
            _lookup3 = new Lookup3();

            // add the default global block
            _blocks = new List<IRootBlock>();
        }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="path">/// <param name="path">BLTE encoded file path</param></param>
        public RootFile(string path) : this()
        {
            _blocks.Clear();

            if (!File.Exists(path))
                throw new FileNotFoundException("Unable to open RootFile", path);

            FilePath = path;

            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="ekey">RootFile MD5</param>
        public RootFile(string directory, MD5Hash ekey) :
            this(Helpers.GetCDNPath(ekey.ToString(), "data", directory))
        { }

        /// <summary>
        /// Loads an existing RootFile from a remote CDN
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ekey"></param>
        public RootFile(CDNClient client, MD5Hash ekey) : this()
        {
            _blocks.Clear();

            string url = Helpers.GetCDNUrl(ekey.ToString(), "data");

            using (var stream = client.OpenStream(url).Result)
            using (var bt = new BlockTableStreamReader(stream))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="stream"></param>
        public RootFile(BlockTableStreamReader stream) : this()
        {
            _blocks.Clear();

            Read(stream);
        }

        #endregion

        #region IO
        private void Read(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead || stream.Length <= 1)
                throw new NotSupportedException($"Unable to read RootFile stream");

            _blocks.Capacity = 0x400; // arbitrary number based on 8.1.5
            _idLookup.EnsureCapacity((int)RootHeader.NamedRecordCount);

            using (var br = new BinaryReader(stream))
            {
                long length = stream.Length;

                RootHeader.Read(br);

                while (stream.Position < length)
                {
                    var block = CreateRootBlock();
                    block.Read(br);
                    _blocks.Add(block);
                }

                _blocks.TrimExcess();

                // build the namehash to id lookup
                foreach (var block in _blocks)
                {
                    if (block.ContentFlags.HasFlag(ContentFlags.NoNameHash))
                        continue;

                    _idLookup.EnsureCapacity(_idLookup.Count + block.Records.Count);
                    foreach (var entry in block.Records)
                        _idLookup[entry.Value.NameHash] = entry.Key;
                }

                Checksum = stream.MD5Hash();
            }

            // validate there is a common block
            var commonBlockTest = GetBlocks(LocaleFlags.All_WoW).Any(x => x.ContentFlags == ContentFlags.None);
            if (!commonBlockTest)
                throw new InvalidDataException("Root is malformed. Missing common block");
        }

        /// <summary>
        /// Writes the RootFile to disk and optionally updates the Encoding and CDN config files
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public CASRecord Write(string directory, TACTRepo tactRepo = null)
        {
            FixDeltas();

            CASRecord record;
            using (var bt = new BlockTableStreamWriter(_EncodingMap[0]))
            using (var bw = new BinaryWriter(bt))
            {
                RootHeader.Write(bw, _blocks);

                foreach (var block in _blocks)
                    block.Write(bw);

                // finalise and change ESpec to non chunked
                record = bt.Finalise();
                record.ESpec = "z";

                // save
                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                }

                // add to the encoding file and update the build config
                if (tactRepo != null)
                {
                    tactRepo.EncodingFile?.AddOrUpdate(record);
                    tactRepo.DownloadFile?.AddOrUpdate(record, 0);
                    tactRepo.DownloadSizeFile?.AddOrUpdate(record);
                    tactRepo.ConfigContainer?.BuildConfig?.SetValue("root", record.CKey, 0);
                }

                FilePath = record.BLTEPath;
                Checksum = record.CKey;
                return record;
            }
        }

        #endregion

        #region Methods
        /// <summary>
        /// Adds or Updates a RootRecord collection and amends all associated system files. If no block is found the Common block is used.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="tactRepo">If provided, will add the entry to all relevant system files</param>
        public void AddOrUpdate(ICollection<CASRecord> records, TACTRepo tactRepo = null)
        {
            foreach (CASRecord record in records)
            {
                AddOrUpdate(record, tactRepo);
            }
        }

        /// <summary>
        /// Adds or Updates a RootRecord and amends all associated system files. If no block is found the Common block is used.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="tactRepo">If provided, will add the entry to all relevant system files</param>
        public void AddOrUpdate(CASRecord record, TACTRepo tactRepo = null)
        {
            if (FileLookup == null)
                throw new NullReferenceException($"{nameof(FileLookup)} has not be instantiated");

            var rootRecord = new RootRecord()
            {
                CKey = record.CKey,
                NameHash = _lookup3.ComputeHash(record.FileName),
                FileId = FileLookup.GetOrCreateFileId(record.FileName)
            };

            AddOrUpdate(rootRecord);

            // add the record to all referenced files
            if (tactRepo != null)
            {
                tactRepo.EncodingFile?.AddOrUpdate(record);
                tactRepo.IndexContainer?.Enqueue(record);
                tactRepo.DownloadFile?.AddOrUpdate(record, 2);
                tactRepo.DownloadSizeFile?.AddOrUpdate(record);
            }
        }

        /// <summary>
        /// Adds or Updates a RootRecord. If no block is found the Common block is used
        /// </summary>
        /// <param name="rootRecord"></param>
        public void AddOrUpdate(RootRecord rootRecord)
        {
            ulong nameHash = rootRecord.NameHash;
            uint fileId = rootRecord.FileId;

            // update the lookup
            _idLookup[nameHash] = fileId;

            var blocks = GetBlocks(LocaleFlags, ContentFlags);
            bool isupdate = blocks.Any(x => x.Records.ContainsKey(fileId));

            // add or update compliant blocks
            foreach (var block in blocks)
            {
                if (!isupdate || block.Records.ContainsKey(fileId))
                    block.Records[fileId] = rootRecord;
            }

            // add the record to a common block
            if (!blocks.Any())
            {
                var block = GetBlocks(LocaleFlags.All_WoW).First(x => x.ContentFlags == ContentFlags.None);
                block.Records[fileId] = rootRecord;
            }
        }

        /// <summary>
        /// Removes files based on their <paramref name="fileId"/>
        /// </summary>
        /// <param name="fileId"></param>
        public void Remove(uint fileId)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);

            foreach (var block in blocks)
                if (block.Records.ContainsKey(fileId))
                    block.Records.Remove(fileId);
        }
        /// <summary>
        /// Removes files based on their <paramref name="namehash"/>
        /// </summary>
        /// <param name="namehash"></param>
        public void Remove(ulong namehash)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);

            if (_idLookup.TryGetValue(namehash, out uint fileid))
                foreach (var block in blocks)
                    block.Records.Remove(fileid);
        }
        /// <summary>
        /// Removes files based on their <paramref name="filepath"/>
        /// </summary>
        /// <param name="filepath"></param>
        public void Remove(string filepath)
        {
            ulong namehash = _lookup3.ComputeHash(filepath);
            Remove(namehash);
        }

        /// <summary>
        /// Returns RootRecords based on their <paramref name="fileId"/>
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(uint fileId)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);

            foreach (var block in blocks)
                if (block.Records.TryGetValue(fileId, out var record))
                    yield return record;
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="namehash"/>
        /// </summary>
        /// <param name="namehash"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(ulong namehash)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);

            if (_idLookup.TryGetValue(namehash, out uint fileid))
                foreach (var block in blocks)
                    if (block.Records.TryGetValue(fileid, out var rootRecord))
                        yield return rootRecord;
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="filepath"/>
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(string filepath)
        {
            ulong namehash = _lookup3.ComputeHash(filepath);
            return Get(namehash);
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="ckey"/>
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(MD5Hash ckey)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);
            foreach (var block in blocks)
                foreach (var record in block.Records.Values)
                    if (record.CKey == ckey)
                        yield return record;
        }

        /// <summary>
        /// Returns the NameHash for the supplied FileId
        /// </summary>
        /// <param name="fileid"></param>
        /// <param name="namehash"></param>
        /// <returns></returns>
        public bool TryGetHashByFileId(uint fileid, out ulong namehash)
        {
            foreach (var block in _blocks)
            {
                if (block.Records.TryGetValue(fileid, out var record))
                {
                    namehash = record.NameHash;
                    return true;
                }
            }

            namehash = 0;
            return false;
        }

        /// <summary>
        /// Determines if the supplied FileId exists
        /// </summary>
        /// <param name="fileid"></param>
        /// <returns></returns>
        public bool ContainsFileId(uint fileid)
        {
            foreach (var block in _blocks)
                if (block.Records.ContainsKey(fileid))
                    return true;

            return false;
        }
        /// <summary>
        /// Determines if the supplied NameHash exists
        /// </summary>
        /// <param name="namehash"></param>
        /// <returns></returns>
        public bool ContainsNameHash(ulong namehash) => _idLookup.ContainsKey(namehash);
        /// <summary>
        /// Determines if the supplied FileName exists
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool ContainsFilename(string filename) => ContainsNameHash(_lookup3.ComputeHash(filename));
        /// <summary>
        /// Determines if the supplied CKey exists
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public bool ContainsCKey(MD5Hash ckey)
        {
            foreach (var block in _blocks)
                foreach (var record in block.Records.Values)
                    if (record.CKey == ckey)
                        return true;

            return false;
        }

        /// <summary>
        /// Opens a stream to the data of the supplied FileId. Returns null if not found
        /// </summary>
        /// <param name="fileid"></param>
        /// <param name="tactRepo"></param>
        /// <returns></returns>
        public Stream OpenFile(uint fileid, TACTRepo tactRepo)
        {
            return OpenFile(Get(fileid).FirstOrDefault(), tactRepo);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied namehash. Returns null if not found
        /// </summary>
        /// <param name="fileid"></param>
        /// <param name="tactRepo"></param>
        /// <returns></returns>
        public Stream OpenFile(ulong namehash, TACTRepo tactRepo)
        {
            return OpenFile(Get(namehash).FirstOrDefault(), tactRepo);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied filepath. Returns null if not found
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="tactRepo"></param>
        /// <returns></returns>
        public Stream OpenFile(string filepath, TACTRepo tactRepo)
        {
            return OpenFile(Get(filepath).FirstOrDefault(), tactRepo);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied RootRecord. Returns null if not found
        /// </summary>
        /// <param name="rootRecord"></param>
        /// <returns></returns>
        public Stream OpenFile(RootRecord rootRecord, TACTRepo tactRepo)
        {
            if (rootRecord == null)
                return null;

            return OpenFile(rootRecord.CKey, tactRepo);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied CKey. Returns null if not found
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public Stream OpenFile(MD5Hash ckey, TACTRepo tactRepo)
        {
            if (tactRepo == null)
                return null;

            if (tactRepo.EncodingFile != null && tactRepo.IndexContainer != null)
            {
                if (tactRepo.EncodingFile.TryGetCKeyEntry(ckey, out EncodingContentEntry encodingCKey))
                    return tactRepo.IndexContainer.OpenFile(encodingCKey.EKey);
            }

            return null;
        }

        /// <summary>
        /// Adds a new RootBlock to the collection
        /// </summary>
        /// <param name="localeFlags"></param>
        /// <param name="contentFlags"></param>
        /// <returns></returns>
        public bool AddBlock(LocaleFlags localeFlags, ContentFlags contentFlags)
        {
            // ensure the flag combination doesn't already exist
            if (_blocks.Find(x => x.ContentFlags == contentFlags && x.LocaleFlags == localeFlags) != null)
                return false;

            // add the new block
            var block = CreateRootBlock();
            block.ContentFlags = contentFlags;
            block.LocaleFlags = localeFlags;
            block.Records = new Dictionary<uint, RootRecord>();
            _blocks.Add(block);

            return true;
        }
        /// <summary>
        /// Returns a collection of RootBlocks filtered by their Locale and Content flags
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<IRootBlock> GetBlocks(LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            foreach (var block in _blocks)
                if ((block.LocaleFlags & locale) == locale)
                    if ((block.ContentFlags & content) == content)
                        yield return block;
        }
        /// <summary>
        /// Removes a RootBlock from the collection
        /// </summary>
        /// <param name="rootBlock"></param>
        /// <returns></returns>
        public bool RemoveBlock(IRootBlock rootBlock) => _blocks.Remove(rootBlock);
        /// <summary>
        /// Removes all RootBlocks with the specified Locale and Content flag combination
        /// </summary>
        /// <param name="localeFlags"></param>
        /// <param name="contentFlags"></param>
        /// <returns></returns>
        public bool RemoveBlock(LocaleFlags localeFlags, ContentFlags contentFlags)
        {
            return _blocks.RemoveAll(x => x.ContentFlags == contentFlags && x.LocaleFlags == localeFlags) > 0;
        }

        #endregion

        #region Helpers

        private void FixDeltas()
        {
            uint previousId, currentId;

            foreach (var block in _blocks)
            {
                // order by id
                var records = block.Records.Values.ToList();
                records.Sort((x, y) => x.FileId.CompareTo(y.FileId));

                // re-calculate deltas
                for (int i = 1; i < records.Count; i++)
                {
                    previousId = records[i - 1].FileId;
                    currentId = records[i].FileId;

                    if (previousId + records[i].FileIdDelta + 1 != currentId)
                        records[i].FileIdDelta = currentId - previousId - 1;
                }

                // reallocate the sorted records
                block.Records = records.ToDictionary(x => x.FileId, x => x);
            }
        }

        /// <summary>
        /// IRootBlock Type factory
        /// </summary>
        /// <returns></returns>
        private IRootBlock CreateRootBlock()
        {
            switch (RootHeader.Version)
            {
                case 2:
                    return Activator.CreateInstance<RootBlockV2>();
                default:
                    return Activator.CreateInstance<RootBlock>();
            }
        }

        /// <summary>
        /// Returns the Lookup3 hash of a FilePath
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public ulong HashName(string filename) => _lookup3.ComputeHash(filename);

        #endregion
    }
}

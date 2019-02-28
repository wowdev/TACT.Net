using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Encoding;
using TACT.Net.FileLookup;
using TACT.Net.SystemFiles;

namespace TACT.Net.Root
{
    /// <summary>
    /// A catalogue of all files stored in the data archives
    /// <para>Blocks contain variants of each file seperated by their Locale and Content flags</para>
    /// </summary>
    public class RootFile : ISystemFile
    {
        /// <summary>
        /// File lookup for filedataids-to-filenames
        /// </summary>
        public IFileLookup FileLookup { get; set; }
        /// <summary>
        /// LocaleFlags to target a specific locale
        /// </summary>
        public LocaleFlags LocaleFlags { get; set; } = LocaleFlags.enUS;
        /// <summary>
        /// ContentFlags to target a specific file set
        /// </summary>
        public ContentFlags ContentFlags { get; set; } = ContentFlags.None;

        public MD5Hash Checksum { get; private set; }

        private readonly EMap[] _EncodingMap = new[] { new EMap(EType.ZLib, 9) };
        private readonly List<RootBlock> _blocks;
        private readonly Lookup3 _lookup3;
        private readonly Dictionary<uint, ulong> _idLookup;

        #region Constructors

        /// <summary>
        /// Creates a new RootFile
        /// </summary>
        public RootFile()
        {
            _idLookup = new Dictionary<uint, ulong>();
            _lookup3 = new Lookup3();

            // add the default global block
            _blocks = new List<RootBlock>
            {
                new RootBlock()
                {
                    ContentFlags = ContentFlags.None,
                    LocaleFlags = LocaleFlags.All_WoW,
                    Records = new Dictionary<ulong, RootRecord>()
                }
            };
        }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="path">/// <param name="path">BLTE encoded file path</param></param>
        public RootFile(string path) : this()
        {
            _blocks.Clear();

            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="hash">RootFile MD5</param>
        public RootFile(string directory, MD5Hash hash) : this(Helpers.GetCDNPath(hash.ToString(), "data", directory)) { }

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
            _blocks.Capacity = 0x400; // arbitrary number based on 8.1.5

            using (var br = new BinaryReader(stream))
            {
                long length = stream.Length;
                while (stream.Position < length)
                {
                    int count = br.ReadInt32();
                    RootBlock block = new RootBlock()
                    {
                        ContentFlags = (ContentFlags)br.ReadUInt32(),
                        LocaleFlags = (LocaleFlags)br.ReadUInt32()
                    };

                    // load the deltas, set the block's record capacity
                    var fileIdDeltas = br.ReadStructArray<uint>(count);
                    block.Records = new Dictionary<ulong, RootRecord>(fileIdDeltas.Length);

                    // calculate the records
                    uint currentId = 0;
                    RootRecord record;
                    foreach (uint delta in fileIdDeltas)
                    {
                        record = new RootRecord { FileIdDelta = delta };
                        record.Read(br);

                        currentId += delta;
                        record.FileId = currentId++;

                        block.Records[record.NameHash] = record;
                        _idLookup[record.FileId] = record.NameHash;
                    }

                    _blocks.Add(block);
                }

                _blocks.TrimExcess();
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
        public CASRecord Write(string directory, TACT tactInstance = null)
        {
            FixDeltas();

            CASRecord record;
            using (var bt = new BlockTableStreamWriter(_EncodingMap[0]))
            using (var bw = new BinaryWriter(bt))
            {
                foreach (var block in _blocks)
                {
                    bw.Write(block.Records.Count);
                    bw.Write((uint)block.ContentFlags);
                    bw.Write((uint)block.LocaleFlags);
                    bw.WriteStructArray(block.Records.Values.Select(x => x.FileIdDelta));
                    foreach (var entry in block.Records.Values)
                        entry.Write(bw);
                }

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
                if (tactInstance != null)
                {
                    tactInstance.EncodingFile?.AddOrUpdate(record);
                    tactInstance.DownloadFile?.AddOrUpdate(record, 0);
                    tactInstance.DownloadSizeFile?.AddOrUpdate(record);
                    tactInstance.ConfigContainer?.BuildConfig?.SetValue("root", record.CKey, 0);
                }

                Checksum = record.CKey;
                return record;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds or Updates a RootRecord and amends all associated system files. If no block is found the Common block is used.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="tactInstance">If provided, will add the entry to all relevant system files</param>
        public void AddOrUpdate(CASRecord record, TACT tactInstance = null)
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
            if (tactInstance != null)
            {
                tactInstance.EncodingFile?.AddOrUpdate(record);
                tactInstance.IndexContainer?.Enqueue(record);
                tactInstance.DownloadFile?.AddOrUpdate(record, 2);
                tactInstance.DownloadSizeFile?.AddOrUpdate(record);
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
            _idLookup[fileId] = nameHash;

            var blocks = GetBlocks(LocaleFlags, ContentFlags);
            bool isupdate = blocks.Any(x => x.Records.ContainsKey(nameHash));

            // add or update compliant blocks
            foreach (var block in blocks)
            {
                if (!isupdate || block.Records.ContainsKey(nameHash))
                    block.Records[nameHash] = rootRecord;
            }

            // add the record to a common block
            if (!blocks.Any())
            {
                var block = GetBlocks(LocaleFlags.All_WoW).First(x => x.ContentFlags == ContentFlags.None);
                block.Records[nameHash] = rootRecord;
            }
        }

        /// <summary>
        /// Removes files based on their <paramref name="fileId"/>
        /// </summary>
        /// <param name="fileId"></param>
        public void Remove(uint fileId)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);

            if (_idLookup.TryGetValue(fileId, out ulong namehash))
                foreach (var block in blocks)
                    if (block.Records.ContainsKey(namehash))
                        block.Records.Remove(namehash);
        }
        /// <summary>
        /// Removes files based on their <paramref name="namehash"/>
        /// </summary>
        /// <param name="namehash"></param>
        public void Remove(ulong namehash)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);
            foreach (var block in blocks)
                if (block.Records.TryGetValue(namehash, out var record))
                    block.Records.Remove(namehash);
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

            if (_idLookup.TryGetValue(fileId, out ulong namehash))
                foreach (var block in blocks)
                    if (block.Records.ContainsKey(namehash))
                        yield return block.Records[namehash];
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="namehash"/>
        /// </summary>
        /// <param name="namehash"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(ulong namehash)
        {
            var blocks = GetBlocks(LocaleFlags, ContentFlags);
            foreach (var block in blocks)
                if (block.Records.TryGetValue(namehash, out var rootRecord))
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
        public bool TryGetHashByFileId(uint fileid, out ulong namehash) => _idLookup.TryGetValue(fileid, out namehash);

        /// <summary>
        /// Determines if the supplied FileId exists
        /// </summary>
        /// <param name="fileid"></param>
        /// <returns></returns>
        public bool ContainsFileId(uint fileid) => _idLookup.ContainsKey(fileid);
        /// <summary>
        /// Determines if the supplied NameHash exists
        /// </summary>
        /// <param name="namehash"></param>
        /// <returns></returns>
        public bool ContainsNameHash(ulong namehash)
        {
            foreach (var block in _blocks)
                if (block.Records.ContainsKey(namehash))
                    return true;

            return false;
        }
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
        /// <param name="tactInstance"></param>
        /// <returns></returns>
        public Stream OpenFile(uint fileid, TACT tactInstance)
        {
            return OpenFile(Get(fileid).FirstOrDefault(), tactInstance);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied namehash. Returns null if not found
        /// </summary>
        /// <param name="fileid"></param>
        /// <param name="tactInstance"></param>
        /// <returns></returns>
        public Stream OpenFile(ulong namehash, TACT tactInstance)
        {
            return OpenFile(Get(namehash).FirstOrDefault(), tactInstance);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied filepath. Returns null if not found
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="tactInstance"></param>
        /// <returns></returns>
        public Stream OpenFile(string filepath, TACT tactInstance)
        {
            return OpenFile(Get(filepath).FirstOrDefault(), tactInstance);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied RootRecord. Returns null if not found
        /// </summary>
        /// <param name="rootRecord"></param>
        /// <returns></returns>
        public Stream OpenFile(RootRecord rootRecord, TACT tactInstance)
        {
            if (rootRecord == null)
                return null;

            return OpenFile(rootRecord.CKey, tactInstance);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied CKey. Returns null if not found
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public Stream OpenFile(MD5Hash ckey, TACT tactInstance)
        {
            if (tactInstance == null)
                return null;

            if (tactInstance.EncodingFile != null && tactInstance.IndexContainer != null)
            {
                if (tactInstance.EncodingFile.TryGetContentEntry(ckey, out EncodingContentEntry encodingCKey))
                    return tactInstance.IndexContainer.OpenFile(encodingCKey.EKey);
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
            _blocks.Add(new RootBlock()
            {
                ContentFlags = contentFlags,
                LocaleFlags = localeFlags,
                Records = new Dictionary<ulong, RootRecord>()
            });

            return true;
        }
        /// <summary>
        /// Returns a collection of RootBlocks filtered by their Locale and Content flags
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootBlock> GetBlocks(LocaleFlags locale, ContentFlags content = ContentFlags.None)
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
        public bool RemoveBlock(RootBlock rootBlock) => _blocks.Remove(rootBlock);
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
                block.Records = records.ToDictionary(x => x.NameHash, x => x);
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

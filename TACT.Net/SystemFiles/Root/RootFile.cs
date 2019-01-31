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
        // TODO kill this
        public TACT TACTInstance
        {
            get => _instance;
            set
            {
                if (value != null)
                    value.RootFile = this; // autoset c ref

                _instance = value;
            }
        }

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

        private TACT _instance;
        private readonly EMap[] _EncodingMap = new[] { new EMap(EType.ZLib, 9) };
        private readonly List<RootBlock> _blocks;
        private readonly Lookup3 _lookup3;
        private readonly Dictionary<uint, ulong> _idLookup;

        #region Constructors

        /// <summary>
        /// Creates a new RootFile
        /// </summary>
        public RootFile(TACT tactInstance = null)
        {
            _idLookup = new Dictionary<uint, ulong>();
            _lookup3 = new Lookup3();

            TACTInstance = tactInstance;

            // add the default global block
            _blocks = new List<RootBlock>
            {
                new RootBlock()
                {
                    ContentFlags = ContentFlags.None,
                    LocaleFlags = LocaleFlags.All_WoW
                }
            };
        }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="path">/// <param name="path">BLTE encoded file path</param></param>
        public RootFile(string path, TACT tactInstance = null) : this()
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
        public RootFile(string directory, MD5Hash hash, TACT tactInstance = null) : this(Helpers.GetCDNPath(hash.ToString(), "data", directory), tactInstance) { }

        /// <summary>
        /// Loads an existing RootFile
        /// </summary>
        /// <param name="stream"></param>
        public RootFile(BlockTableStreamReader stream, TACT tactInstance = null) : this()
        {
            _blocks.Clear();

            Read(stream);
        }

        #endregion

        #region IO

        private void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                long length = stream.Length;
                while (stream.Position < length)
                {
                    int count = br.ReadInt32();
                    RootBlock block = new RootBlock()
                    {
                        ContentFlags = (ContentFlags)br.ReadUInt32(),
                        LocaleFlags = (LocaleFlags)br.ReadUInt32(),
                        Records = new Dictionary<ulong, RootRecord>(),
                    };

                    uint currentId = 0;
                    var fileIdDeltas = br.ReadStructArray<uint>(count);
                    foreach (var delta in fileIdDeltas)
                    {
                        var record = new RootRecord();
                        record.Read(br);

                        record.FileIdDelta = delta;
                        currentId += record.FileIdDelta;
                        record.FileId = currentId++;

                        block.Records[record.NameHash] = record;
                        _idLookup[record.FileId] = record.NameHash;
                    }

                    _blocks.Add(block);
                }

                Checksum = stream.MD5Hash();
            }

            // validate there is a common block
            var commonBlockTest = GetRootBlocks(LocaleFlags.All_WoW).Any(x => x.ContentFlags == ContentFlags.None);
            if (!commonBlockTest)
                throw new InvalidDataException("Root is malformed. Missing common block");
        }

        /// <summary>
        /// Writes the RootFile to disk and optionally updates the Encoding and CDN config files
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public CASRecord Write(string directory)
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
                    bt.WriteTo(fs);

                // add to the encoding file and update the build config
                if (TACTInstance != null)
                {
                    TACTInstance.EncodingFile?.AddOrUpdate(record);
                    TACTInstance.ConfigContainer?.BuildConfig?.SetValue("root", record.CKey, 0);
                }

                Checksum = record.CKey;
                return record;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Inserts a CASRecord into the RootFile, if no block is found the Common block is used
        /// <para>Note: This will override existing records</para>
        /// </summary>
        /// <param name="record"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        public void AddOrUpdate(CASRecord record)
        {
            var rootRecord = new RootRecord()
            {
                CKey = record.CKey,
                NameHash = _lookup3.ComputeHash(record.FileName),
                FileId = FileLookup.GetOrCreateFileId(record.FileName)
            };

            AddOrUpdate(rootRecord);

            // add the record to all referenced files
            if (TACTInstance != null)
            {
                TACTInstance.EncodingFile?.AddOrUpdate(record);
                TACTInstance.IndexContainer?.Enqueue(record);
                TACTInstance.DownloadFile?.AddOrUpdate(record, 2);
                TACTInstance.DownloadSizeFile?.AddOrUpdate(record);
            }
        }
        /// <summary>
        /// Inserts a RootRecord into the RootFile, if no block is found the Common block is used
        /// <para>Note: This will override existing records</para>
        /// </summary>
        /// <param name="rootRecord"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        public void AddOrUpdate(RootRecord rootRecord)
        {
            ulong nameHash = rootRecord.NameHash;
            uint fileId = rootRecord.FileId;

            // update the lookup
            _idLookup[fileId] = nameHash;

            var blocks = GetRootBlocks(LocaleFlags, ContentFlags);
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
                var block = GetRootBlocks(LocaleFlags.All_WoW).First(x => x.ContentFlags == ContentFlags.None);
                block.Records[nameHash] = rootRecord;
            }
        }

        /// <summary>
        /// Removes files based on their <paramref name="fileId"/> and optionally their flags
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        public void Remove(uint fileId)
        {
            var blocks = GetRootBlocks(LocaleFlags, ContentFlags);

            if (_idLookup.TryGetValue(fileId, out ulong namehash))
                foreach (var block in blocks)
                    if (block.Records.ContainsKey(namehash))
                        block.Records.Remove(namehash);
        }
        /// <summary>
        /// Removes files based on their <paramref name="namehash"/> and optionally their flags
        /// </summary>
        /// <param name="namehash"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        public void Remove(ulong namehash)
        {
            var blocks = GetRootBlocks(LocaleFlags, ContentFlags);
            foreach (var block in blocks)
            {
                if (block.Records.TryGetValue(namehash, out var record))
                    block.Records.Remove(namehash);
            }
        }
        /// <summary>
        /// Removes files based on their <paramref name="filepath"/> and optionall their flags
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        public void Remove(string filepath)
        {
            ulong namehash = _lookup3.ComputeHash(filepath);
            Remove(namehash);
        }

        /// <summary>
        /// Returns RootRecords based on their <paramref name="fileId"/> and optionally their flags
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(uint fileId)
        {
            var blocks = GetRootBlocks(LocaleFlags, ContentFlags);

            if (_idLookup.TryGetValue(fileId, out ulong namehash))
                foreach (var block in blocks)
                    if (block.Records.ContainsKey(namehash))
                        yield return block.Records[namehash];
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="namehash"/> and optionally their flags
        /// </summary>
        /// <param name="namehash"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(ulong namehash)
        {
            var blocks = GetRootBlocks(LocaleFlags, ContentFlags);

            foreach (var block in blocks)
                if (block.Records.TryGetValue(namehash, out var rootRecord))
                    yield return rootRecord;
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="filepath"/> and optionally their flags
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(string filepath)
        {
            ulong namehash = _lookup3.ComputeHash(filepath);
            return Get(namehash);
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="ckey"/> and optionally their flags
        /// </summary>
        /// <param name="ckey"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(MD5Hash ckey)
        {
            var blocks = GetRootBlocks(LocaleFlags, ContentFlags);

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
        /// Opens a stream to the data of the supplied RootRecord. Returns null if not found
        /// </summary>
        /// <param name="rootRecord"></param>
        /// <returns></returns>
        public BlockTableStreamReader OpenFile(RootRecord rootRecord)
        {
            return OpenFile(rootRecord.CKey);
        }
        /// <summary>
        /// Opens a stream to the data of the supplied CKey. Returns null if not found
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public BlockTableStreamReader OpenFile(MD5Hash ckey)
        {
            if (TACTInstance == null)
                return null;

            if (TACTInstance.EncodingFile != null && TACTInstance.IndexContainer != null)
            {
                if (TACTInstance.EncodingFile.TryGetContentEntry(ckey, out EncodingContentEntry encodingCKey) && encodingCKey.EKeys.Count > 0)
                    return TACTInstance.IndexContainer.OpenFile(encodingCKey.EKeys.First());
            }

            return null;
        }

        #endregion

        #region Helpers

        private void FixDeltas()
        {
            foreach (var block in _blocks)
            {
                // order by id
                var records = block.Records.Values.ToList();
                records.Sort((x, y) => x.FileId.CompareTo(y.FileId));

                // re-calculate deltas
                for (int i = 1; i < records.Count; i++)
                {
                    var previousId = records[i - 1].FileId;
                    var currentId = records[i].FileId;

                    if (previousId + records[i].FileIdDelta + 1 != currentId)
                        records[i].FileIdDelta = currentId - previousId - 1;
                }

                // reallocate the sorted records
                block.Records = records.ToDictionary(x => x.NameHash, x => x);
            }
        }

        /// <summary>
        /// Returns a collection Blocks filtered by their Locale and Content flags
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootBlock> GetRootBlocks(LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            foreach (var block in _blocks)
                if ((block.LocaleFlags & locale) == locale)
                    if ((block.ContentFlags & content) == content)
                        yield return block;
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;
using TACT.Net.Encoding;
using TACT.Net.ListFileHandler;
using TACT.Net.SystemFiles.Shared;

namespace TACT.Net.Root
{
    /// <summary>
    /// A catalogue of all files stored in the data archives
    /// <para>Blocks contain variants of each file seperated by their Locale and Content flags</para>
    /// </summary>
    public class RootFile : SystemFileBase
    {
        public IListFile ListFile { get; set; }
        public MD5Hash Checksum { get; private set; }

        private readonly EMap[] _EncodingMap = new[] { new EMap(EType.ZLib, 9) };
        private readonly List<RootBlock> _blocks;
        private readonly Lookup3 _lookup3;
        private readonly Dictionary<uint, ulong> _idLookup;


        #region Constructors

        public RootFile(TACT container = null) : base(container)
        {
            _idLookup = new Dictionary<uint, ulong>();
            _lookup3 = new Lookup3();

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

        public RootFile(string path, TACT container = null) : base(container)
        {
            _idLookup = new Dictionary<uint, ulong>();
            _lookup3 = new Lookup3();
            _blocks = new List<RootBlock>();

            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        public RootFile(BlockTableStreamReader stream, TACT container = null) : base(container)
        {
            _idLookup = new Dictionary<uint, ulong>();
            _lookup3 = new Lookup3();
            _blocks = new List<RootBlock>();

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
                if (Container != null)
                {
                    Container.Resolve<EncodingFile>()?.AddOrUpdate(record);
                    Container.Resolve<Configs.ConfigContainer>()?.BuildConfig?.SetValue("root", record.CKey, 0);
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
        public void AddOrUpdate(CASRecord record, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            var rootRecord = new RootRecord()
            {
                CKey = record.CKey,
                NameHash = _lookup3.ComputeHash(record.FileName),
                FileId = ListFile.GetOrCreateFileId(record.FileName)
            };

            AddOrUpdate(rootRecord, locale, content);

            // add the record to all referenced files
            if (Container != null)
            {
                Container.Resolve<EncodingFile>()?.AddOrUpdate(record);
                Container.Resolve<Archives.ArchiveContainer>()?.Enqueue(record);
                Container.Resolve<Download.DownloadFile>()?.AddOrUpdate(record, 2);
                Container.Resolve<Download.DownloadSizeFile>()?.AddOrUpdate(record);
            }
        }
        /// <summary>
        /// Inserts a RootRecord into the RootFile, if no block is found the Common block is used
        /// <para>Note: This will override existing records</para>
        /// </summary>
        /// <param name="rootRecord"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        public void AddOrUpdate(RootRecord rootRecord, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            ulong nameHash = rootRecord.NameHash;
            uint fileId = rootRecord.FileId;

            // update the lookup
            _idLookup[fileId] = nameHash;

            var blocks = GetRootBlocks(locale, content);
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
        public void Remove(uint fileId, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            var blocks = GetRootBlocks(locale, content);

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
        public void Remove(ulong namehash, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            var blocks = GetRootBlocks(locale, content);
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
        public void Remove(string filepath, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            ulong namehash = _lookup3.ComputeHash(filepath);
            Remove(namehash, locale, content);
        }

        /// <summary>
        /// Returns RootRecords based on their <paramref name="fileId"/> and optionally their flags
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(uint fileId, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            var blocks = GetRootBlocks(locale, content);

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
        public IEnumerable<RootRecord> Get(ulong namehash, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            var blocks = GetRootBlocks(locale, content);

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
        public IEnumerable<RootRecord> Get(string filepath, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            ulong namehash = _lookup3.ComputeHash(filepath);
            return Get(namehash, locale, content);
        }
        /// <summary>
        /// Returns RootRecords based on their <paramref name="ckey"/> and optionally their flags
        /// </summary>
        /// <param name="ckey"></param>
        /// <param name="locale"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public IEnumerable<RootRecord> Get(MD5Hash ckey, LocaleFlags locale = LocaleFlags.All_WoW, ContentFlags content = ContentFlags.None)
        {
            var blocks = GetRootBlocks(locale, content);

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
            if (Container != null && Container.TryResolve<EncodingFile>(out var encodingFile))
                if (encodingFile.TryGetContentEntry(rootRecord.CKey, out EncodingContentEntry encodingCKey) && encodingCKey.EKeys.Count > 0)
                    if (Container.TryResolve<Archives.ArchiveContainer>(out var archiveContainer))
                        return archiveContainer.OpenFile(encodingCKey.EKeys.First());

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

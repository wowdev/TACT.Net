using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;
using TACT.Net.SystemFiles.Shared;

namespace TACT.Net.Encoding
{
    using CKeyPageTable = SortedDictionary<MD5Hash, EncodingContentEntry>;
    using EKeyPageTable = SortedDictionary<MD5Hash, EncodingEncodedEntry>;
    using PageIndexTable = Dictionary<MD5Hash, MD5Hash>;

    using Text = System.Text;

    /// <summary>
    /// A catalogue of Encoding and Content key relationships plus encoding informaton for each file
    /// </summary>
    public class EncodingFile : SystemFileBase
    {
        public EncodingHeader EncodingHeader { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public List<string> ESpecStringTable { get; private set; }
        public IEnumerable<EncodingContentEntry> CKeyEntries => _CKeyEntries.Values;
        public IEnumerable<EncodingEncodedEntry> EKeyEntries => _EKeyEntries.Values;

        private readonly EMap[] _EncodingMap;
        private CKeyPageTable _CKeyEntries;
        private EKeyPageTable _EKeyEntries;
        private bool _requiresRebuild = false;

        #region Constructors

        /// <summary>
        /// Creates a new EncodingFile
        /// </summary>
        public EncodingFile(TACT container = null) : base(container)
        {
            EncodingHeader = new EncodingHeader();
            ESpecStringTable = new List<string> { "" };
            _CKeyEntries = new CKeyPageTable(new HashComparer());
            _EKeyEntries = new EKeyPageTable(new HashComparer());

            _EncodingMap = new[]
            {
                new EMap(EType.None, 6),
                new EMap(EType.ZLib, 9),
                new EMap(EType.None, 6),
                new EMap(EType.None, 6),
                new EMap(EType.None, 6),
                new EMap(EType.None, 6),
                new EMap(EType.ZLib, 9),
            };
        }

        /// <summary>
        /// Loads an existing EncodingFile
        /// </summary>
        /// <param name="path">BLTE encoded file path</param>
        public EncodingFile(string path, TACT container = null) : this(container)
        {
            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing EncodingFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="hash">Encoding EKey</param>
        public EncodingFile(string directory, MD5Hash hash, TACT container = null) :
            this(Helpers.GetCDNPath(hash.ToString(), "data", directory), container)
        { }


        /// <summary>
        /// Loads an existing EncodingFile
        /// </summary>
        /// <param name="stream"></param>
        public EncodingFile(BlockTableStreamReader stream, TACT container = null) : this(container)
        {
            Read(stream);
        }

        #endregion

        #region IO

        private void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                EncodingHeader.Read(br);

                // ESpec string table
                byte[] buffer = br.ReadBytes((int)EncodingHeader.ESpecTableSize);
                ESpecStringTable = Text.Encoding.ASCII.GetString(buffer).Split('\0').ToList();

                // skip CKey page table indices
                stream.Seek((int)EncodingHeader.CKeyPageCount * (EncodingHeader.CKeyHashSize + 16), SeekOrigin.Current);

                // read CKey entries
                ReadPage(br, EncodingHeader.CKeyPageSize << 10, EncodingHeader.CKeyPageCount, _CKeyEntries);

                // skip EKey page table indices
                stream.Seek((int)EncodingHeader.EKeyPageCount * (EncodingHeader.EKeyHashSize + 16), SeekOrigin.Current);

                // read EKey entries
                ReadPage(br, EncodingHeader.EKeyPageSize << 10, EncodingHeader.EKeyPageCount, _EKeyEntries);

                // remainder is an ESpec block for the file itself

                Checksum = stream.MD5Hash();
            }
        }

        /// <summary>
        /// Saves the EncodingFile to disk and updates the BuildConfig
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <returns></returns>
        public CASRecord Write(string directory)
        {
            //RebuildLookups();

            EBlock[] eblocks = new EBlock[_EncodingMap.Length];

            CASRecord record;
            using (var bt = new BlockTableStreamWriter(_EncodingMap[1], 1))
            using (var bw = new BinaryWriter(bt))
            {
                // ESpecStringTable 1
                bt.Write(string.Join('\0', ESpecStringTable).GetBytes());
                EncodingHeader.ESpecTableSize = (uint)bt.Length;

                // CKeysPageIndices 2, CKeysPageTable 3
                WritePage(bw, eblocks, 2, EncodingHeader.CKeyPageSize << 10, _CKeyEntries);

                // EKeysPageIndices 4, EKeysPageTable 5
                WritePage(bw, eblocks, 4, EncodingHeader.EKeyPageSize << 10, _EKeyEntries);

                // Header 0
                bt.AddBlock(_EncodingMap[0], 0);
                EncodingHeader.Write(bw);

                // File ESpec 6
                bt.AddBlock(_EncodingMap[6], 6);
                bt.Write(GetFileESpec(bt.SubStreams).GetBytes());

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
                configContainer.BuildConfig?.SetValue("encoding-size", record.EBlock.DecompressedSize, 0);
                configContainer.BuildConfig?.SetValue("encoding-size", record.EBlock.CompressedSize, 1);
                configContainer.BuildConfig?.SetValue("encoding", record.CKey, 0);
                configContainer.BuildConfig?.SetValue("encoding", record.EKey, 1);
            }

            Checksum = record.CKey;
            return record;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a CASRecord to the EncodingFile generating all required entries. This will overwrite existing entries
        /// </summary>
        /// <param name="record"></param>
        public void AddOrUpdate(CASRecord record)
        {
            // CKeyPageTable - overwrite existing
            var cKeyEntry = new EncodingContentEntry
            {
                CKey = record.CKey,
                DecompressedSize = record.EBlock.DecompressedSize
            };
            cKeyEntry.EKeys.Add(record.EKey);
            _CKeyEntries[record.CKey] = cKeyEntry;

            // EKeyPageTable - overwrite existing
            // get or add to the ESpecStringTable
            int especIndex = ESpecStringTable.IndexOf(record.ESpec);
            if (especIndex == -1)
            {
                especIndex = ESpecStringTable.Count - 2;
                ESpecStringTable.Insert(especIndex, record.ESpec);
            }

            // create the entry
            var eKeyEntry = new EncodingEncodedEntry()
            {
                CompressedSize = record.EBlock.CompressedSize,
                EKey = record.EKey,
                ESpecIndex = (uint)especIndex
            };
            _EKeyEntries[record.EKey] = eKeyEntry;

            _requiresRebuild = true;
        }
        public void AddOrUpdate(EncodingContentEntry entry)
        {
            entry.Validate();
            _CKeyEntries[entry.CKey] = entry;
            _requiresRebuild = true;
        }
        public void AddOrUpdate(EncodingEncodedEntry entry)
        {
            entry.Validate();
            _EKeyEntries[entry.EKey] = entry;
            _requiresRebuild = true;
        }

        /// <summary>
        /// Removes a CASRecord
        /// </summary>
        /// <param name="record"></param>
        public void Remove(CASRecord record)
        {
            if (_CKeyEntries.TryGetValue(record.CKey, out var cKeyEntry))
                Remove(cKeyEntry);
            if (_EKeyEntries.TryGetValue(record.CKey, out var eKeyEntry))
                Remove(eKeyEntry);
        }
        public void Remove(EncodingContentEntry entry)
        {
            if (_CKeyEntries.Remove(entry.CKey))
                _requiresRebuild = true;
        }
        public void Remove(EncodingEncodedEntry entry)
        {
            if (_EKeyEntries.Remove(entry.EKey))
                _requiresRebuild = true;
        }

        /// <summary>
        /// Gets a CKeyEntry by it's Content Key
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool TryGetContentEntry(MD5Hash hash, out EncodingContentEntry entry) => _CKeyEntries.TryGetValue(hash, out entry);
        /// <summary>
        /// Returns all CKeyEntries containing a specific Encoding Key
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public IEnumerable<EncodingContentEntry> GetContentEntryByEKey(MD5Hash hash)
        {
            foreach (var ckeyEntry in _CKeyEntries.Values)
                if (ckeyEntry.EKeys.Contains(hash))
                    yield return ckeyEntry;
        }
        /// <summary>
        /// Gets a EKeyEntry by it's Encoding Key
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool TryGetEncodedEntry(MD5Hash hash, out EncodingEncodedEntry entry) => _EKeyEntries.TryGetValue(hash, out entry);
        /// <summary>
        /// Determines where the specified CKeyEntry exists
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public bool ContainsCKey(MD5Hash ckey) => _CKeyEntries.ContainsKey(ckey);
        /// <summary>
        /// Determines where the specified EKeyEntry exists
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public bool ContainsEKey(MD5Hash ekey) => _EKeyEntries.ContainsKey(ekey);

        #endregion

        #region Helpers

        /// <summary>
        /// Reads entries from pages. Pages are determined by checking entry validity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="br"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageCount"></param>
        /// <param name="container"></param>
        private void ReadPage<T>(BinaryReader br, int pageSize, uint pageCount, IDictionary<MD5Hash, T> container) where T : EncodingEntryBase, new()
        {
            long startPos = br.BaseStream.Position;

            for (uint i = 0; i < pageCount; i++)
            {
                T entry = new T();
                while (entry.Read(br, EncodingHeader))
                {
                    container.Add(entry.Key, entry);
                    entry = new T();
                }

                br.BaseStream.Seek(pageSize - ((br.BaseStream.Position - startPos) % pageSize), SeekOrigin.Current);
            }
        }

        /// <summary>
        /// Generates the pages and page lookups
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eblocks"></param>
        /// <param name="blockIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="container"></param>
        private void WritePage<T>(BinaryWriter bw, EBlock[] eblocks, int blockIndex, int pageSize, IDictionary<MD5Hash, T> container) where T : EncodingEntryBase
        {
            var bt = bw.BaseStream as BlockTableStreamWriter;

            // create page entries
            bt.AddBlock(_EncodingMap[blockIndex + 1], blockIndex + 1);
            var pageIndexTable = WritePageImpl(bw, pageSize, container);

            // create page index
            bt.AddBlock(_EncodingMap[blockIndex], blockIndex);
            foreach (var index in pageIndexTable)
            {
                bw.Write(index.Key.Value);
                bw.Write(index.Value.Value);
            }
            pageIndexTable.Clear();
        }

        /// <summary>
        /// Writes the page data and calculates the page lookups
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bw"></param>
        /// <param name="pageSize"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        private PageIndexTable WritePageImpl<T>(BinaryWriter bw, int pageSize, IDictionary<MD5Hash, T> container) where T : EncodingEntryBase
        {
            PageIndexTable pageIndices = new PageIndexTable();
            bool EOFflag = typeof(T) == typeof(EncodingEncodedEntry);

            using (var md5 = MD5.Create())
            {
                // split entries into pages of pageSize
                var pages = EnumerablePartitioner.ConcreteBatch(container.Values, pageSize, (x) => x.Size);
                uint pageCount = (uint)pages.Count();

                // set Header PageCount
                EncodingHeader.SetPageCount<T>(pageCount);

                uint index = pageCount;
                foreach (var page in pages)
                {
                    // write page entries and pad to pageSize
                    page.ForEach(x => x.Write(bw, EncodingHeader));

                    // apply EOF flag (EKey page)
                    if (EOFflag && --index == 0)
                    {
                        bw.Write(new byte[EncodingHeader.EKeyHashSize]);
                        bw.Write(0xFFFFFFFF);
                    }

                    // pad to page size
                    bw.Write(new byte[pageSize - (bw.BaseStream.Position % pageSize)]);

                    // create page index record
                    pageIndices[page[0].Key] = bw.BaseStream.HashSlice(md5, bw.BaseStream.Position - pageSize, pageSize);
                    page.Clear();
                }
            }

            return pageIndices;
        }

        /// <summary>
        /// Returns the ESpec for the EncodingFile itself
        /// </summary>
        /// <param name="substreams"></param>
        /// <returns></returns>
        private string GetFileESpec(IEnumerable<BlockTableSubStream> substreams)
        {
            Text.StringBuilder sb = new Text.StringBuilder(_EncodingMap.Length * 0xA);

            // explicit sizes for the previous blocks
            foreach (var stream in substreams.Take(substreams.Count() - 1))
                sb.Append($"{stream.Length}={(char)stream.EncodingMap.Type},");

            // greedy final block
            sb.Append($"*={(char)substreams.Last().EncodingMap.Type}");

            return "b:{" + sb.ToString().ToLowerInvariant() + "}";
        }

        private void RebuildLookups()
        {
            if (!_requiresRebuild)
                return;

            // TODO need to revisit this
            // do something about broken CKeyEntries links - throw ex?
            // should this check if the Root entry counterpart exists and delete from the archives?

            // remove entries that are x-ref to missing keys
            var eKeys = _EKeyEntries.Keys.ToHashSet();
            foreach (var ckeyentry in _CKeyEntries)
            {
                ckeyentry.Value.EKeys.RemoveWhere(x => !_EKeyEntries.ContainsKey(x));
                eKeys.ExceptWith(ckeyentry.Value.EKeys);
            }

            // remove unreferenced EKeys
            foreach (var ekey in eKeys)
                _EKeyEntries.Remove(ekey);

            _requiresRebuild = false;
        }

        #endregion
    }
}

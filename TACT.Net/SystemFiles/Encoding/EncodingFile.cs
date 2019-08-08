using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Network;

namespace TACT.Net.Encoding
{
    using CKeyPageTable = SortedList<MD5Hash, EncodingContentEntry>;
    using EKeyPageTable = SortedList<MD5Hash, EncodingEncodedEntry>;
    using PageIndexTable = Dictionary<MD5Hash, MD5Hash>;

    using Text = System.Text;

    /// <summary>
    /// A catalogue of Encoding and Content key relationships plus encoding informaton for each file
    /// </summary>
    public class EncodingFile : ISystemFile
    {
        public EncodingHeader EncodingHeader { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public string FilePath { get; private set; }
        public List<string> ESpecStringTable { get; private set; }
        public IEnumerable<EncodingContentEntry> CKeyEntries => _CKeyEntries.Values;
        public IEnumerable<EncodingEncodedEntry> EKeyEntries => _EKeyEntries.Values;
        public readonly bool Partial;

        private readonly EMap[] _EncodingMap;
        private CKeyPageTable _CKeyEntries;
        private EKeyPageTable _EKeyEntries;

        #region Constructors

        /// <summary>
        /// Creates a new EncodingFile
        /// </summary>
        public EncodingFile()
        {
            EncodingHeader = new EncodingHeader();
            ESpecStringTable = new List<string> { "z", "" };
            _CKeyEntries = new CKeyPageTable(new MD5HashComparer());
            _EKeyEntries = new EKeyPageTable(new MD5HashComparer());

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
        /// <param name="partial">Only reads the mandatory information. Prevents write support</param>
        public EncodingFile(string path, bool partial = false) : this()
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Unable to open EncodingFile", path);

            FilePath = path;
            Partial = partial;

            using (var fs = File.OpenRead(path))
            using (var bt = new BlockTableStreamReader(fs))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing EncodingFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="ekey">Encoding EKey</param>
        /// <param name="partial">Only reads the mandatory information. Prevents write support</param>
        public EncodingFile(string directory, MD5Hash ekey, bool partial = false) :
            this(Helpers.GetCDNPath(ekey.ToString(), "data", directory), partial)
        { }

        /// <summary>
        /// Loads an existing EncodingFile from a remote CDN
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ekey"></param>
        /// <param name="partial">Only reads the mandatory information. Prevents write support</param>
        public EncodingFile(CDNClient client, MD5Hash ekey, bool partial = false) : this()
        {
            Partial = partial;

            string url = Helpers.GetCDNUrl(ekey.ToString(), "data");

            using (var stream = client.OpenStream(url).Result)
            using (var bt = new BlockTableStreamReader(stream))
                Read(bt);
        }

        /// <summary>
        /// Loads an existing EncodingFile
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="partial">Only reads the mandatory information. Prevents write support</param>
        public EncodingFile(BlockTableStreamReader stream, bool partial = false) : this()
        {
            Partial = partial;

            Read(stream);
        }

        #endregion

        #region IO

        private void Read(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead || stream.Length <= 1)
                throw new NotSupportedException($"Unable to read EncodingFile stream");

            using (var br = new BinaryReader(stream))
            {
                EncodingHeader.Read(br);

                // ESpec string table
                byte[] buffer = br.ReadBytes((int)EncodingHeader.ESpecTableSize);
                ESpecStringTable = Text.Encoding.ASCII.GetString(buffer).Split('\0').ToList();

                // skip CKey page table indices
                stream.Seek((int)EncodingHeader.CKeyPageCount * (EncodingHeader.CKeyHashSize + 16), SeekOrigin.Current);

                // read CKey entries
                _CKeyEntries.Capacity = (int)EncodingHeader.CKeyPageCount * 40;
                ReadPage(br, EncodingHeader.CKeyPageSize << 10, EncodingHeader.CKeyPageCount, _CKeyEntries);

                if (!Partial)
                {
                    // skip EKey page table indices
                    stream.Seek((int)EncodingHeader.EKeyPageCount * (EncodingHeader.EKeyHashSize + 16), SeekOrigin.Current);

                    // read EKey entries
                    _EKeyEntries.Capacity = (int)EncodingHeader.CKeyPageCount * 25;
                    ReadPage(br, EncodingHeader.EKeyPageSize << 10, EncodingHeader.EKeyPageCount, _EKeyEntries);

                    // remainder is an ESpec block for the file itself
                }

                Checksum = stream.MD5Hash();
            }
        }

        /// <summary>
        /// Saves the EncodingFile to disk and optionally updates the BuildConfig
        /// </summary>
        /// <param name="directory">Root Directory</param>
        /// <param name="configContainer"></param>
        /// <returns></returns>
        public CASRecord Write(string directory, TACTRepo repo = null)
        {
            if (Partial)
                throw new NotSupportedException("Writing is not supported for partial EncodingFiles");

            if (_EKeyEntries.Count != _CKeyEntries.Count)
                throw new InvalidDataException("CKeyEntry and EKeyEntry count must match");

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
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                }
            }

            // update the build config with the new values
            if (repo?.ConfigContainer?.BuildConfig != null)
            {
                repo.ConfigContainer.BuildConfig.SetValue("encoding-size", record.EBlock.DecompressedSize, 0);
                repo.ConfigContainer.BuildConfig.SetValue("encoding-size", record.EBlock.CompressedSize, 1);
                repo.ConfigContainer.BuildConfig.SetValue("encoding", record.CKey, 0);
                repo.ConfigContainer.BuildConfig.SetValue("encoding", record.EKey, 1);
            }

            Checksum = record.CKey;
            FilePath = record.BLTEPath;
            return record;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a CASRecord to the EncodingFile generating all required entries. This will overwrite existing entries
        /// </summary>
        /// <param name="record"></param>
        public void AddOrUpdate(CASRecord record, TACTRepo tactRepo = null)
        {
            // CKeyPageTable
            Remove(record, tactRepo);
            var cKeyEntry = new EncodingContentEntry
            {
                CKey = record.CKey,
                EKey = record.EKey,
                DecompressedSize = record.EBlock.DecompressedSize,
            };
            _CKeyEntries.Add(record.CKey, cKeyEntry);

            // get or add to the ESpecStringTable
            int especIndex = ESpecStringTable.IndexOf(record.ESpec);
            if (especIndex == -1)
            {
                especIndex = ESpecStringTable.Count - 2;
                ESpecStringTable.Insert(especIndex, record.ESpec);
            }

            // EKeyPageTable
            var eKeyEntry = new EncodingEncodedEntry()
            {
                CompressedSize = record.EBlock.CompressedSize,
                EKey = record.EKey,
                ESpecIndex = (uint)especIndex
            };
            _EKeyEntries[record.EKey] = eKeyEntry;

            // propogate the new record
            if (tactRepo != null)
            {
                tactRepo.IndexContainer?.Enqueue(record);
                tactRepo.DownloadFile?.AddOrUpdate(record);
                tactRepo.DownloadSizeFile?.AddOrUpdate(record);
            }
        }
        /// <summary>
        /// Removes a Content Entry
        /// <para>Note: CKeys and EKeys must have a 1:1 relationship</para>
        /// </summary>
        /// <param name="entry"></param>
        public void AddOrUpdate(EncodingContentEntry entry)
        {
            entry.Validate();
            _CKeyEntries[entry.CKey] = entry;
        }
        /// <summary>
        /// Removes am Encoding Entry
        /// <para>Note: CKeys and EKeys must have a 1:1 relationship</para>
        /// </summary>
        /// <param name="entry"></param>
        public void AddOrUpdate(EncodingEncodedEntry entry)
        {
            entry.Validate();
            _EKeyEntries[entry.EKey] = entry;
        }

        /// <summary>
        /// Removes a CASRecord
        /// </summary>
        /// <param name="record"></param>
        public bool Remove(CASRecord record, TACTRepo tactRepo = null)
        {
            if (record == null)
                return false;

            if (_CKeyEntries.TryGetValue(record.CKey, out var entry))
            {
                _CKeyEntries.Remove(record.CKey);
                _EKeyEntries.Remove(entry.EKey);

                // propagate removal
                if (tactRepo != null)
                {
                    tactRepo.IndexContainer?.Remove(entry.EKey);
                    tactRepo.DownloadFile?.Remove(entry.EKey);
                    tactRepo.DownloadSizeFile?.Remove(entry.EKey);
                }

                return true;
            }

            return false;
        }
        /// <summary>
        /// Removes a Content Entry
        /// <para>Note: CKeys and EKeys must have a 1:1 relationship</para>
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool Remove(EncodingContentEntry entry)
        {
            return entry == null || _CKeyEntries.Remove(entry.CKey);
        }
        /// <summary>
        /// Removes an Encoding Entry
        /// <para>Note: CKeys and EKeys must have a 1:1 relationship</para>
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool Remove(EncodingEncodedEntry entry)
        {
            return entry == null || _EKeyEntries.Remove(entry.EKey);
        }

        /// <summary>
        /// Gets a CKeyEntry by it's Content Key
        /// </summary>
        /// <param name="ckey"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool TryGetCKeyEntry(MD5Hash ckey, out EncodingContentEntry entry) => _CKeyEntries.TryGetValue(ckey, out entry);
        /// <summary>
        /// Gets a Content Entry by it's Encoding Key
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public EncodingContentEntry GetCKeyEntryByEKey(MD5Hash ekey)
        {
            foreach (var ckeyEntry in _CKeyEntries.Values)
                if (ckeyEntry.EKey == ekey)
                    return ckeyEntry;

            return null;
        }
        /// <summary>
        /// Gets a EKeyEntry by it's Encoding Key
        /// </summary>
        /// <param name="ekey"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool TryGetEKeyEntry(MD5Hash ekey, out EncodingEncodedEntry entry) => _EKeyEntries.TryGetValue(ekey, out entry);
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
        /// <param name="reader"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageCount"></param>
        /// <param name="container"></param>
        private void ReadPage<T>(BinaryReader reader, int pageSize, uint pageCount, IDictionary<MD5Hash, T> container) where T : EncodingEntryBase, new()
        {
            for (uint i = 0; i < pageCount; i++)
            {
                // buffer the page then read the entries to minimise file reads
                using (var ms = new MemoryStream(reader.ReadBytes(pageSize)))
                using (var br = new BinaryReader(ms))
                {
                    T entry = new T();
                    while (entry.Read(br, EncodingHeader))
                    {
                        container.Add(entry.Key, entry);
                        entry = new T();
                    }
                }
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
            using (var md5 = MD5.Create())
            {
                // split entries into pages of pageSize
                var pages = EnumerablePartitioner.ConcreteBatch(container.Values, pageSize, (x) => x.Size);
                uint pageCount = (uint)pages.Count();

                PageIndexTable pageIndices = new PageIndexTable((int)pageCount);
                bool EOFflag = typeof(T) == typeof(EncodingEncodedEntry);

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

                return pageIndices;
            }
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

        #endregion
    }
}

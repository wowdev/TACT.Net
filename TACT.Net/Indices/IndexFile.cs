using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Network;

namespace TACT.Net.Indices
{
    /// <summary>
    /// A lookup for files within the data blobs
    /// </summary>
    public class IndexFile
    {
        public IEnumerable<IndexEntry> Entries => _indexEntries.Values;
        public IndexFooter IndexFooter { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public readonly IndexType Type;

        #region Helper Variables

        internal bool RequiresSave { get; private set; }
        internal bool IsDataIndex => (Type & IndexType.Data) == IndexType.Data;
        internal bool IsLooseIndex => (Type & IndexType.Loose) == IndexType.Loose;
        internal bool IsGroupIndex => (Type & IndexType.Group) == IndexType.Group;
        internal bool IsPatchIndex => (Type & IndexType.Patch) == IndexType.Patch;

        #endregion

        private readonly SortedList<MD5Hash, IndexEntry> _indexEntries;
        private readonly Dictionary<MD5Hash, CASRecord> _newEntries;
        private ulong _currentOffset;

        #region Constructors

        /// <summary>
        /// Creates a new IndexFile
        /// </summary>
        public IndexFile(IndexType type)
        {
            _indexEntries = new SortedList<MD5Hash, IndexEntry>(new MD5HashComparer());
            _newEntries = new Dictionary<MD5Hash, CASRecord>();

            Type = type;
            IndexFooter = new IndexFooter();

            if (IsLooseIndex)
                IndexFooter.OffsetBytes = 0;
            else if (IsGroupIndex)
                IndexFooter.OffsetBytes = 6;
        }

        /// <summary>
        /// Loads an IndexFile from a filepath
        /// </summary>
        /// <param name="path"></param>
        public IndexFile(string path) : this(IndexType.Unknown)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Unable to open IndexFile", path);

            using (var fs = File.OpenRead(path))
                Read(fs);

            Type = DetermineType(Type, path);
        }

        /// <summary>
        /// Loads an IndexFile from a remote CDN
        /// </summary>
        /// <param name="stream"></param>
        public IndexFile(CDNClient client, string path, IndexType type) : this(IndexType.Unknown)
        {
            if (!path.EndsWith(".index", StringComparison.OrdinalIgnoreCase))
                path = path + ".index";

            string endpoint = type.HasFlag(IndexType.Data) ? "data" : "patch";
            string url = Helpers.GetCDNUrl(path, endpoint);

            using (var stream = client.OpenStream(url).Result)
                Read(stream);

            Type = DetermineType(type);
        }

        /// <summary>
        /// Loads an IndexFile from a stream
        /// </summary>
        /// <param name="stream"></param>
        public IndexFile(Stream stream, IndexType type) : this(IndexType.Unknown)
        {
            Read(stream);

            Type = DetermineType(type);
        }
        #endregion

        #region IO

        private void Read(Stream stream)
        {
            if (!stream.CanRead || stream.Length <= 0)
                throw new NotSupportedException($"Unable to read IndexFile stream");

            using (var md5 = MD5.Create())
            using (var reader = new BinaryReader(stream))
            {
                IndexFooter.Read(reader);

                // calculate file dimensions
                var (PageSize, _, PageCount) = GetFileDimensions();

                // read the entries
                _indexEntries.Capacity = (int)IndexFooter.EntryCount;
                stream.Position = 0;

                for (int i = 0; i < PageCount; i++)
                {
                    // buffer the page then read the entries to minimise file reads
                    using (var ms = new MemoryStream(reader.ReadBytes(PageSize)))
                    using (var br = new BinaryReader(ms))
                    {
                        var entry = new IndexEntry();
                        while (entry.Read(br, IndexFooter))
                        {
                            _indexEntries[entry.Key] = entry;
                            entry = new IndexEntry();
                        }
                    }
                }

                // calculate the current filename
                Checksum = stream.HashSlice(md5, stream.Length - IndexFooter.Size + IndexFooter.ChecksumSize, IndexFooter.Size - IndexFooter.ChecksumSize);

                // store the current blob offset
                _currentOffset = (ulong)_indexEntries.Values.Sum(x => (long)x.CompressedSize);
            }
        }

        /// <summary>
        /// Saves the IndexFile to disk and optionally updates the CDN config
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="configContainer"></param>
        public void Write(string directory, Configs.ConfigContainer configContainer = null)
        {
            RequiresSave = false;

            // TODO patch index writing
            if (IsPatchIndex || Type == IndexType.Unknown)
                throw new NotImplementedException();
            // Group Indicies only supported for Data and Patch indicies
            if (IsGroupIndex && IsLooseIndex)
                throw new NotImplementedException();

            List<MD5Hash> EKeyLookupHashes = new List<MD5Hash>();
            List<MD5Hash> PageChecksums = new List<MD5Hash>();

            // update Footer
            IndexFooter.EntryCount = (uint)_indexEntries.Count;

            // get file dimensions
            var (PageSize, EntriesPerPage, PageCount) = GetFileDimensions();

            using (var md5 = MD5.Create())
            using (var ms = new MemoryStream(PageCount * (PageSize + 1)))
            using (var bw = new BinaryWriter(ms))
            {
                // set capcity
                EKeyLookupHashes.Capacity = PageCount;
                PageChecksums.Capacity = PageCount;

                // IndexEntries
                int index = 0;
                for (int i = 0; i < PageCount; i++)
                {
                    // write the entries
                    for (int j = 0; j < EntriesPerPage && index < IndexFooter.EntryCount; j++)
                        _indexEntries.Values[index++].Write(bw, IndexFooter);

                    // apply padding and store EKey and page checksum
                    int remainder = (int)bw.BaseStream.Position % PageSize;
                    if (remainder > 0)
                        ms.Write(new byte[PageSize - remainder]);

                    EKeyLookupHashes.Add(_indexEntries.Values[index - 1].Key);
                    PageChecksums.Add(ms.HashSlice(md5, bw.BaseStream.Position - PageSize, PageSize, IndexFooter.ChecksumSize));
                }

                // EKey Lookup
                long lookupStartPos = bw.BaseStream.Position;
                foreach (var lookupHash in EKeyLookupHashes)
                    bw.Write(lookupHash.Value);

                // Page hashes - final page is ignored
                long pageStartPos = bw.BaseStream.Position;
                PageChecksums.RemoveAt(PageChecksums.Count - 1);
                foreach (var pagechecksum in PageChecksums)
                    bw.Write(pagechecksum.Value);

                // LastPage hash - last PageSize of Entries
                long footerStartPos = bw.BaseStream.Position;
                IndexFooter.LastPageHash = ms.HashSlice(md5, lookupStartPos - PageSize, PageSize, IndexFooter.ChecksumSize);
                bw.Write(IndexFooter.LastPageHash.Value);

                // TOC hash - from EKey Lookup to LastPage Hash
                IndexFooter.ContentsHash = ms.HashSlice(md5, lookupStartPos, ms.Length - lookupStartPos, IndexFooter.ChecksumSize);
                bw.Write(IndexFooter.ContentsHash.Value);

                // write footer
                IndexFooter.Write(bw);

                // compute filename - from ContentsHash to EOF
                MD5Hash newChecksum = ms.HashSlice(md5, footerStartPos + IndexFooter.ChecksumSize, IndexFooter.Size - IndexFooter.ChecksumSize);
                // update the CDN Config
                UpdateConfig(configContainer, newChecksum, bw.BaseStream.Length);

                //// remove old index file
                //if (!Checksum.IsEmpty)
                //    Helpers.Delete(Checksum.ToString() + ".index", directory);

                // update Checksum
                Checksum = newChecksum;

                // Group Indicies are generated client-side
                if (IsGroupIndex)
                    return;

                string saveLocation = Helpers.GetCDNPath(Checksum.ToString() + ".index", "data", directory, true);
                if (!File.Exists(saveLocation))
                {
                    // save to disk
                    File.WriteAllBytes(saveLocation, ms.ToArray());
                }
            }
        }

        /// <summary>
        /// Generates the archive blob from the archive's index entries
        /// <para>New entries are added while old are copied from the previous blob</para>
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="prevBlob"></param>
        public void WriteBlob(string directory, string prevBlob = "")
        {
            // TODO patch blob writing
            if (IsPatchIndex || IsGroupIndex || Type == IndexType.Unknown)
                throw new NotImplementedException();

            string saveLocation = Helpers.GetCDNPath(Checksum.ToString(), "data", directory, true);

            // load the previous blob to copy data from
            FileStream blob = null;
            long blobLength = 0;

            if (!Checksum.IsEmpty && !string.IsNullOrEmpty(prevBlob))
            {
                if (!File.Exists(prevBlob))
                    throw new FileNotFoundException($"Missing old archive blob {Path.GetFileName(prevBlob)}");

                // same checksum, just overwrite
                if (Path.GetFileName(prevBlob).Equals(Checksum.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(prevBlob, saveLocation, true);
                    return;
                }

                blob = File.OpenRead(prevBlob);
                blobLength = blob.Length;
            }

            // create the new file
            using (var fs = File.Create(saveLocation))
            using (blob)
            {
                foreach (var entry in _indexEntries.Values)
                {
                    fs.Position = entry.Offset;

                    if (_newEntries.TryGetValue(entry.Key, out var record))
                    {
                        // append new entries
                        record.WriteTo(fs);
                        fs.Flush();
                    }
                    else if (blobLength >= entry.Offset + (uint)entry.CompressedSize)
                    {
                        // copy data from the old blob
                        blob.Position = entry.Offset;
                        blob.PartialCopyTo(fs, (long)entry.CompressedSize);
                    }
                }
            }

            _newEntries.Clear();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a CASRecord to the archive
        /// </summary>
        /// <param name="record"></param>
        public void Add(CASRecord record)
        {
            var entry = new IndexEntry()
            {
                Key = IsPatchIndex ? record.CKey : record.EKey,
                CompressedSize = record.EBlock.CompressedSize,
                Offset = (uint)_currentOffset
            };

            _currentOffset += record.EBlock.CompressedSize;

            _indexEntries[entry.Key] = entry;
            _newEntries[entry.Key] = record;

            RequiresSave = true;
        }
        /// <summary>
        /// Adds multiple CASRecords to the archive
        /// </summary>
        /// <param name="records"></param>
        public void Add(IEnumerable<CASRecord> records)
        {
            foreach (var record in records)
                Add(record);
        }
        /// <summary>
        /// Adds a IndexEntry to the archive
        /// </summary>
        /// <param name="entry"></param>
        public void Add(IndexEntry entry)
        {
            entry.Offset = (uint)_currentOffset;
            _currentOffset += entry.CompressedSize;

            _indexEntries[entry.Key] = entry;
            RequiresSave = true;
        }
        /// <summary>
        /// Addes multiple IndexEntries to the archive
        /// </summary>
        /// <param name="records"></param>
        public void Add(IEnumerable<IndexEntry> records)
        {
            foreach (var record in records)
                Add(record);
        }

        /// <summary>
        /// Returns an IndexEntry from the collection if it exists
        /// </summary>
        /// <param name="ekey"></param>
        /// <param name="indexEntry"></param>
        /// <returns></returns>
        public bool TryGet(MD5Hash ekey, out IndexEntry indexEntry)
        {
            return _indexEntries.TryGetValue(ekey, out indexEntry);
        }

        /// <summary>
        /// Determines if the provided hash exists in the collection
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public bool Contains(MD5Hash ekey) => _indexEntries.ContainsKey(ekey);

        /// <summary>
        /// Removes an index entry from the archive
        /// </summary>
        /// <param name="ekey"></param>
        public bool Remove(MD5Hash ekey)
        {
            if (_indexEntries.Remove(ekey))
            {
                _newEntries.Remove(ekey);
                RequiresSave = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes multiple index entries from the archive
        /// </summary>
        /// <param name="hashes"></param>
        public int Remove(IEnumerable<MD5Hash> hashes)
        {
            int count = 0;
            foreach (var hash in hashes)
                if (Remove(hash))
                    count++;

            return count;
        }

        /// <summary>
        /// Copies all entries from an existing IndexFile
        /// </summary>
        /// <param name="indexFile"></param>
        /// <param name="archiveIndex"></param>
        internal void LoadIndicies(List<IndexEntry> entries)
        {
            foreach (var entry in entries)
                _indexEntries.Add(entry.Key, entry);
        }

        #endregion

        #region Helpers

        private IndexType DetermineType(IndexType type, string path = "")
        {
            if (!string.IsNullOrWhiteSpace(path))
                type |= Helpers.PathContainsDirectory(path, "patch") ? IndexType.Patch : IndexType.Data;
            if (IndexFooter.OffsetBytes == 0)
                type |= IndexType.Loose;
            if (IndexFooter.OffsetBytes > 4)
                type |= IndexType.Group;

            return type;
        }

        private void UpdateConfig(Configs.ConfigContainer configContainer, MD5Hash hash, long size)
        {
            if (configContainer?.CDNConfig == null)
                return;

            // determine the field names
            string archivefield, sizefield;
            if (IsGroupIndex)
            {
                archivefield = IsPatchIndex ? "patch-archive-group" : "archive-group";
                sizefield = null;
            }
            else if (IsLooseIndex)
            {
                archivefield = IsPatchIndex ? "patch-file-index" : "file-index";
                sizefield = archivefield + "-size";
            }
            else
            {
                archivefield = IsPatchIndex ? "patch-archives" : "archives";
                sizefield = archivefield + "-index-size";
            }

            // update the collections
            var archives = configContainer.CDNConfig.GetValues(archivefield);
            var sizes = configContainer.CDNConfig.GetValues(sizefield);

            if (archives != null)
            {
                if (IsGroupIndex)
                {
                    archives[0] = hash.ToString(); // group indicies are single entries
                }
                else
                {
                    // remove old hash
                    if (Checksum.Value != null)
                    {
                        int index = archives.IndexOf(Checksum.ToString());
                        if(index > -1)
                        {
                            archives.RemoveAt(index);
                            sizes?.RemoveAt(index);
                        }
                    }

                    // add if new
                    if (!archives.Contains(hash.ToString()))
                    {
                        archives.Add(hash.ToString());
                        sizes?.Add(size.ToString());
                    }
                }
            }
        }

        private (int PageSize, int EntriesPerPage, int PageCount) GetFileDimensions()
        {
            int pageSize = IndexFooter.PageSizeKB << 10;
            int entriesPerPage = pageSize / (0x14 + IndexFooter.OffsetBytes);
            int pageCount = (int)(IndexFooter.EntryCount + entriesPerPage - 1) / entriesPerPage;

            return (pageSize, entriesPerPage, pageCount);
        }

        #endregion
    }
}

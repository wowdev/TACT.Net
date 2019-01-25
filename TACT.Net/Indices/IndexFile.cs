using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;

namespace TACT.Net.Indices
{
    /// <summary>
    /// A lookup file containing location details for files within the data blobs
    /// </summary>
    public class IndexFile
    {
        public IEnumerable<IndexEntry> Entries => _indexEntries.Values;
        public IndexFooter IndexFooter { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public readonly IndexType Type;

        internal bool IsGroupIndex => (Type & IndexType.Group) == IndexType.Group;
        internal bool IsPatchIndex => (Type & IndexType.Patch) == IndexType.Patch;

        private readonly SortedList<MD5Hash, IndexEntry> _indexEntries;
        private readonly Dictionary<MD5Hash, CASRecord> _newEntries;
        private ulong _currentOffset;

        #region Constructors

        /// <summary>
        /// Creates a new IndexFile
        /// </summary>
        public IndexFile()
        {
            _indexEntries = new SortedList<MD5Hash, IndexEntry>(new HashComparer());
            _newEntries = new Dictionary<MD5Hash, CASRecord>();

            Checksum = new MD5Hash(new byte[0]);
            IndexFooter = new IndexFooter();
        }

        /// <summary>
        /// Loads an IndexFile from a filepath
        /// </summary>
        /// <param name="path"></param>
        public IndexFile(string path) : this()
        {
            using (var fs = File.OpenRead(path))
                Read(fs);

            Type = DetermineType(Type, path);
        }

        /// <summary>
        /// Loads an IndexFile from a stream
        /// </summary>
        /// <param name="stream"></param>
        public IndexFile(Stream stream, IndexType type) : this()
        {
            Read(stream);

            Type = DetermineType(type);
        }
        #endregion

        #region IO

        private void Read(Stream stream)
        {
            using (var md5 = MD5.Create())
            using (var br = new BinaryReader(stream))
            {
                IndexFooter.Read(br);

                // calculate file dimensions
                int pageSize = IndexFooter.PageSizeKB << 10;
                int entriesPerPage = pageSize / (0x14 + IndexFooter.OffsetBytes);
                int pageCount = (int)(IndexFooter.EntryCount + entriesPerPage - 1) / entriesPerPage;

                stream.Position = 0;
                for (int i = 0; i < pageCount; i++)
                {
                    for (int b = 0; b < entriesPerPage; b++)
                    {
                        var entry = new IndexEntry();
                        entry.Read(br, IndexFooter);
                        if (!entry.Key.IsEmpty)
                            _indexEntries[entry.Key] = entry;
                    }

                    stream.Seek(pageSize - (stream.Position % pageSize), SeekOrigin.Current);
                }
                _indexEntries.TrimExcess();

                // calculate the current filename
                Checksum = stream.HashSlice(md5, stream.Length - IndexFooter.Size + IndexFooter.ChecksumSize, IndexFooter.Size - IndexFooter.ChecksumSize);

                // store the current blob offset
                _currentOffset = (ulong)_indexEntries.Values.Sum(x => (long)x.CompressedSize);
            }

            stream?.Close();
            stream?.Dispose();
        }

        /// <summary>
        /// Saves the IndexFile
        /// </summary>
        /// <param name="directory"></param>
        public void Write(string directory, TACT container = null)
        {
            // TODO patch index writing
            if (IsPatchIndex || Type == IndexType.Unknown)
                throw new NotImplementedException();

            List<MD5Hash> EKeyLookupHashes = new List<MD5Hash>();
            List<MD5Hash> PageChecksums = new List<MD5Hash>();

            using (var md5 = MD5.Create())
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // update Footer
                IndexFooter.EntryCount = (uint)_indexEntries.Count;

                // get file dimensions
                int pageSize = IndexFooter.PageSizeKB << 10;
                int entriesPerPage = pageSize / (0x14 + IndexFooter.OffsetBytes);
                int pageCount = (int)(IndexFooter.EntryCount + entriesPerPage - 1) / entriesPerPage;

                // set capcity
                EKeyLookupHashes.Capacity = pageCount;
                PageChecksums.Capacity = pageCount;

                // IndexEntries
                int index = 0;
                for (int i = 0; i < pageCount; i++)
                {
                    // write the entries
                    for (int b = 0; b < entriesPerPage && index < IndexFooter.EntryCount; b++)
                        _indexEntries.Values[index++].Write(bw, IndexFooter);

                    // apply padding and store EKey and page checksum
                    ms.Write(new byte[pageSize - (bw.BaseStream.Position % pageSize)]);
                    EKeyLookupHashes.Add(_indexEntries.Values[index - 1].Key);
                    PageChecksums.Add(ms.HashSlice(md5, bw.BaseStream.Position - pageSize, pageSize, IndexFooter.ChecksumSize));
                }

                // EKey Lookup
                long lookupStartPos = bw.BaseStream.Position;
                foreach (var lookupHash in EKeyLookupHashes)
                    bw.Write(lookupHash.Value);

                // Page hashes - final page is ignored
                long pageStartPos = bw.BaseStream.Position;
                PageChecksums.RemoveAt(PageChecksums.Count - 1);
                foreach (var checksum in PageChecksums)
                    bw.Write(checksum.Value);

                // LastPage hash - last PageSize of Entries
                long footerStartPos = bw.BaseStream.Position;
                IndexFooter.LastPageHash = ms.HashSlice(md5, lookupStartPos - pageSize, pageSize, IndexFooter.ChecksumSize);
                bw.Write(IndexFooter.LastPageHash.Value);

                // TOC hash - from EKey Lookup to LastPage Hash
                IndexFooter.ContentsHash = ms.HashSlice(md5, lookupStartPos, ms.Length - lookupStartPos, IndexFooter.ChecksumSize);
                bw.Write(IndexFooter.ContentsHash.Value);

                // write footer
                IndexFooter.Write(bw);

                // compute filename - from ContentsHash to EOF
                var fileHash = ms.HashSlice(md5, footerStartPos + IndexFooter.ChecksumSize, IndexFooter.Size - IndexFooter.ChecksumSize);

                string saveLocation = Helpers.GetCDNPath(fileHash.ToString() + ".index", "data", directory, true);
                if (!File.Exists(saveLocation))
                {
                    // save to disk
                    File.WriteAllBytes(saveLocation, ms.ToArray());

                    // update the CDN Config
                    if (container.TryResolve<Configs.ConfigContainer>(out var configContainer))
                    {
                        var archives = configContainer.CDNConfig?.GetValues("archives");
                        if (archives != null)
                        {
                            archives.Remove(Checksum.ToString());
                            archives.Add(fileHash.ToString());
                            archives.Sort(new HashComparer());
                        }

                        // TODO archives-index-size once the size is known
                    }

                    // store the new checksum
                    Checksum = fileHash;
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
            string saveLocation = Helpers.GetCDNPath(Checksum.ToString(), "data", directory, true);

            // load the previous blob to copy data from
            FileStream blob = null;
            long blobLength = 0;

            if (!Checksum.IsEmpty)
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
                    else if (blobLength > entry.Offset)
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
        /// Returns an IndexEntry from the collection if it exists
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="indexEntry"></param>
        /// <returns></returns>
        public bool TryGet(MD5Hash hash, out IndexEntry indexEntry)
        {
            return _indexEntries.TryGetValue(hash, out indexEntry);
        }

        /// <summary>
        /// Determines if the provided hash exists in the collection
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool Contains(MD5Hash hash) => _indexEntries.ContainsKey(hash);

        /// <summary>
        /// Removes an index entry from the archive
        /// </summary>
        /// <param name="hash"></param>
        public void Remove(MD5Hash hash)
        {
            if (_indexEntries.Remove(hash))
                _newEntries.Remove(hash);
        }

        /// <summary>
        /// Removes multiple index entries from the archive
        /// </summary>
        /// <param name="hashes"></param>
        public void Remove(IEnumerable<MD5Hash> hashes)
        {
            foreach (var hash in hashes)
                Remove(hash);
        }
        #endregion

        #region Helpers

        private IndexType DetermineType(IndexType type, string path = "")
        {
            if(!string.IsNullOrWhiteSpace(path))
                type |= Helpers.PathContainsDirectory(path, "patch") ? IndexType.Patch : IndexType.Data;
            if (IndexFooter.OffsetBytes == 0)
                type |= IndexType.Loose;
            if (IndexFooter.OffsetBytes > 4)
                type |= IndexType.Group;

            return type;
        }

        #endregion
    }
}

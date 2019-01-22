using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;
using TACT.Net.SystemFiles.Shared;

namespace TACT.Net.Archives
{
    public class ArchiveContainer : SystemFileBase
    {
        public IEnumerable<ArchiveIndex> DataIndices
        {
            get => _archiveIndices.Where(x => (x.Type & IndexType.Data) == IndexType.Data);
        }
        public IEnumerable<ArchiveIndex> LooseIndices
        {
            get => _archiveIndices.Where(x => (x.Type & IndexType.Loose) == IndexType.Loose);
        }
        public IEnumerable<ArchiveIndex> PatchIndices
        {
            get => _archiveIndices.Where(x => (x.Type & IndexType.Patch) == IndexType.Patch);
        }

        private const long ArchiveDataSize = 256000000;
        private List<ArchiveIndex> _archiveIndices;
        /// <summary>
        /// Files enqueued to be added to a new archive
        /// </summary>
        private readonly SortedDictionary<MD5Hash, CASRecord> _fileQueue;
        private string _sourceDirectory;

        #region Constructors

        public ArchiveContainer(TACT container = null) : base(container)
        {
            _archiveIndices = new List<ArchiveIndex>();
            _fileQueue = new SortedDictionary<MD5Hash, CASRecord>(new HashComparer());
        }

        #endregion

        #region Methods

        /// <summary>
        /// Parses all Index files in the provided directory
        /// </summary>
        /// <param name="directory"></param>
        public void Open(string directory)
        {
            _sourceDirectory = directory;

            var indicies = Directory.EnumerateFiles(directory, "*.index", SearchOption.AllDirectories);
            foreach (var index in indicies)
                _archiveIndices.Add(new ArchiveIndex(index));
        }

        /// <summary>
        /// Enqueues a CASRecord for storing archiving
        /// </summary>
        /// <param name="record"></param>
        public void Enqueue(CASRecord record)
        {
            _fileQueue.TryAdd(record.EKey, record);
        }

        /// <summary>
        /// Enqueues multiple CASRecords for archiving
        /// </summary>
        /// <param name="records"></param>
        public void Enqueue(IEnumerable<CASRecord> records)
        {
            foreach (var record in records)
                Enqueue(record);
        }

        /// <summary>
        /// Opens a file stream to a file stored in the archives
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public BlockTableStreamReader OpenFile(MD5Hash hash)
        {
            foreach (var index in _archiveIndices)
            {
                if (!index.IsGroup && index.TryGet(hash, out var archiveIndexEntry))
                {
                    // blob file location
                    string blobpath = Helpers.GetCDNPath(index.Checksum.ToString(), "data", _sourceDirectory);

                    if (File.Exists(blobpath))
                    {
                        // open a shared stream, set the offset and return a new BLTE reader
                        var fs = new FileStream(blobpath, FileMode.Open, FileAccess.Read, FileShare.Read)
                        {
                            Position = archiveIndexEntry.Offset
                        };
                        return new BlockTableStreamReader(fs);
                    }

                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Updates modified indicies and writes enqueued files to archives
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="dispose">Delete old files</param>
        public void Save(string directory, bool dispose = false)
        {
            // save altered Data archive indicies
            foreach (var archiveIndex in DataIndices)
            {
                if (!archiveIndex.IsGroup)
                {
                    string prevBlob = Helpers.GetCDNPath(archiveIndex.Checksum.ToString(), "data", _sourceDirectory);
                    archiveIndex.Write(directory, Container);
                    archiveIndex.WriteBlob(directory, prevBlob);

                    // remove old archives
                    if (dispose)
                    {
                        Helpers.Delete(prevBlob);
                        Helpers.Delete(prevBlob + ".index");
                    }
                }
            }

            // create any new archive indicies
            var partitions = EnumerablePartitioner.ConcreteBatch(_fileQueue.Values, ArchiveDataSize, (x) => x.EBlock.CompressedSize);
            foreach (var entries in partitions)
            {
                ArchiveIndex archiveIndex = new ArchiveIndex();
                archiveIndex.Add(entries);
                archiveIndex.Write(directory, Container);
                archiveIndex.WriteBlob(directory);
            }

            // reload indicies
            _archiveIndices.Clear();
            Open(directory);
        }

        /// <summary>
        /// Returns an ArchiveIndexEntry from the collection if it exists
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="archiveIndexEntry"></param>
        /// <returns></returns>
        public bool TryGet(MD5Hash hash, out ArchiveIndexEntry archiveIndexEntry)
        {
            archiveIndexEntry = null;
            foreach (var index in _archiveIndices)
                if (!index.IsGroup && index.TryGet(hash, out archiveIndexEntry))
                    return true;

            return false;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Indices
{
    public class IndexContainer : ISystemFile
    {
        public IEnumerable<IndexFile> DataIndices => _indices.Where(x => (x.Type & IndexType.Data) == IndexType.Data);
        public IEnumerable<IndexFile> LooseIndices => _indices.Where(x => (x.Type & IndexType.Loose) == IndexType.Loose);
        public IEnumerable<IndexFile> PatchIndices => _indices.Where(x => (x.Type & IndexType.Patch) == IndexType.Patch);

        public MD5Hash Checksum { get; }

        private const long ArchiveDataSize = 256000000;

        /// <summary>
        /// Files enqueued to be added to a new archive
        /// </summary>
        private readonly SortedList<MD5Hash, CASRecord> _fileQueue;

        private ConcurrentSet<IndexFile> _indices;
        private string _sourceDirectory;
        private bool _useParallelism = false;

        #region Constructors

        public IndexContainer()
        {
            _indices = new ConcurrentSet<IndexFile>();
            _fileQueue = new SortedList<MD5Hash, CASRecord>(new MD5HashComparer());
        }

        #endregion

        #region Methods

        /// <summary>
        /// Parses all Index files in the provided directory
        /// </summary>
        /// <param name="directory"></param>
        public void Open(string directory, bool useParallelism = false)
        {
            _sourceDirectory = directory;
            _useParallelism = useParallelism;

            if (!Directory.Exists(directory))
                throw new ArgumentException("Directory not found", paramName: nameof(directory));

            var indices = Directory.EnumerateFiles(directory, "*.index", SearchOption.AllDirectories);

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = useParallelism ? -1 : 1 };
            Parallel.ForEach(indices, options, index => _indices.Add(new IndexFile(index)));
        }

        /// <summary>
        /// Updates modified data indices and writes enqueued files to archives
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="dispose">Delete old files</param>
        /// <param name="configContainer"></param>
        public void Save(string directory, Configs.ConfigContainer configContainer = null)
        {
            // save altered Data archive indices
            foreach (var index in DataIndices)
            {
                bool requiresSave = index.RequiresSave || !directory.Equals(_sourceDirectory, StringComparison.OrdinalIgnoreCase);

                if (!index.IsGroupIndex && requiresSave)
                {
                    string prevBlob = Helpers.GetCDNPath(index.Checksum.ToString(), "data", _sourceDirectory);
                    index.Write(directory, configContainer);
                    index.WriteBlob(directory, prevBlob);
                }
            }

            // create any new archive indices
            var partitions = EnumerablePartitioner.ConcreteBatch(_fileQueue.Values, ArchiveDataSize, (x) => x.EBlock.CompressedSize);
            foreach (var entries in partitions)
            {
                IndexFile index = new IndexFile(IndexType.Data);
                index.Add(entries);
                index.Write(directory, configContainer);
                index.WriteBlob(directory);
            }

            // reload indices
            _indices.Clear();
            Open(directory, _useParallelism);
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
        /// Opens a stream to a data file stored in the archives
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public Stream OpenFile(MD5Hash ekey)
        {
            string path = GetIndexEntryAndPath(DataIndices, "data", ekey, out var indexEntry);
            if (path == null)
                return null;

            // open a shared stream, set the offset and return a new BLTE reader
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            {
                Position = indexEntry.Offset
            };
            return new BlockTableStreamReader(fs);
        }

        /// <summary>
        /// Opens a stream to a patch entry stored in the archives
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public Stream OpenPatch(MD5Hash ekey)
        {
            string path = GetIndexEntryAndPath(PatchIndices, "patch", ekey, out var indexEntry);
            if (path == null)
                return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Position = indexEntry.Offset;

                // segment this entry only
                byte[] buffer = new byte[indexEntry.CompressedSize];
                fs.Read(buffer);
                return new MemoryStream(buffer);
            }
        }

        /// <summary>
        /// Returns an IndexEntry from the collection if it exists
        /// </summary>
        /// <param name="ekey"></param>
        /// <param name="indexEntry"></param>
        /// <returns></returns>
        public bool TryGet(MD5Hash ekey, out IndexEntry indexEntry)
        {
            indexEntry = null;
            foreach (var index in _indices)
                if (!index.IsGroupIndex && index.TryGet(ekey, out indexEntry))
                    return true;

            return false;
        }

        /// <summary>
        /// Removes an IndexFile from the collection
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool Remove(IndexFile index) => _indices.Remove(index);

        #endregion

        #region Helpers

        /// <summary>
        /// Generic method to find a hash inside a collection of IndexFiles
        /// </summary>
        /// <param name="indices"></param>
        /// <param name="path"></param>
        /// <param name="hash"></param>
        /// <param name="indexEntry">The IndexFile containing the hash</param>
        /// <returns>Returns the path to the blob file</returns>
        private string GetIndexEntryAndPath(IEnumerable<IndexFile> indices, string path, MD5Hash hash, out IndexEntry indexEntry)
        {
            indexEntry = null;

            foreach (var index in indices)
            {
                if (!index.IsGroupIndex && index.TryGet(hash, out indexEntry))
                {
                    // blob file location
                    string blobpath = Helpers.GetCDNPath(index.Checksum.ToString(), path, _sourceDirectory);
                    if (File.Exists(blobpath))
                        return blobpath;

                    return null;
                }
            }

            return null;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Network;

namespace TACT.Net.Indices
{
    public class IndexContainer
    {
        public const long ArchiveDataSize = 256000000;

        public bool IsRemote { get; private set; }
        public IEnumerable<IndexFile> DataIndices => _indices.Where(x => x.IsDataIndex);
        public IEnumerable<IndexFile> LooseIndices => _indices.Where(x => x.IsLooseIndex);
        public IEnumerable<IndexFile> PatchIndices => _indices.Where(x => x.IsPatchIndex);
        /// <summary>
        /// Files enqueued to be added to a new archive
        /// </summary>
        public readonly SortedList<MD5Hash, CASRecord> QueuedEntries;

        private ConcurrentSet<IndexFile> _indices;
        private string _sourceDirectory;
        private bool _useParallelism = false;
        private CDNClient _client;

        #region Constructors

        public IndexContainer()
        {
            _indices = new ConcurrentSet<IndexFile>();
            QueuedEntries = new SortedList<MD5Hash, CASRecord>(new MD5HashComparer());

            ThreadPool.GetMinThreads(out int workers, out _);
            if (workers != 100)
                ThreadPool.SetMinThreads(100, 100);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Parses all Index files in the provided directory.
        /// <para></para>
        /// Providing the ConfigContainer will filter indices to just those used in the CDNConfig.
        /// </summary>
        /// <param name="directory">Directory the archives are located</param>
        /// <param name="configContainer">The Configs for the repo</param>
        /// <param name="useParallelism">Enables parallel processing</param>
        public void Open(string directory, Configs.ConfigContainer configContainer = null, bool useParallelism = false)
        {
            IsRemote = false;

            _sourceDirectory = directory;
            _useParallelism = useParallelism;

            if (!Directory.Exists(directory))
                throw new ArgumentException("Directory not found", paramName: nameof(directory));

            var indices = Directory.EnumerateFiles(directory, "*.index", SearchOption.AllDirectories);

            // filter the indices to just this version's
            if(configContainer != null)
            {
                var applicableIndicies = GetRequiredIndices(configContainer);
                indices = indices.Where(x => applicableIndicies.Contains(Path.GetFileNameWithoutExtension(x)));
            }

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = useParallelism ? -1 : 1 };
            Parallel.ForEach(indices, options, index => _indices.Add(new IndexFile(index)));
        }

        /// <summary>
        /// Parses all Index files from a remote CDN
        /// </summary>
        /// <param name="manifestContainer"></param>
        /// <param name="useParallelism"></param>
        public void OpenRemote(Configs.ConfigContainer configContainer, Configs.ManifestContainer manifestContainer, bool useParallelism = false)
        {
            IsRemote = true;

            _useParallelism = useParallelism;
            _client = new CDNClient(manifestContainer);

            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = useParallelism ? -1 : 1 };

            // stream data archive indicies
            var archives = configContainer.CDNConfig.GetValues("archives");
            if (archives != null && archives.Count > 0)
                Parallel.ForEach(archives, options, index => _indices.Add(new IndexFile(_client, index, IndexType.Data)));

            // stream patch archive indices
            var patcharchives = configContainer.CDNConfig.GetValues("patch-archives");
            if (patcharchives != null && patcharchives.Count > 0)
                Parallel.ForEach(patcharchives, options, index => _indices.Add(new IndexFile(_client, index, IndexType.Patch)));

            // stream loose file index
            var fileIndex = configContainer.CDNConfig.GetValue("file-index");
            if (fileIndex != null)
                _indices.Add(new IndexFile(_client, fileIndex, IndexType.Loose | IndexType.Data));

            // stream loose patch file index
            var patchIndex = configContainer.CDNConfig.GetValue("patch-file-index");
            if (patchIndex != null)
                _indices.Add(new IndexFile(_client, patchIndex, IndexType.Loose | IndexType.Patch));
        }

        /// <summary>
        /// Downloads all Index and Archive files from a remote CDN
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="configContainer"></param>
        public void DownloadRemote(string directory, Configs.ConfigContainer configContainer, Configs.ManifestContainer manifestContainer)
        {
            _client = new CDNClient(manifestContainer);

            var queuedDownloader = new QueuedDownloader(directory, _client);

            // download data archives
            var archives = configContainer.CDNConfig.GetValues("archives");
            if (archives != null && archives.Count > 0)
            {
                queuedDownloader.Enqueue(archives);
                queuedDownloader.Enqueue(archives, (x) => x + ".index");
                queuedDownloader.Download("data");
            }

            // download patch archives
            var patcharchives = configContainer.CDNConfig.GetValues("patch-archives");
            if (patcharchives != null && patcharchives.Count > 0)
            {
                queuedDownloader.Enqueue(patcharchives);
                queuedDownloader.Enqueue(patcharchives, (x) => x + ".index");
                queuedDownloader.Download("patch");
            }

            // download loose file index
            var fileIndex = configContainer.CDNConfig.GetValue("file-index");
            if (fileIndex != null)
            {
                string url = Helpers.GetCDNUrl(fileIndex, "data");
                string path = Helpers.GetCDNPath(fileIndex, "data", directory, true);
                _client.DownloadFile(url, path).Wait();

                // download loose files
                var index = new IndexFile(path);
                queuedDownloader.Enqueue(index.Entries, (x) => x.Key.ToString());
                queuedDownloader.Download("data");
            }

            // download loose patch file index
            var patchIndex = configContainer.CDNConfig.GetValue("patch-file-index");
            if (patchIndex != null)
            {
                string url = Helpers.GetCDNUrl(patchIndex, "patch");
                string path = Helpers.GetCDNPath(patchIndex, "patch", directory, true);
                _client.DownloadFile(url, path).Wait();

                // download loose patches
                var index = new IndexFile(path);
                queuedDownloader.Enqueue(index.Entries, (x) => x.Key.ToString());
                queuedDownloader.Download("patch");
            }

            Open(directory);
        }


        /// <summary>
        /// Updates modified data indices and writes enqueued files to archives
        /// <para>Note: IndexFile saving is limited to new entries if the container was opened remotely</para>
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="dispose">Delete old files</param>
        /// <param name="configContainer"></param>
        public void Save(string directory, Configs.ConfigContainer configContainer = null)
        {
            bool sameDirectory = directory.Equals(_sourceDirectory, StringComparison.OrdinalIgnoreCase);

            // save altered Data archive indices
            if (!IsRemote)
            {
                foreach (var index in DataIndices)
                {
                    if (index.IsGroupIndex)
                        continue;

                    if (index.RequiresSave)
                    {
                        // save the index file and blob
                        string prevBlob = Helpers.GetCDNPath(index.Checksum.ToString(), "data", _sourceDirectory);
                        index.Write(directory, configContainer);
                        index.WriteBlob(directory, prevBlob);
                    }
                    else if (!sameDirectory)
                    {
                        // copy the index file and blob
                        string oldblob = Helpers.GetCDNPath(index.Checksum.ToString(), "data", _sourceDirectory);
                        string newblob = Helpers.GetCDNPath(index.Checksum.ToString(), "data", directory, true);
                        File.Copy(oldblob, newblob);
                        File.Copy(oldblob + ".index", newblob + ".index");
                    }
                }
            }

            // create any new archive indices
            var partitions = EnumerablePartitioner.ConcreteBatch(QueuedEntries.Values, ArchiveDataSize, (x) => x.EBlock.CompressedSize);
            foreach (var entries in partitions)
            {
                IndexFile index = new IndexFile(IndexType.Data);
                index.Add(entries);
                index.Write(directory, configContainer);
                index.WriteBlob(directory);
            }

            // reload indices
            _indices.Clear();
            Open(directory, useParallelism: _useParallelism);
        }


        /// <summary>
        /// Enqueues a CASRecord to be written to the indicies and archives
        /// </summary>
        /// <param name="record"></param>
        public void Enqueue(CASRecord record)
        {
            lock (QueuedEntries)
                QueuedEntries[record.EKey] = record;
        }
        /// <summary>
        /// Dequeues a CASRecord from being written to the indicies and archives
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public bool Dequeue(CASRecord record)
        {
            lock (QueuedEntries)
                return QueuedEntries.Remove(record.EKey);
        }
        /// <summary>
        /// Dequeues an entry from being written to the indicies and archives
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public bool Dequeue(MD5Hash ekey)
        {
            lock (QueuedEntries)
                return QueuedEntries.Remove(ekey);
        }


        /// <summary>
        /// Opens a stream to a data file stored in the archives
        /// <para>Note: If IsRemote is true the entry will be streamed from a CDN</para>
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public Stream OpenFile(MD5Hash ekey)
        {
            if (!IsRemote)
                return OpenLocalFile(IndexType.Data, ekey);
            else
                return OpenRemoteFile(IndexType.Data, ekey);
        }
        /// <summary>
        /// Opens a stream to a patch entry stored in the archives
        /// <para>Note: If IsRemote is true the entry will be streamed from a CDN</para>
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public Stream OpenPatch(MD5Hash ekey)
        {
            if (!IsRemote)
                return OpenLocalFile(IndexType.Patch, ekey);
            else
                return OpenRemoteFile(IndexType.Patch, ekey);
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
        /// <summary>
        /// Removes an IndexEntry from the collection
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public bool Remove(MD5Hash ekey)
        {
            foreach (var index in _indices)
                if (!index.IsGroupIndex && index.Contains(ekey))
                    return index.Remove(ekey);

            return false;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns a list of index filenames used by the specific CDNConfig
        /// </summary>
        /// <param name="configContainer"></param>
        /// <returns></returns>
        private HashSet<string> GetRequiredIndices(Configs.ConfigContainer configContainer)
        {
            var indices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // data archives
            var archives = configContainer.CDNConfig.GetValues("archives");
            if (archives != null)
                indices.UnionWith(archives);

            // patch archives
            var patcharchives = configContainer.CDNConfig.GetValues("patch-archives");
            if (patcharchives != null)
                indices.UnionWith(patcharchives);

            // loose file index
            var fileIndex = configContainer.CDNConfig.GetValue("file-index");
            if (fileIndex != null)
                indices.Add(fileIndex);

            // loose patch file index
            var patchIndex = configContainer.CDNConfig.GetValue("patch-file-index");
            if (patchIndex != null)
                indices.Add(patchIndex);

            return indices;
        }
        
        /// <summary>
        /// Generic method to find a hash inside a collection of IndexFiles
        /// </summary>
        /// <param name="indices"></param>
        /// <param name="path"></param>
        /// <param name="ekey"></param>
        /// <param name="indexEntry">The IndexFile containing the hash</param>
        /// <returns>Returns the path to the blob file</returns>
        private IndexFile GetIndexFileAndEntry(IndexType type, MD5Hash ekey, out IndexEntry indexEntry)
        {
            indexEntry = null;

            var indices = type == IndexType.Data ? DataIndices : PatchIndices;
            foreach (var index in indices)
                if (!index.IsGroupIndex && index.TryGet(ekey, out indexEntry))
                    return index;

            return null;
        }

        /// <summary>
        /// Opens a stream to an entry in the local data archives
        /// </summary>
        /// <param name="type"></param>
        /// <param name="ekey"></param>
        /// <returns></returns>
        private Stream OpenLocalFile(IndexType type, MD5Hash ekey)
        {
            var index = GetIndexFileAndEntry(type, ekey, out var indexEntry);
            if (index == null || indexEntry == null)
                return null;

            string archive = index.IsLooseIndex ? ekey.ToString() : index.Checksum.ToString();
            string filepath = Helpers.GetCDNPath(archive, type.ToString().ToLowerInvariant(), _sourceDirectory);

            if (!File.Exists(filepath))
                return null;

            // open a shared stream and seek to the entry's offset
            var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read)
            {
                Position = indexEntry.Offset
            };

            switch (type)
            {
                case IndexType.Data:
                    {
                        // return a new BLTE reader
                        return new BlockTableStreamReader(stream);
                    }
                case IndexType.Patch:
                    {
                        // segment this entry only
                        var ms = new MemoryStream((int)indexEntry.CompressedSize);
                        stream.PartialCopyTo(ms, (int)indexEntry.CompressedSize);
                        stream.Dispose();
                        ms.Position = 0;
                        return ms;
                    }
                default:
                    stream.Dispose();
                    return null;
            }
        }

        /// <summary>
        /// Opens a stream to an entry in a remote CDN's data archives
        /// </summary>
        /// <param name="type"></param>
        /// <param name="ekey"></param>
        /// <returns></returns>
        private Stream OpenRemoteFile(IndexType type, MD5Hash ekey)
        {
            var index = GetIndexFileAndEntry(type, ekey, out var indexEntry);
            if (index == null || indexEntry == null)
                return null;

            string archive = index.IsLooseIndex ? ekey.ToString() : index.Checksum.ToString();
            string url = Helpers.GetCDNUrl(archive, type.ToString().ToLowerInvariant());

            var stream = _client.OpenStream(url, indexEntry.Offset, indexEntry.Offset + (long)indexEntry.CompressedSize - 1).Result;
            if (stream == null)
                return null;

            switch (type)
            {
                case IndexType.Data:
                    return new BlockTableStreamReader(stream);
                case IndexType.Patch:
                    return stream;
                default:
                    stream.Dispose();
                    return null;
            }
        }

        [Obsolete]
        /// <summary>
        /// Creates a fake Data Index Group and stores the computed checksum
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="configContainer"></param>
        private void GenerateIndexGroup(string directory, Configs.ConfigContainer configContainer)
        {
            if (configContainer == null)
                return;

            // get the list of data archives
            var archives = configContainer.CDNConfig.GetValues("archives");
            archives.Sort(new MD5HashComparer());

            // populate the archive indicies and 
            var temp = new List<IndexEntry>(DataIndices.Sum(x => x.Entries.Count()));
            foreach (var index in DataIndices)
            {
                if (index.IsLooseIndex)
                    continue;

                ushort archiveIndex = (ushort)archives.IndexOf(index.Checksum.ToString());
                foreach (var e in index.Entries)
                {
                    e.IndexOrdinal = archiveIndex;
                    temp.Add(e);
                }
            }

            // sort
            var comparer = new MD5HashComparer();
            temp.Sort((x, y) => comparer.Compare(x.Key, y.Key));

            // create a new IndexFile, add all entries and store the checksum in the CDN config
            var indexFile = new IndexFile(IndexType.Data | IndexType.Group);
            indexFile.LoadIndicies(temp);

            indexFile.Write(directory, configContainer);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TACT.Net.FileLookup
{
    /// <summary>
    /// Two column (id,name) CSV lookup
    /// <para>E.g. https://bnet.marlam.in/listfile.php?type=csv</para>
    /// </summary>
    public class CSVFileLookup : IFileLookup, IDisposable
    {
        private Queue<uint> _unusedIds;
        private SemaphoreSlim _sync;
        private uint _curMaxId;

        private readonly string _filename;
        private readonly char _seperator;
        private readonly uint _minFileId;
        private readonly Dictionary<string, uint> _fileLookup;

        #region Constructors

        /// <param name="filepath"></param>
        /// <param name="seperator"></param>
        /// <param name="minimumFileId">Minimum file ID for new files</param>
        public CSVFileLookup(string filepath, char seperator = ';', uint minimumFileId = 0)
        {
            _filename = filepath;
            _seperator = seperator;
            _minFileId = minimumFileId;

            _fileLookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            _sync = new SemaphoreSlim(1, 1);
        }

        #endregion

        #region IO

        /// <summary>
        /// Loads the CSV lookup and optionally prioritises unused ids
        /// </summary>
        /// <param name="fillIdGaps"></param>
        public void Open(bool fillIdGaps)
        {
            using (var sr = File.OpenText(_filename))
            {
                string line;
                int seperatorIdx;

                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    seperatorIdx = line.IndexOf(_seperator);

                    if (seperatorIdx > -1 && uint.TryParse(line.Substring(0, seperatorIdx), out uint id))
                        _fileLookup[line.Substring(seperatorIdx)] = id;
                }
            }

            // store the current max id/wanted max id
            _curMaxId = Math.Max(_fileLookup.Values.Max(), _minFileId);

            if (fillIdGaps)
                LoadUnusedIDs();
        }

        public async Task Sync()
        {
            await _sync.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var sw = new StreamWriter(_filename))
                    foreach (var lookup in _fileLookup)
                        await sw.WriteLineAsync(lookup.Value + _seperator + lookup.Key).ConfigureAwait(false);
            }
            finally
            {
                _sync.Release();
            }
        }

        public void Close()
        {
            Sync().RunSynchronously();
        }

        #endregion

        #region Methods

        public uint GetOrCreateFileId(string filename)
        {
            // attempt to get the fileid
            if (!_fileLookup.TryGetValue(filename, out uint id))
            {
                // attempt to load an unusedid
                if (!_unusedIds.TryDequeue(out id))
                    id = ++_curMaxId; // used the next highest id

                _fileLookup.Add(filename, id);
            }

            return id;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Generates a range of IDs that aren't used
        /// </summary>
        private void LoadUnusedIDs()
        {
            uint min = Math.Max(_fileLookup.Values.Min(), _minFileId);

            IEnumerable<uint> GetRange()
            {
                for (; min < _curMaxId; min++)
                    yield return min;
            }

            var idRange = GetRange().Except(_fileLookup.Values);
            _unusedIds = new Queue<uint>(idRange);
        }

        public void Dispose()
        {
            ((IDisposable)_sync).Dispose();
        }

        #endregion
    }
}

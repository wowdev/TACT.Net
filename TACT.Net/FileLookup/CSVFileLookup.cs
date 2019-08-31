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
        public bool IsLoaded { get; private set; }

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
        public void Open()
        {
            using (var sr = File.OpenText(_filename))
            {
                string line;
                int seperatorIdx;

                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    seperatorIdx = line.IndexOf(_seperator);

                    if (seperatorIdx > -1)
                        if (uint.TryParse(line.Substring(0, seperatorIdx), out uint id))
                            _fileLookup[line.Substring(seperatorIdx)] = id;
                }
            }

            // store the current max id/wanted max id
            _curMaxId = Math.Max(_fileLookup.Values.Max(), _minFileId);

            IsLoaded = true;
        }

        /// <summary>
        /// Asynchronously saves the FileIdMap as a CSV
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Synschronously saves the FileIdMap as a CSV
        /// </summary>
        public void Close()
        {
            Sync().RunSynchronously();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Attempts to return a FileId. If the filename is unused a new one is generated
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public uint GetOrCreateFileId(string filename)
        {
            // attempt to get the fileid
            if (!_fileLookup.TryGetValue(filename, out uint id))
            {
                id = ++_curMaxId; // used the next highest id
                _fileLookup.Add(filename, id);
            }

            return id;
        }

        #endregion

        #region Helpers

        public void Dispose()
        {
            ((IDisposable)_sync).Dispose();
        }

        #endregion
    }
}

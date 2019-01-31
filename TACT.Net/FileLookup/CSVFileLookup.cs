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
    public class CSVFileLookup : IFileLookup
    {
        private Queue<uint> _unusedIds;
        private SemaphoreSlim _sync;

        private readonly string _filename;
        private readonly Dictionary<string, uint> _fileLookup;

        public CSVFileLookup(string filename)
        {
            _filename = filename;
            _fileLookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            _sync = new SemaphoreSlim(0, 1);
        }


        public void Open()
        {
            using (var sr = File.OpenText(_filename))
            {
                string line;
                int seperatorIdx;

                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    seperatorIdx = line.IndexOf(';');

                    if (seperatorIdx > -1 && uint.TryParse(line.Substring(0, seperatorIdx), out uint id))
                        _fileLookup[line.Substring(seperatorIdx)] = id;
                }
            }

            LoadUnusedIDs();
        }

        public async Task Sync()
        {
            await _sync.WaitAsync();
            try
            {
                using (var sw = new StreamWriter(_filename))
                    foreach (var lookup in _fileLookup)
                        await sw.WriteLineAsync(lookup.Value + ";" + lookup.Key);
            }
            finally
            {
                _sync.Release();
            }
        }

        public void Close()
        {
            Task.Run(Sync);
        }


        public uint GetOrCreateFileId(string filename)
        {
            if (!_fileLookup.TryGetValue(filename, out uint id))
            {
                id = _unusedIds.Count > 0 ? _unusedIds.Dequeue() : (uint)_fileLookup.Count;
                _fileLookup.Add(filename, id);
            }

            return id;
        }

        /// <summary>
        /// Generates a range of IDs that aren't used
        /// </summary>
        private void LoadUnusedIDs()
        {
            var idRange = Enumerable.Range((int)_fileLookup.Values.Min(), (int)_fileLookup.Values.Max())
                                    .Select(x => (uint)x)
                                    .Except(_fileLookup.Values);

            _unusedIds = new Queue<uint>(idRange);
        }
    }
}

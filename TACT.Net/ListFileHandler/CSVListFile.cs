using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace TACT.Net.ListFileHandler
{
    /// <summary>
    /// Two column CSV ListFile lookup
    /// </summary>
    public class CSVListFile : IListFile
    {
        private Queue<uint> _unusedIds;
        private readonly string _filename;
        private readonly int _idIndex;

        private readonly Dictionary<string, uint> _fileLookup;
        private Func<string, int> IndexOf;

        public CSVListFile(string filename, int idIndex = 0)
        {
            _filename = filename;
            _idIndex = idIndex;

            // declare indexof function
            if(_idIndex == 0)
                IndexOf = (string s) => s.IndexOf(',');
            else
                IndexOf = (string s) => s.LastIndexOf(',');

            _fileLookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        }

        public void Open()
        {
            int nameIndex = _idIndex ^ 1;
            string[] parts = new string[2];

            using (var sr = File.OpenText(_filename))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();

                    int commaIndex = IndexOf(line);
                    parts[0] = line.Substring(0, commaIndex);
                    parts[1] = line.Substring(commaIndex);

                    _fileLookup[parts[nameIndex]] = uint.Parse(parts[_idIndex]);
                }
            }

            LoadUnusedIDs();
        }

        public void Close()
        {
            throw new NotImplementedException();
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

        private void LoadUnusedIDs()
        {
            var idRange = Enumerable.Range((int)_fileLookup.Values.Min(), (int)_fileLookup.Values.Max()).Select(x => (uint)x);

            HashSet<uint> allIdsInRange = new HashSet<uint>(idRange);
            allIdsInRange.ExceptWith(_fileLookup.Values);

            _unusedIds = new Queue<uint>(allIdsInRange);
        }
    }
}

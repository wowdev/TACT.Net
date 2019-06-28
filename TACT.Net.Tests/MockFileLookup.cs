using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TACT.Net.FileLookup;

namespace TACT.Net.Tests
{
    class MockFileLookup : IFileLookup
    {
        public bool IsLoaded => true;

        private readonly Dictionary<string, uint> FileLookup;
        private uint CurrentId = 0;

        public MockFileLookup()
        {
            FileLookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        }

        public uint this[string name]
        {
            get => FileLookup[name];
            set => FileLookup[name] = value;
        }

        public uint GetOrCreateFileId(string filename)
        {
            if (!FileLookup.TryGetValue(filename, out uint id))
            {
                FileLookup.Add(filename, ++CurrentId);
                id = CurrentId;
            }

            return id;
        }

        public void Open() { }

        public Task Sync() => throw new NotImplementedException();

        public void Close() { }
    }
}

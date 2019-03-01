using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TACT.Net.FileLookup;

namespace TACT.Net.Tests
{
    class MockFileLookup : IFileLookup
    {
        private readonly Dictionary<string, uint> FileLookup;
        private uint CurrentId = 0;

        public MockFileLookup()
        {
            FileLookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        }

        public uint GetOrCreateFileId(string filename)
        {
            if (!FileLookup.TryGetValue(filename, out uint id))
                id = ++CurrentId;

            return id;
        }

        public void Open(bool fillIdGaps) { }

        public Task Sync() => throw new NotImplementedException();

        public void Close() { }
    }
}

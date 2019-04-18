using System.IO;
using TACT.Net.Cryptography;

namespace TACT.Net.Root
{
    public sealed class RootRecord
    {
        public MD5Hash CKey { get; set; }
        public ulong NameHash { get; set; }
        public uint FileId { get; set; }
        public uint FileIdDelta { get; set; }
    }
}

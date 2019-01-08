using System.IO;
using TACT.Net.Common.Cryptography;

namespace TACT.Net.Root
{
    public sealed class RootRecord
    {
        public MD5Hash CKey;
        public ulong NameHash;
        public uint FileId;
        public uint FileIdDelta;

        #region IO
        public void Read(BinaryReader br)
        {
            CKey = new MD5Hash(br.ReadBytes(16));
            NameHash = br.ReadUInt64();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(CKey.Value);
            bw.Write(NameHash);
        }
        #endregion
    }
}

using System.IO;
using TACT.Net.Cryptography;

namespace TACT.Net.Root
{
    public sealed class RootRecord
    {
        public MD5Hash CKey;
        public ulong NameHash;
        public uint FileId;
        public uint FileIdDelta;

        #region IO

        public void Read(BinaryReader br, RootBlock rootBlock, uint version)
        {
            CKey = new MD5Hash(br.ReadBytes(16));
            if (version > 1 && rootBlock.HasNameHash)
                NameHash = br.ReadUInt64();
        }

        public void Write(BinaryWriter bw, RootBlock rootBlock, uint version)
        {
            bw.Write(CKey.Value);
            if (version > 1 && rootBlock.HasNameHash)
                bw.Write(NameHash);
        }

        #endregion
    }
}

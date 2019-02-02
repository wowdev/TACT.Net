using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Patch
{
    public class PatchHeader
    {
        public byte[] Magic;
        public byte Version;
        public byte FileKeySize;
        public byte unk_05;
        public byte PatchEKeySize;
        public byte PageSize;
        public ushort BlockCount;
        public byte unk_0A;

        #region Encoding File Specification
        public MD5Hash EncodingCKey;
        public MD5Hash EncodingEKey;
        public uint DecompressedSize;
        public uint CompressedSize;
        public string ESpecTable;
        #endregion

        #region IO

        public void Read(BinaryReader br)
        {
            Magic = br.ReadBytes(2);
            Version = br.ReadByte();
            FileKeySize = br.ReadByte();
            unk_05 = br.ReadByte();
            PatchEKeySize = br.ReadByte();
            PageSize = br.ReadByte();
            BlockCount = br.ReadUInt16BE();
            unk_0A = br.ReadByte();

            EncodingCKey = new MD5Hash(br.ReadBytes(16));
            EncodingEKey = new MD5Hash(br.ReadBytes(16));
            DecompressedSize = br.ReadUInt32BE();
            CompressedSize = br.ReadUInt32BE();

            byte ESpecTableSize = br.ReadByte();
            ESpecTable = System.Text.Encoding.ASCII.GetString(br.ReadBytes(ESpecTableSize));
        }

        #endregion
    }
}

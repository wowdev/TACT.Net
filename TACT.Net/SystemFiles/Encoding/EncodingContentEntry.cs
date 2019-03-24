using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Encoding
{
    public class EncodingContentEntry : EncodingEntryBase
    {
        /// <summary>
        /// Original file size
        /// </summary>
        public ulong DecompressedSize;
        /// <summary>
        /// Content key
        /// </summary>
        public MD5Hash CKey;
        /// <summary>
        /// Encoding key
        /// <para>Note: The implementation states this can be >1 however this has never been the case for WoW</para>
        /// </summary>
        public MD5Hash EKey;

        internal override MD5Hash Key => CKey;
        internal override int Size => 6 + CKey.Value.Length + EKey.Value.Length;

        #region IO

        public override bool Read(BinaryReader br, EncodingHeader header)
        {
            byte keyCount = br.ReadByte();
            if (keyCount == 0)
                return false;

            DecompressedSize = br.ReadUInt40BE();
            CKey = new MD5Hash(br.ReadBytes(header.CKeyHashSize));

            EKey = new MD5Hash(br.ReadBytes(header.EKeyHashSize));

            if (keyCount > 1)
                br.BaseStream.Position += (keyCount - 1) * header.EKeyHashSize;

            return true;
        }

        public override void Write(BinaryWriter bw, EncodingHeader header)
        {
            bw.Write((byte)1); // EKey count
            bw.WriteUInt40BE(DecompressedSize);
            bw.Write(CKey.Value);
            bw.Write(EKey.Value);
        }

        #endregion

        #region Helpers

        internal override void Validate()
        {
            if (EKey.IsEmpty)
                throw new InvalidDataException("Entry contains no EKey");
            if (CKey.IsEmpty)
                throw new InvalidDataException("Entry contains no CKey");

            base.Validate();
        }

        #endregion
    }
}

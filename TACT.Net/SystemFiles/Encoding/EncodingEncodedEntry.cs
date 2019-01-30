using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Encoding
{
    public class EncodingEncodedEntry : EncodingEntryBase
    {
        /// <summary>
        /// Encoding Key
        /// </summary>
        public MD5Hash EKey;
        /// <summary>
        /// Index into the ESpec stringtable
        /// </summary>
        public uint ESpecIndex;
        /// <summary>
        /// Encoded size
        /// </summary>
        public ulong CompressedSize;

        internal override MD5Hash Key => EKey;
        internal override int Size => EKey.Value.Length + 9;

        #region IO

        public override bool Read(BinaryReader br, EncodingHeader header)
        {
            EKey = new MD5Hash(br.ReadBytes(header.EKeyHashSize));
            if (EKey.IsEmpty)
                return false;

            ESpecIndex = br.ReadUInt32BE();
            CompressedSize = br.ReadUInt40BE();

            return true;
        }

        public override void Write(BinaryWriter bw, EncodingHeader header)
        {
            bw.Write(EKey.Value);
            bw.WriteUInt32BE(ESpecIndex);
            bw.WriteUInt40BE(CompressedSize);
        }

        #endregion
    }
}

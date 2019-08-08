using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Indices
{
    public sealed class IndexEntry
    {
        /// <summary>
        /// Encoding Key for data archives, Content Key for patch archives
        /// </summary>
        public MD5Hash Key;
        /// <summary>
        /// Compressed size of the file stored in the blob
        /// </summary>
        public ulong CompressedSize;
        /// <summary>
        /// Index into CDN Config's Archive list
        /// </summary>
        public ushort IndexOrdinal;
        /// <summary>
        /// Offset of the file data in the archive blob
        /// </summary>
        public uint Offset;

        #region IO

        public bool Read(BinaryReader br, IndexFooter footer)
        {
            Key = new MD5Hash(br.ReadBytes(footer.KeySize));
            if (Key.IsEmpty)
                return false;

            CompressedSize = br.ReadUIntBE(footer.CompressedSizeBytes);

            if (footer.OffsetBytes == 6)
                IndexOrdinal = br.ReadUInt16BE();
            if (footer.OffsetBytes >= 4)
                Offset = br.ReadUInt32BE();

            return true;
        }

        public void Write(BinaryWriter bw, IndexFooter footer)
        {
            bw.Write(Key.Value, 0, footer.KeySize);
            bw.WriteUIntBE(CompressedSize, footer.CompressedSizeBytes);

            if (footer.OffsetBytes == 6)
                bw.WriteUInt16BE(IndexOrdinal);
            if (footer.OffsetBytes >= 4)
                bw.WriteUInt32BE(Offset);
        }

        #endregion

        #region Helpers

        public IndexEntry Clone() => (IndexEntry)MemberwiseClone();

        #endregion
    }
}

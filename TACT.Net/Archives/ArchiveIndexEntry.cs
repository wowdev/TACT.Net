using System;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;

namespace TACT.Net.Archives
{
    [Serializable]
    public sealed class ArchiveIndexEntry
    {
        /// <summary>
        /// Encoding Key
        /// <para>Note: Content Key for patch archives</para>
        /// </summary>
        public MD5Hash EKey;
        /// <summary>
        /// Compressed size of the file stored in the blob
        /// </summary>
        public ulong CompressedSize;
        /// <summary>
        /// Index of the CDN Config Archive containing this entry
        /// <para>Archive Index Group only</para>
        /// </summary>
        public ushort ArchiveIndex;
        /// <summary>
        /// Offset of the file data in the archive blob
        /// </summary>
        public uint Offset;

        #region IO
        public void Read(BinaryReader br, ArchiveIndexFooter footer)
        {
            EKey = new MD5Hash(br.ReadBytes(footer.EKeySize));
            CompressedSize = br.ReadUIntBE(footer.CompressedSizeBytes);

            if (footer.OffsetBytes == 6)
                ArchiveIndex = br.ReadUInt16BE();
            if (footer.OffsetBytes >= 4)
                Offset = br.ReadUInt32BE();
        }

        public void Write(BinaryWriter bw, ArchiveIndexFooter footer)
        {
            bw.Write(EKey.Value, 0, footer.EKeySize);
            bw.WriteUIntBE(CompressedSize, footer.CompressedSizeBytes);

            if (footer.OffsetBytes == 6)
                bw.WriteUInt16BE(ArchiveIndex);
            if (footer.OffsetBytes >= 4)
                bw.WriteUInt32BE(Offset);
        }
        #endregion
    }
}

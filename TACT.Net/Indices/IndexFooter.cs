using System.IO;
using System.Security.Cryptography;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Indices
{
    public sealed class IndexFooter
    {
        /// <summary>
        /// Hash of the last page of entries
        /// </summary>
        public MD5Hash LastPageHash;
        /// <summary>
        /// Hash of the table of contents - EKey Lookup and Page Checksum
        /// </summary>
        public MD5Hash ContentsHash;
        public byte Version = 1;
        public byte Unk_11 = 0;
        public byte Unk_12 = 0;
        /// <summary>
        /// Entry Page Size
        /// </summary>
        public byte PageSizeKB = 4;
        /// <summary>
        /// Size of Entry Offset and Index
        /// <para>0 = loosefile, 4 = blob offset, 6 = blob offset and ordinal</para>
        /// </summary>
        public byte OffsetBytes = 4;
        /// <summary>
        /// Size of IndexEntry's CompressedSize field
        /// </summary>
        public byte CompressedSizeBytes = 4;
        public byte KeySize { get; private set; } = 16;
        public byte ChecksumSize { get; private set; } = 8;
        public uint EntryCount;
        /// <summary>
        /// Hash of Version to the EOF with an empty FooterChecksum
        /// </summary>
        public MD5Hash FooterChecksum;

        internal int Size => 12 + (ChecksumSize * 3);

        #region IO

        public void Read(BinaryReader br)
        {
            // TODO checksum size check
            br.BaseStream.Seek(-Size, SeekOrigin.End);

            LastPageHash = new MD5Hash(br.ReadBytes(8));
            ContentsHash = new MD5Hash(br.ReadBytes(8));
            Version = br.ReadByte();
            Unk_11 = br.ReadByte();
            Unk_12 = br.ReadByte();
            PageSizeKB = br.ReadByte();
            OffsetBytes = br.ReadByte();
            CompressedSizeBytes = br.ReadByte();
            KeySize = br.ReadByte();
            ChecksumSize = br.ReadByte();
            EntryCount = br.ReadUInt32();
            FooterChecksum = new MD5Hash(br.ReadBytes(8));
        }

        public void Write(BinaryWriter bw)
        {
            using (var md5 = MD5.Create())
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(Version);
                writer.Write(Unk_11);
                writer.Write(Unk_12);
                writer.Write(PageSizeKB);
                writer.Write(OffsetBytes);
                writer.Write(CompressedSizeBytes);
                writer.Write(KeySize);
                writer.Write(ChecksumSize);
                writer.Write(EntryCount);
                writer.Write(new byte[8]);

                // calculate checksum with placeholder
                FooterChecksum = ms.HashSlice(md5, 0, ms.Length, ChecksumSize);

                // override the placeholder and copy to the main stream
                ms.Position = ms.Length - 8;
                writer.Write(FooterChecksum.Value);

                ms.WriteTo(bw.BaseStream);
            }
        }

        #endregion
    }
}

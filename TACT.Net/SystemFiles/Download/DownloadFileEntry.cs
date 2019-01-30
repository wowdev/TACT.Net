using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Download
{
    public class DownloadFileEntry : IDownloadFileEntry
    {
        /// <summary>
        /// Encoding Key
        /// </summary>
        public MD5Hash EKey { get; set; }
        /// <summary>
        /// Encoded file size
        /// </summary>
        public ulong CompressedSize;
        /// <summary>
        /// Download priority after DownloadHeader priority is subtracted
        /// <para>0 = highest, 2 = lowest. -1 for InstallFile since BfA</para>
        /// </summary>
        public sbyte Priority;
        /// <summary>
        /// Internal checksum (NYI)
        /// </summary>
        public uint Checksum;
        /// <summary>
        /// Plugin flags (NYI)
        /// <para>[Plugin, Plugin Data]</para>
        /// </summary>
        public byte[] Flags;

        #region IO

        public void Read(BinaryReader br, DownloadHeader header)
        {
            EKey = new MD5Hash(br.ReadBytes(header.EKeySize));
            CompressedSize = br.ReadUInt40BE();
            Priority = (sbyte)(br.ReadSByte() - header.BasePriority);

            if (header.IncludeChecksum)
                Checksum = br.ReadUInt32BE();

            if (header.Version >= 2)
                Flags = br.ReadBytes(header.FlagSize);
        }

        public void Write(BinaryWriter bw, DownloadHeader header)
        {
            bw.Write(EKey.Value);
            bw.WriteUInt40BE(CompressedSize);

            bw.Write((sbyte)(Priority + header.BasePriority));

            if (header.IncludeChecksum)
                bw.WriteUInt32BE(Checksum);

            if (header.Version >= 2)
                bw.Write(Flags);
        }

        #endregion
    }
}

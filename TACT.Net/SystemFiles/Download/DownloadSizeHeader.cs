using System.IO;
using TACT.Net.Common;

namespace TACT.Net.Download
{
    public class DownloadSizeHeader
    {
        public byte[] Magic = new byte[] { 68, 83 };
        public byte Version = 1;
        public byte EKeySize = 9;
        public uint EntryCount;
        public ushort TagCount;
        /// <summary>
        /// Total size of all entries
        /// </summary>
        public ulong TotalSize;

        #region IO

        public void Read(BinaryReader br)
        {
            Magic = br.ReadBytes(2);
            Version = br.ReadByte();
            EKeySize = br.ReadByte();
            EntryCount = br.ReadUInt32BE();
            TagCount = br.ReadUInt16BE();
            TotalSize = br.ReadUInt40BE();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(EKeySize);
            bw.WriteUInt32BE(EntryCount);
            bw.WriteUInt16BE(TagCount);
            bw.WriteUInt40BE(TotalSize);
        }

        #endregion
    }
}

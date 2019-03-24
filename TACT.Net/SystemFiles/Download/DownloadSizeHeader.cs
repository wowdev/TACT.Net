using System.IO;
using TACT.Net.Common;

namespace TACT.Net.Download
{
    public class DownloadSizeHeader
    {
        public byte[] Magic { get; private set; } = new byte[] { 68, 83 };
        public byte Version { get; set; } = 1;
        public byte EKeySize { get; set; } = 9;
        public uint EntryCount { get; internal set; }
        public ushort TagCount { get; internal set; }
        /// <summary>
        /// Total size of all entries
        /// </summary>
        public ulong TotalSize { get; internal set; }

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

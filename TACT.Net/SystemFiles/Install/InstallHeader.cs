using System.IO;
using TACT.Net.Common;

namespace TACT.Net.Install
{
    public class InstallHeader
    {
        public byte[] Magic = new byte[] { 73, 78 };
        public byte Version = 1;
        public byte CKeySize { get; private set; } = 16;
        /// <summary>
        /// Number of tags
        /// </summary>
        public ushort TagCount;
        /// <summary>
        /// Number of InstallFileEntries
        /// </summary>
        public uint EntryCount;

        #region IO

        public void Read(BinaryReader br)
        {
            Magic = br.ReadBytes(2);
            Version = br.ReadByte();
            CKeySize = br.ReadByte();
            TagCount = br.ReadUInt16BE();
            EntryCount = br.ReadUInt32BE();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(CKeySize);
            bw.WriteUInt16BE(TagCount);
            bw.WriteUInt32BE(EntryCount);
        }

        #endregion
    }
}

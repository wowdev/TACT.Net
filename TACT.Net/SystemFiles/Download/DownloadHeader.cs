using System.IO;
using TACT.Net.Common;

namespace TACT.Net.Download
{
    public class DownloadHeader
    {
        public byte[] Magic { get; private set; } = new byte[] { 68, 76 };
        public byte Version { get; set; } = 1;
        public byte EKeySize { get; private set; } = 16;
        /// <summary>
        /// Determines the existance of the checksum in DownloadFileEntry
        /// <para>HACK do we know what this is?</para>
        /// </summary>
        internal bool IncludeChecksum = false;
        public uint EntryCount { get; internal set; }
        public ushort TagCount { get; internal set; }
        /// <summary>
        /// Size of DownloadFileEntry flags
        /// <para>Plugin = 1, Plugin Data = 2</para>
        /// </summary>
        public byte FlagSize { get; set; }
        /// <summary>
        /// Base Priority for entries
        /// <para>0 = highest, 2 = lowest; Subtracted from entry priority</para>
        /// </summary>
        public sbyte BasePriority { get; set; }
        public uint Unk_0D { get; private set; }

        #region IO

        public void Read(BinaryReader br)
        {
            Magic = br.ReadBytes(2);
            Version = br.ReadByte();
            EKeySize = br.ReadByte();
            IncludeChecksum = br.ReadBoolean();
            EntryCount = br.ReadUInt32BE();
            TagCount = br.ReadUInt16BE();

            if (Version >= 2)
            {
                FlagSize = br.ReadByte();

                if (Version >= 3)
                {
                    BasePriority = br.ReadSByte();
                    Unk_0D = br.ReadUInt24BE();
                }
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(EKeySize);
            bw.Write(IncludeChecksum);
            bw.WriteUInt32BE(EntryCount);
            bw.WriteUInt16BE(TagCount);

            if (Version >= 2)
            {
                bw.Write(FlagSize);

                if (Version >= 3)
                {
                    bw.Write(BasePriority);
                    bw.WriteUInt24BE(Unk_0D);
                }
            }
        }

        #endregion
    }
}

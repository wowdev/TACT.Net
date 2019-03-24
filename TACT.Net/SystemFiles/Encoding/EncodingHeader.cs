using System;
using System.IO;
using TACT.Net.Common;

namespace TACT.Net.Encoding
{
    public sealed class EncodingHeader
    {
        public byte[] Magic { get; private set; } = new byte[] { 69, 78 };
        public byte Version { get; set; } = 1;
        public byte CKeyHashSize { get; private set; } = 16;
        public byte EKeyHashSize { get; private set; } = 16;
        /// <summary>
        /// KB size of the Content Key pages
        /// </summary>
        public ushort CKeyPageSize { get; set; } = 4;
        /// <summary>
        /// KB size of the Encoding Key pages
        /// </summary>
        public ushort EKeyPageSize { get; set; } = 4;
        /// <summary>
        /// Number of Content Key pages
        /// </summary>
        public uint CKeyPageCount { get; private set; }
        /// <summary>
        /// Number of Encoding Key pages
        /// </summary>
        public uint EKeyPageCount { get; private set; }
        public byte Unk_11 { get; private set; } = 0;
        /// <summary>
        /// Total size of the ESpec stringtable
        /// </summary>
        public uint ESpecTableSize { get; internal set; }

        #region IO
        public void Read(BinaryReader br)
        {
            Magic = br.ReadBytes(2);
            Version = br.ReadByte();
            CKeyHashSize = br.ReadByte();
            EKeyHashSize = br.ReadByte();
            CKeyPageSize = br.ReadUInt16BE();
            EKeyPageSize = br.ReadUInt16BE();
            CKeyPageCount = br.ReadUInt32BE();
            EKeyPageCount = br.ReadUInt32BE();
            Unk_11 = br.ReadByte();
            ESpecTableSize = br.ReadUInt32BE();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(CKeyHashSize);
            bw.Write(EKeyHashSize);
            bw.WriteUInt16BE(CKeyPageSize);
            bw.WriteUInt16BE(EKeyPageSize);
            bw.WriteUInt32BE(CKeyPageCount);
            bw.WriteUInt32BE(EKeyPageCount);
            bw.Write(Unk_11);
            bw.WriteUInt32BE(ESpecTableSize);
        }

        #endregion

        #region Helpers

        internal void SetPageCount<T>(uint count) where T : EncodingEntryBase
        {
            Type type = typeof(T);

            switch (true)
            {
                case true when type == typeof(EncodingContentEntry):
                    CKeyPageCount = count;
                    break;
                case true when type == typeof(EncodingEncodedEntry):
                    EKeyPageCount = count;
                    break;
            }
        }

        #endregion
    }
}

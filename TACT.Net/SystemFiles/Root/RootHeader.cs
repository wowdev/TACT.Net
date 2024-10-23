using System.Collections.Generic;
using System.IO;
using TACT.Net.Root.Blocks;

namespace TACT.Net.Root
{
    public class RootHeader
    {
        public uint Magic { get; private set; } = 0x4D465354;
        public uint TotalRecordCount { get; private set; }
        public uint NamedRecordCount { get; private set; }
        public uint V3HeaderSize { get; private set; }
        public uint V3Version { get; private set; }
        public uint V3Padding { get; private set; }

        /// <summary>
        /// Custom versioning, version 3 is 10.1.7+ format with header size/version/padding. Version 2 includes header information. Version 1 doesn't have either.
        /// </summary>
        public uint Version { get; set; } = 1;


        #region IO

        public void Read(BinaryReader br)
        {
            uint magic = br.ReadUInt32();
            if (magic == Magic)
            {
                Version = 2;
                TotalRecordCount = br.ReadUInt32();
                NamedRecordCount = br.ReadUInt32();

                // Hackfix for 10.1.7+ root support
                if (TotalRecordCount < 1000)
                {
                    Version = 3;

                    br.BaseStream.Position -= 8;

                    V3HeaderSize = br.ReadUInt32();
                    V3Version = br.ReadUInt32();
                    TotalRecordCount = br.ReadUInt32();
                    NamedRecordCount = br.ReadUInt32();
                    V3Padding = br.ReadUInt32();
                }
            }
            else
            {
                Version = 1;
                br.BaseStream.Position = 0;
            }
        }

        public void Write(BinaryWriter bw, List<IRootBlock> blocks)
        {
            if (Version == 2 || Version == 3)
            {
                // re-count records
                TotalRecordCount = NamedRecordCount = 0;
                foreach (var block in blocks)
                {
                    TotalRecordCount += (uint)block.Records.Count;
                    if ((block.ContentFlags & ContentFlags.NoNameHash) != ContentFlags.NoNameHash)
                        NamedRecordCount += (uint)block.Records.Count;
                }

                bw.Write(Magic);

                if(Version == 3)
                {
                    bw.Write(V3HeaderSize);
                    bw.Write(V3Version);
                }

                bw.Write(TotalRecordCount);
                bw.Write(NamedRecordCount);

                if(Version == 3)
                    bw.Write(V3Padding);
            }
        }

        #endregion
    }
}

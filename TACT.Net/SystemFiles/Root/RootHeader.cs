using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TACT.Net.Root;

namespace TACT.Net.Root
{
    public class RootHeader
    {
        public uint Magic { get; private set; } = 0x4D465354;
        public uint RecordCount { get; private set; }
        public uint NameHashCount { get; private set; }

        /// <summary>
        /// Custom versioning, version 2 includes header information whereas 1 doesn't
        /// </summary>
        public uint Version { get; set; } = 1;


        #region IO

        public void Read(BinaryReader br)
        {
            uint magic = br.ReadUInt32();
            if(magic == Magic)
            {
                Version = 2;
                RecordCount = br.ReadUInt32();
                NameHashCount = br.ReadUInt32();
            }
            else
            {
                Version = 1;
                br.BaseStream.Position = 0;
            }
        }

        public void Write(BinaryWriter bw, List<RootBlock> blocks)
        {
            if(Version == 2)
            {
                // re-count records
                RecordCount = NameHashCount = 0;
                foreach(var block in blocks)
                {
                    RecordCount += (uint)block.Records.Count;
                    if (block.HasNameHash)
                        NameHashCount += (uint)block.Records.Count;
                }

                bw.Write(Magic);
                bw.Write(RecordCount);
                bw.Write(NameHashCount);
            }
        }

        #endregion
    }
}

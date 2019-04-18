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

        /// <summary>
        /// Custom versioning, version 2 includes header information whereas 1 doesn't
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
            }
            else
            {
                Version = 1;
                br.BaseStream.Position = 0;
            }
        }

        public void Write(BinaryWriter bw, List<IRootBlock> blocks)
        {
            if (Version == 2)
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
                bw.Write(TotalRecordCount);
                bw.Write(NamedRecordCount);
            }
        }

        #endregion
    }
}

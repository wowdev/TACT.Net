using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Root.Blocks
{
    public class RootBlock : IRootBlock
    {
        public ContentFlags ContentFlags { get; set; }
        public LocaleFlags LocaleFlags { get; set; }
        public Dictionary<uint, RootRecord> Records { get; set; }

        #region IO

        public void Read(BinaryReader br)
        {
            int count = br.ReadInt32();
            ContentFlags = (ContentFlags)br.ReadUInt32();
            LocaleFlags = (LocaleFlags)br.ReadUInt32();

            // load the deltas, set the block's record capacity
            var fileIdDeltas = br.ReadStructArray<uint>(count);
            Records = new Dictionary<uint, RootRecord>(fileIdDeltas.Length);

            // calculate the records
            uint currentId = 0;
            RootRecord record;
            foreach (uint delta in fileIdDeltas)
            {
                record = new RootRecord
                {
                    FileIdDelta = delta,
                    CKey = new MD5Hash(br.ReadBytes(16)),
                    NameHash = br.ReadUInt64()
                };

                currentId += delta;
                record.FileId = currentId++;

                Records[record.FileId] = record;
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Records.Count);
            bw.Write((uint)ContentFlags);
            bw.Write((uint)LocaleFlags);
            bw.WriteStructArray(Records.Values.Select(x => x.FileIdDelta));

            foreach (var entry in Records.Values)
            {
                bw.Write(entry.CKey.Value);
                bw.Write(entry.NameHash);
            }
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Root.Blocks
{
    public class RootBlockV2 : IRootBlock
    {
        public ContentFlags ContentFlags { get; set; }
        public LocaleFlags LocaleFlags { get; set; }
        public Dictionary<uint, RootRecord> Records { get; set; }

        private bool HasNameHashes => (ContentFlags & ContentFlags.NoNameHash) != ContentFlags.NoNameHash;

        #region IO

        public void Read(BinaryReader br)
        {
            int count = br.ReadInt32();
            ContentFlags = (ContentFlags)br.ReadUInt32();
            LocaleFlags = (LocaleFlags)br.ReadUInt32();

            // load the deltas, set the block's record capacity
            var fileIdDeltas = br.ReadStructArray<uint>(count);
            Records = new Dictionary<uint, RootRecord>(fileIdDeltas.Length);

            // Content Hashes and Name hashes are now split as they are controlled
            // by the content flags
            Span<byte> ckeyData = br.ReadBytes(count * 16);
            Span<ulong> namehashes = ReadNameHashes(br, count);

            // calculate the records
            uint currentId = 0;
            RootRecord record;
            for (int i = 0; i < fileIdDeltas.Length; i++)
            {
                record = new RootRecord
                {
                    FileIdDelta = fileIdDeltas[i],
                    CKey = new MD5Hash(ckeyData.Slice(i * 16, 16).ToArray()),
                    NameHash = namehashes[i]
                };

                currentId += fileIdDeltas[i];
                record.FileId = currentId++;

                Records[record.FileId] = record;
            }
        }

        public void Write(BinaryWriter bw)
        {
            // split into seperate collections so that we don't need to iterate
            // the Records three seperate times
            int i = 0;
            var deltas = new uint[Records.Count];
            var ckeyData = new List<byte>(Records.Count * 16);
            var namehashes = new ulong[Records.Count];
            
            foreach(var record in Records.Values)
            {
                ckeyData.AddRange(record.CKey.Value);
                deltas[i] = record.FileIdDelta;
                namehashes[i] = record.NameHash;
                i++;
            }

            // write the actual data
            bw.Write(Records.Count);
            bw.Write((uint)ContentFlags);
            bw.Write((uint)LocaleFlags);
            bw.WriteStructArray(deltas);

            bw.Write(ckeyData.ToArray());
            if (HasNameHashes)
                bw.WriteStructArray(namehashes);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads the NameHash block if it exists otherwise returns a 0 filled array
        /// </summary>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private ulong[] ReadNameHashes(BinaryReader br, int count)
        {
            ulong[] hashes = null;

            if (HasNameHashes)
                hashes = br.ReadStructArray<ulong>(count).ToArray();

            return hashes ?? new ulong[count];
        }

        #endregion
    }
}

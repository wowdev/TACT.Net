using System.Collections.Generic;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Patch
{
    /// <summary>
    /// Documents what patches are available for a specific file
    /// </summary>
    public class PatchEntry
    {
        /// <summary>
        /// The current content hash of the file
        /// </summary>
        public MD5Hash CKey;
        /// <summary>
        /// The current decompressed size of the file
        /// </summary>
        public ulong DecompressedSize;

        public List<PatchRecord> Records;

        #region IO
        public bool Read(BinaryReader br, PatchHeader header)
        {
            if (br.BaseStream.Position == br.BaseStream.Length)
                return false;

            byte entryCount = br.ReadByte();
            if (entryCount == 0)
                return false;

            CKey = new MD5Hash(br.ReadBytes(header.FileKeySize));
            DecompressedSize = br.ReadUInt40BE();

            Records = new List<PatchRecord>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                var entry = new PatchRecord();
                entry.Read(br, header);
                Records.Add(entry);
            }

            return true;
        }
        #endregion
    }
}

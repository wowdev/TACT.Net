using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Patch
{
    /// <summary>
    /// Contains information for a patch of a file
    /// </summary>
    public class PatchRecord
    {
        /// <summary>
        /// Post patch EKey
        /// </summary>
        public MD5Hash EKey;
        /// <summary>
        /// Post patch decompressed size
        /// </summary>
        public ulong DecompressedSize;
        /// <summary>
        /// Patch archive EKey lookup
        /// </summary>
        public MD5Hash PatchEKey;
        /// <summary>
        /// Size of the patch data
        /// </summary>
        public uint PatchSize;
        /// <summary>
        /// Order of application
        /// </summary>
        public byte Ordinal;

        #region IO

        public void Read(BinaryReader br, PatchHeader header)
        {
            EKey = new MD5Hash(br.ReadBytes(header.FileKeySize));
            DecompressedSize = br.ReadUInt40BE();
            PatchEKey = new MD5Hash(br.ReadBytes(header.PatchEKeySize));
            PatchSize = br.ReadUInt32BE();
            Ordinal = br.ReadByte();
        }

        #endregion
    }
}

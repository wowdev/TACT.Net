using System;
using TACT.Net.Common.Cryptography;

namespace TACT.Net.BlockTable
{
    /// <summary>
    /// Encoding Block
    /// </summary>
    public sealed class EBlock
    {
        /// <summary>
        /// Size of file after encoding and/or compressing
        /// </summary>
        public uint CompressedSize;
        /// <summary>
        /// Size of original file
        /// </summary>
        public uint DecompressedSize;
        /// <summary>
        /// Encoding Key
        /// </summary>
        public MD5Hash EKey;
        /// <summary>
        /// Encoding Type and Compression Level
        /// </summary>
        public EMap EncodingMap;

        public EBlock()
        {
            EncodingMap = default(EMap);
        }

        /// <summary>
        /// Returns the block's ESpec string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string spec;

            // 256K* is the max that Blizzard documents
            if (CompressedSize >= 1024 * 256)
                spec = "256K*=";
            // closest floored KB + greedy
            else if (CompressedSize > 1024)
                spec = (int)Math.Floor(CompressedSize / 1024d) + "K*";
            // actual size + greedy
            else
                spec = CompressedSize + "*";

            spec += EncodingMap.ToString();

            return spec.ToLowerInvariant();
        }
    }
}

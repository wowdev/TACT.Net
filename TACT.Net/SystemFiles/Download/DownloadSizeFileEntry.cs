using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Download
{
    public class DownloadSizeFileEntry : IDownloadFileEntry
    {
        /// <summary>
        /// Encoding Key
        /// </summary>
        public MD5Hash EKey { get; set; }
        /// <summary>
        /// Encoded file size
        /// </summary>
        public uint CompressedSize { get; set; }

        #region IO

        public void Read(BinaryReader br, DownloadSizeHeader header)
        {
            EKey = new MD5Hash(br.ReadBytes(header.EKeySize));
            CompressedSize = br.ReadUInt32BE();
        }

        public void Write(BinaryWriter bw, DownloadSizeHeader header)
        {
            bw.Write(EKey.Value, 0, header.EKeySize);
            bw.WriteUInt32BE(CompressedSize);
        }

        #endregion
    }
}

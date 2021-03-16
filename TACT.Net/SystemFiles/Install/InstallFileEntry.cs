using System;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Install
{
    public class InstallFileEntry
    {
        /// <summary>
        /// Name and directory for the file
        /// </summary>
        public string FilePath;
        /// <summary>
        /// Content Key
        /// </summary>
        public MD5Hash CKey;
        /// <summary>
        /// Orginal file size
        /// </summary>
        public uint DecompressedSize;

        #region IO

        public void Read(BinaryReader br, InstallHeader header)
        {
            FilePath = br.ReadCString();
            CKey = new MD5Hash(br.ReadBytes(header.CKeySize));
            DecompressedSize = br.ReadUInt32BE();
        }

        public void Write(BinaryWriter bw)
        {
            bw.WriteCString(FilePath);
            bw.Write(CKey.Value);
            bw.WriteUInt32BE(DecompressedSize);
        }

        #endregion

        public override bool Equals(object obj)
        {
            return obj is InstallFileEntry entry &&
                entry.FilePath.ToUpper() == FilePath.ToUpper() &&
                entry.DecompressedSize == DecompressedSize &&
                entry.CKey == CKey;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FilePath.ToUpper(), DecompressedSize, CKey);
        }

        public override string ToString() => FilePath;
    }
}

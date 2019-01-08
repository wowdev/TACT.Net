using System;
using System.IO;
using TACT.Net.BlockTable;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;

namespace TACT.Net
{
    public sealed class CASRecord
    {
        /// <summary>
        /// Filename as used in the Root file
        /// </summary>
        public string FileName;
        /// <summary>
        /// External BLTE blob file
        /// </summary>
        public string BLTEPath = "";
        /// <summary>
        /// Encoding Block
        /// </summary>
        public EBlock EBlock;
        /// <summary>
        /// Content Key
        /// </summary>
        public MD5Hash CKey;
        /// <summary>
        /// Encoding Key
        /// </summary>
        public MD5Hash EKey => EBlock.EKey;
        /// <summary>
        /// Returns the block ESpec
        /// </summary>
        public string ESpec { get; internal set; }

        #region Helpers

        public bool WriteTo(Stream stream, bool dispose = true)
        {
            if (!File.Exists(BLTEPath))
                return false;

            stream.Write(File.ReadAllBytes(BLTEPath));
            if (dispose)
                Helpers.Delete(BLTEPath);

            return true;
        }

        #endregion
    }
}

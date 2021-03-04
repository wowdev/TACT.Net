using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Encoding
{
    public class EncodingContentEntry : EncodingEntryBase
    {
        /// <summary>
        /// Original file size
        /// </summary>
        public ulong DecompressedSize;
        /// <summary>
        /// Content key
        /// </summary>
        public MD5Hash CKey;
        /// <summary>
        /// Encoding key
        /// </summary>
        public List<MD5Hash> EKeys;

        internal override MD5Hash Key => CKey;
        internal override int Size => 6 + CKey.Value.Length + EKeys.Sum((x) => x.Value.Length);

        #region IO

        public override bool Read(BinaryReader br, EncodingHeader header)
        {
            byte keyCount = br.ReadByte();
            if (keyCount == 0)
                return false;

            DecompressedSize = br.ReadUInt40BE();
            CKey = new MD5Hash(br.ReadBytes(header.CKeyHashSize));

            EKeys = new List<MD5Hash>(keyCount);
            for (var i = 0; i < keyCount; ++i)
                EKeys.Add(new MD5Hash(br.ReadBytes(header.EKeyHashSize)));

            return true;
        }

        public override void Write(BinaryWriter bw, EncodingHeader header)
        {
            bw.Write((byte)EKeys.Count);
            bw.WriteUInt40BE(DecompressedSize);
            bw.Write(CKey.Value);

            EKeys.ForEach(ekey => bw.Write(ekey.Value));
        }

        #endregion

        #region Helpers

        internal override void Validate()
        {
            if (EKeys.Count == 0)
                throw new InvalidDataException("Entry contains no EKey");
            if (CKey.IsEmpty)
                throw new InvalidDataException("Entry contains no CKey");

            base.Validate();
        }

        #endregion

        public override string ToString() => CKey.ToString();
    }
}

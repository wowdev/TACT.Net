using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;

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
        /// Encoding keys; one for each block
        /// </summary>
        public HashSet<MD5Hash> EKeys;

        internal override MD5Hash Key => CKey;
        internal override int Size => 6 + CKey.Value.Length + (EKeys.Count > 0 ? EKeys.Count * EKeys.First().Value.Length : 0);

        #region Constructors

        public EncodingContentEntry()
        {
            EKeys = new HashSet<MD5Hash>();
        }

        #endregion

        #region IO

        public override bool Read(BinaryReader br, EncodingHeader header)
        {
            byte keyCount = br.ReadByte();
            if (keyCount == 0)
                return false;

            DecompressedSize = br.ReadUInt40BE();
            CKey = new MD5Hash(br.ReadBytes(header.CKeyHashSize));

            EKeys = new HashSet<MD5Hash>(keyCount);
            for (int i = 0; i < keyCount; i++)
                EKeys.Add(new MD5Hash(br.ReadBytes(header.EKeyHashSize)));

            return true;
        }

        public override void Write(BinaryWriter bw, EncodingHeader header)
        {
            bw.Write((byte)EKeys.Count);
            bw.WriteUInt40BE(DecompressedSize);
            bw.Write(CKey.Value);

            foreach (var ekey in EKeys)
                bw.Write(ekey.Value);
        }

        #endregion

        #region Helpers

        internal override void Validate()
        {
            EKeys?.RemoveWhere(x => x.IsEmpty);

            if (EKeys == null || EKeys.Count == 0)
                throw new InvalidDataException("Entry contains no EKeys");

            base.Validate();
        }

        #endregion
    }
}

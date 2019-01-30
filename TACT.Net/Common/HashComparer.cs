using System;
using System.Collections.Generic;
using TACT.Net.Cryptography;
using TACT.Net.Encoding;

namespace TACT.Net.Common
{
    internal class HashComparer : IComparer<byte[]>, IComparer<MD5Hash>, IComparer<string>, IComparer<EncodingEntryBase>, IEqualityComparer<MD5Hash>
    {
        public int KeySize { get; set; } = 16;

        public int Compare(MD5Hash x, MD5Hash y) => Compare(x.Value, y.Value);
        public int Compare(string x, string y) => Compare(x.ToByteArray(), y.ToByteArray());
        public int Compare(EncodingEntryBase x, EncodingEntryBase y) => Compare(x.Key.Value, y.Key.Value);

        public int Compare(byte[] x, byte[] y)
        {
            int length = Math.Min(x.Length, y.Length);

            for (int i = 0; i < length; i++)
            {
                int c = x[i].CompareTo(y[i]);
                if (c != 0)
                    return c;
            }

            return 0;
        }

        public bool Equals(MD5Hash x, MD5Hash y)
        {
            if (KeySize == 16)
                return x == y;

            return Compare(x, y) == 0;
        }

        public int GetHashCode(MD5Hash obj)
        {
            return obj.GetHashCode();
        }
    }
}

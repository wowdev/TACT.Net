using System;
using System.Collections.Generic;
using TACT.Net.Common;
using TACT.Net.Encoding;

namespace TACT.Net.Cryptography
{
    /// <summary>
    /// A generic MD5 Hash Comparer and Equality Comparer
    /// </summary>
    public sealed class MD5HashComparer : IComparer<byte[]>, IComparer<MD5Hash>, IComparer<string>, IComparer<EncodingEntryBase>, IEqualityComparer<MD5Hash>
    {
        public int Compare(MD5Hash x, MD5Hash y) => Compare(x.Value, y.Value);
        public int Compare(string x, string y) => Compare(x.ToByteArray(), y.ToByteArray());
        public int Compare(EncodingEntryBase x, EncodingEntryBase y) => Compare(x.Key.Value, y.Key.Value);
        public int Compare(byte[] x, byte[] y) => Compare(x, y, 16);

        private int Compare(byte[] x, byte[] y, int length)
        {
            int c;
            for (int i = 0; i < length; i++)
            {
                c = x[i] - y[i];
                if (c != 0)
                    return c;
            }

            return 0;
        }

        public bool Equals(MD5Hash x, MD5Hash y)
        {
            if (x.Value.Length == y.Value.Length)
                return x == y;

            return Compare(x.Value, y.Value, Math.Min(x.Value.Length, y.Value.Length)) == 0;
        }

        public int GetHashCode(MD5Hash obj)
        {
            return obj.GetHashCode();
        }
    }
}

using System;
using System.Collections.Generic;
using TACT.Net.Encoding;
using TACT.Net.Shared.DownloadFile;

namespace TACT.Net.Common.Cryptography
{
    internal class HashComparer : IComparer<byte[]>, IComparer<MD5Hash>, IComparer<string>, IComparer<EncodingEntryBase>, IEqualityComparer<MD5Hash>
    {
        private readonly bool _nonStandardKeySize;

        public HashComparer(bool nonStandardKeySize = false)
        {
            _nonStandardKeySize = nonStandardKeySize;
        }


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
            if (_nonStandardKeySize)
                return Compare(x, y) == 0;

            return x == y;
        }
        public int GetHashCode(MD5Hash obj) => obj.GetHashCode();
    }
}

using System;
using System.Security.Cryptography;

namespace TACT.Net.Cryptography
{
    /// <summary>
    /// https://en.wikipedia.org/wiki/Jenkins_hash_function
    /// Original implementation by TOMRUS: https://github.com/WoW-Tools/CASCExplorer/blob/master/CascLib/Jenkins96.cs
    /// </summary>
    public sealed class Lookup3 : HashAlgorithm
    {
        public ulong Result { get; private set; }

        public ulong ComputeHash(string str, bool normalize = true)
        {
            if (normalize)
                str = str.Replace('/', '\\').ToUpperInvariant();

            ComputeHash(System.Text.Encoding.ASCII.GetBytes(str));
            return Result;
        }

        public override void Initialize() { }

        protected override unsafe void HashCore(byte[] array, int ibStart, int cbSize)
        {
            uint rot(uint x, int k) => (x << k) | (x >> (32 - k));

            uint length = (uint)array.Length;
            uint a, b, c;
            a = b = c = 0xdeadbeef + length;

            if (length == 0)
            {
                Result = ((ulong)c << 32) | b;
                return;
            }

            uint newLen = length + (12 - length % 12) % 12;
            if (length != newLen)
            {
                Array.Resize(ref array, (int)newLen);
                length = newLen;
            }

            fixed (byte* bb = array)
            {
                uint* u = (uint*)bb;

                for (var j = 0; j < length - 12; j += 12)
                {
                    a += *(u + j / 4);
                    b += *(u + j / 4 + 1);
                    c += *(u + j / 4 + 2);

                    a -= c; a ^= rot(c, 4); c += b;
                    b -= a; b ^= rot(a, 6); a += c;
                    c -= b; c ^= rot(b, 8); b += a;
                    a -= c; a ^= rot(c, 16); c += b;
                    b -= a; b ^= rot(a, 19); a += c;
                    c -= b; c ^= rot(b, 4); b += a;
                }

                uint i = length - 12;
                a += *(u + i / 4);
                b += *(u + i / 4 + 1);
                c += *(u + i / 4 + 2);

                c ^= b; c -= rot(b, 14);
                a ^= c; a -= rot(c, 11);
                b ^= a; b -= rot(a, 25);
                c ^= b; c -= rot(b, 16);
                a ^= c; a -= rot(c, 4);
                b ^= a; b -= rot(a, 14);
                c ^= b; c -= rot(b, 24);

                Result = ((ulong)c << 32) | b;
            }
        }

        protected override byte[] HashFinal() => BitConverter.GetBytes(Result);
    }
}

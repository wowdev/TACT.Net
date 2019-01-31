using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TACT.Net.Cryptography;

namespace TACT.Net.Common
{
    internal static class Extensions
    {
        #region MD5 Extensions

        public static MD5Hash MD5Hash(this Stream stream, long offset = 0)
        {
            stream.Position = offset;
            using (var md5 = MD5.Create())
                return new MD5Hash(md5.ComputeHash(stream));
        }

        public static MD5Hash MD5Hash(this byte[] bytes)
        {
            using (var md5 = MD5.Create())
                return new MD5Hash(md5.ComputeHash(bytes));
        }

        public static string ToHex(this byte[] array)
        {
            var c = new char[array.Length * 2].AsSpan();

            byte b;
            for (int i = 0; i < array.Length; ++i)
            {
                b = (byte)(array[i] >> 4);
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = (byte)(array[i] & 0xF);
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }

            return new string(c).ToLowerInvariant();
        }

        public static byte[] ToByteArray(this string hex, int count = 32)
        {
            int CharToHex(char c) => c - (c < 0x3A ? 0x30 : 0x57);

            byte[] bytes = new byte[Math.Min(hex.Length / 2, count)];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)((CharToHex(hex[i << 1]) << 4) + CharToHex(hex[(i << 1) + 1]));

            return bytes;
        }

        #endregion

        #region BinaryReader Extensions
        public static ulong ReadUInt40BE(this BinaryReader reader) => ReadUIntBE(reader, 5);
        public static uint ReadUInt32BE(this BinaryReader reader) => (uint)ReadUIntBE(reader, 4);
        public static uint ReadUInt24BE(this BinaryReader reader) => (uint)ReadUIntBE(reader, 3);
        public static ushort ReadUInt16BE(this BinaryReader reader) => (ushort)ReadUIntBE(reader, 2);
        public static ulong ReadUIntBE(this BinaryReader reader, int size)
        {
            byte[] buffer = reader.ReadBytes(size);
            Array.Reverse(buffer);
            Array.Resize(ref buffer, 8);
            return BitConverter.ToUInt64(buffer);
        }

        public static Span<T> ReadStructArray<T>(this BinaryReader reader, int count) where T : unmanaged
        {
            if (count <= 0)
                return new T[0];

            byte[] buffer = reader.ReadBytes(count * Marshal.SizeOf<T>());
            return MemoryMarshal.Cast<byte, T>(buffer);
        }

        public static string ReadCString(this BinaryReader reader)
        {
            StringBuilder sb = new StringBuilder(0x40);

            byte b;
            while ((b = reader.ReadByte()) != 0)
                sb.Append((char)b);

            return sb.ToString();
        }
        #endregion

        #region BinaryWriter Extensions
        public static void WriteUInt40BE(this BinaryWriter writer, ulong value) => WriteUIntBE(writer, value, 5);
        public static void WriteUInt32BE(this BinaryWriter writer, uint value) => WriteUIntBE(writer, value, 4);
        public static void WriteUInt24BE(this BinaryWriter writer, uint value) => WriteUIntBE(writer, value, 3);
        public static void WriteUInt16BE(this BinaryWriter writer, ushort value) => WriteUIntBE(writer, value, 2);
        public static void WriteUIntBE(this BinaryWriter writer, ulong value, int size)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            writer.Write(buffer, 8 - size, size);
        }

        public static void WriteStructArray<T>(this BinaryWriter writer, IEnumerable<T> objs) where T : unmanaged
        {
            if (!objs.Any())
                return;

            writer.Write(MemoryMarshal.AsBytes<T>(objs.ToArray()));
        }

        public static void WriteCString(this BinaryWriter writer, string value)
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes(value));
            writer.Write((byte)0);
        }
        #endregion

        #region Misc

        /// <summary>
        /// MD5 hashes a slice of a stream with optional key resizing
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="md5"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="keysize"></param>
        /// <returns></returns>
        public static MD5Hash HashSlice(this Stream stream, MD5 md5, long offset, long length, int keysize = 16)
        {
            long startPos = stream.Position;

            stream.Position = offset;
            byte[] buffer = new byte[length];
            stream.Read(buffer);
            stream.Position = startPos;

            byte[] hash = md5.ComputeHash(buffer);
            if (keysize != 16)
                Array.Resize(ref hash, keysize);

            return new MD5Hash(hash);
        }

        /// <summary>
        /// Returns the zero-based index of a key within a Dictionary's KeyCollection
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static int IndexOfKey<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Func<TKey, bool> comparer)
        {
            int i = 0;
            foreach (var key in dictionary.Keys)
            {
                if (comparer.Invoke(key))
                    return i;
                i++;
            }

            return -1;
        }

        /// <summary>
        /// Sugar syntax string to byte array with optional encoding
        /// </summary>
        /// <param name="str"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static byte[] GetBytes(this string str, string encoding = "UTF-8")
        {
            return System.Text.Encoding.GetEncoding(encoding).GetBytes(str);
        }

        /// <summary>
        /// Copies a specific length of one stream to another from the current offset
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="destination"></param>
        /// <param name="length"></param>
        public static void PartialCopyTo(this Stream stream, Stream destination, long length)
        {
            // pre-LOH magic number
            byte[] buffer = new byte[81920];

            long remaining = length;
            int read;
            while (remaining >= buffer.Length && (read = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                destination.Write(buffer, 0, read);
                remaining -= read;
            }

            // final block < buffer size
            read = stream.Read(buffer, 0, (int)remaining);
            destination.Write(buffer, 0, read);
        }

        #endregion
    }
}

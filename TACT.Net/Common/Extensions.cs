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
    public static class Extensions
    {
        #region MD5 Extensions

        public static MD5Hash MD5Hash(this Stream stream, long offset = 0)
        {
            stream.Position = offset;
            using (var md5 = MD5.Create())
                return new MD5Hash(md5.ComputeHash(stream));
        }

        public static string ToHex(this byte[] array)
        {
            Span<char> c = new char[array.Length * 2];

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

        public static byte[] ToByteArray(this string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        #endregion

        #region BinaryReader Extensions

        public static ulong ReadUInt40BE(this BinaryReader reader) => Endian.SwapUInt40(reader.ReadBytes(5));
        public static uint ReadUInt32BE(this BinaryReader reader) => Endian.SwapUInt32(reader.ReadUInt32());
        public static uint ReadUInt24BE(this BinaryReader reader) => Endian.SwapUInt24(reader.ReadBytes(3));
        public static ushort ReadUInt16BE(this BinaryReader reader) => Endian.SwapUInt16(reader.ReadUInt16());
        public static ulong ReadUIntBE(this BinaryReader reader, int size)
        {
            byte[] buffer = new byte[8];
            reader.Read(buffer, 8 - size, size);
            return Endian.SwapUInt64(buffer);
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
        public static void WriteUInt32BE(this BinaryWriter writer, uint value) => writer.Write(Endian.SwapUInt32(value));
        public static void WriteUInt24BE(this BinaryWriter writer, uint value) => WriteUIntBE(writer, value, 3);
        public static void WriteUInt16BE(this BinaryWriter writer, ushort value) => writer.Write(Endian.SwapUInt16(value));
        public static void WriteUIntBE(this BinaryWriter writer, ulong value, int size)
        {
            byte[] buffer = BitConverter.GetBytes(Endian.SwapUInt64(value));
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

        #region ZBS Stream Extensions

        public static unsafe long ReadInt64BS(this Stream reader)
        {
            byte[] buffer = new byte[8];
            reader.Read(buffer);

            fixed (byte* b = &buffer[0])
            {
                ulong raw = *(ulong*)b;
                long value = (long)(raw & 0x7FFFFFFFFFFFFFFF);
                return (raw & 0x8000000000000000) == 0x8000000000000000 ? -value : value;
            }
        }

        public static unsafe void WriteInt64BS(this Stream writer, long value)
        {
            byte[] buffer = new byte[8];
            fixed (byte* b = buffer)
                *(ulong*)b = (ulong)Math.Abs(value) | (value < 0 ? 0x8000000000000000 : 0);

            writer.Write(buffer);
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
            foreach (var kvp in dictionary)
            {
                if (comparer.Invoke(kvp.Key))
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
            if (remaining > 0)
            {
                read = stream.Read(buffer, 0, (int)remaining);
                destination.Write(buffer, 0, read);
            }
        }

        /// <summary>
        /// Incasesensitive array search returning the index of the first occurrence
        /// </summary>
        /// <param name="array"></param>
        /// <param name="needle"></param>
        /// <returns></returns>
        public static int IndexOf(this string[] array, string needle)
        {
            return Array.FindIndex(array, t => t.Equals(needle, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Normalise local file path to casc path
        /// </summary>
        /// <param name="str"></param>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public static string WoWNormalise(this string str, string basePath = "")
        {
            str = str.Trim();

            if (basePath != "")
                str = str.Replace(basePath, "");

            return str.TrimStart(new char[] { '\\', '/' })
                      .Replace('\\', '/')
                      .ToLowerInvariant();
        }

        /// <summary>
        /// Compresses and resuses the ZLibStream's own BaseStream
        /// </summary>
        /// <param name="offset"></param>
        public static void WriteBasestream(this Joveler.Compression.ZLib.ZLibStream stream, long offset = 0)
        {
            stream.BaseStream.Position = 0;

            // the largest multiple of 4096 smaller than the LOH threshold
            byte[] buffer = new byte[81920];

            // calculate the read position delta
            offset -= stream.TotalIn;

            // reading in chunks and jumping stream position is faster and allocates less with large streams
            int read;
            while ((read = stream.BaseStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                // jump to the write position
                stream.BaseStream.Position = offset + stream.TotalOut;
                stream.Write(buffer, 0, read);

                // reset to the next read position
                stream.BaseStream.Position = offset + stream.TotalIn;
            }

            // jump to the final write position and flush the buffer
            stream.BaseStream.Position = offset + stream.TotalOut;
            stream.Flush();
        }

        #endregion
    }
}

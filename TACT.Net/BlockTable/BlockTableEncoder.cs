using System;
using System.IO;
using TACT.Net.Common;

namespace TACT.Net.BlockTable
{
    public static class BlockTableEncoder
    {
        #region Encode

        /// <summary>
        /// Encodes a byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encodingmap"></param>
        /// <returns></returns>
        public static CASRecord Encode(byte[] data, EMap encodingmap)
        {
            using (var bt = new BlockTableStreamWriter(encodingmap))
            {
                bt.Write(data);
                return bt.Finalise();
            }
        }

        /// <summary>
        /// Encodes a file using Blizzard-esque rules
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static CASRecord Encode(string filename)
        {
            using (var fs = File.OpenRead(filename))
            using (var bt = new BlockTableStreamWriter(GetEMapFromExtension(filename)))
            {
                fs.CopyTo(bt);
                return bt.Finalise();
            }
        }

        /// <summary>
        /// Encodes a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encodingmap"></param>
        /// <returns></returns>
        public static CASRecord Encode(Stream stream, EMap encodingmap)
        {
            using (var bt = new BlockTableStreamWriter(encodingmap))
            {
                stream.Position = 0;
                stream.CopyTo(bt);
                return bt.Finalise();
            }
        }

        #endregion

        #region Encode and Export

        /// <summary>
        /// Encodes a byte array and saves the result to disk
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="data"></param>
        /// <param name="encodingmap"></param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(string directory, byte[] data, EMap encodingmap)
        {
            using (var bt = new BlockTableStreamWriter(encodingmap))
            {
                bt.Write(data);
                var record = bt.Finalise();

                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                    bt.WriteTo(fs);

                return record;
            }
        }

        /// <summary>
        /// Encodes a file using Blizzard-esque rules and saves the result to disk
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(string directory, string filename)
        {
            using (var bt = new BlockTableStreamWriter(GetEMapFromExtension(filename)))
            {
                // read the file into the BlockTableStream
                using (var fs = File.OpenRead(filename))
                    fs.CopyTo(bt);

                // encode
                var record = bt.Finalise();

                // save the encoded file
                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                    bt.WriteTo(fs);

                return record;
            }
        }

        /// <summary>
        /// Encodes a stream using Blizzard-esque rules and saves the result to disk
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="stream"></param>
        /// <param name="encodingmap"></param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(string directory, Stream stream, EMap encodingmap)
        {
            using (var bt = new BlockTableStreamWriter(encodingmap))
            {
                stream.Position = 0;
                stream.CopyTo(bt);
                var record = bt.Finalise();

                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                    bt.WriteTo(fs);

                return record;
            }
        }

        #endregion

        #region Decode and Export

        /// <summary>
        /// Decodes a byte array and saves the result to disk
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static void DecodeAndExport(string filepath, byte[] data)
        {
            using (var bt = new BlockTableStreamReader(data))
            using (var fs = File.Create(filepath))
            {
                bt.Position = 0;
                bt.CopyTo(fs);
            }
        }

        /// <summary>
        /// Decodes a file and saves the result to disk
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static void DecodeAndExport(string filepath, string filename)
        {
            using (var bt = new BlockTableStreamReader(filename))
            using (var fs = File.Create(filepath))
            {
                bt.Position = 0;
                bt.CopyTo(fs);
            }
        }

        /// <summary>
        /// Decodes a stream and saves the result to disk
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="stream"></param>
        /// <param name="encodingmap"></param>
        /// <returns></returns>
        public static void DecodeAndExport(string filepath, Stream stream)
        {
            using (var bt = new BlockTableStreamReader(stream))
            using (var fs = File.Create(filepath))
            {
                bt.Position = 0;
                bt.CopyTo(fs);
            }

        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the EncodingMap based on Blizzard-esque rules
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="filesize"></param>
        /// <returns></returns>
        public static EMap GetEMapFromExtension(string filename, long filesize = -1)
        {
            if (string.IsNullOrWhiteSpace(filename) || filename.IndexOf('.') == -1 || filename.EndsWith('.'))
                throw new ArgumentException("Invalid Filename");

            // not worth compressing files < 20 bytes
            if (filesize >= 0 && filesize < 20)
                return new EMap(EType.None, 0);

            // Blizzard-esque rules
            switch (Path.GetExtension(filename).ToUpperInvariant())
            {
                // don't compress - natively compressed formats
                case ".AVI":
                case ".MP3":
                case ".OGG":
                case ".PNG":
                case ".TTF":
                    return new EMap(EType.None, 0);
                // mpq compression
                case ".ADT":
                case ".BLP":
                case ".MDX":
                case ".M2":
                case ".PHYS":
                case ".SKIN":
                case ".WDT":
                case ".WMO":
                    return new EMap(EType.ZLib, 6, true);
                // best compression
                default:
                    return new EMap(EType.ZLib, 9);
            }
        }

        #endregion
    }
}

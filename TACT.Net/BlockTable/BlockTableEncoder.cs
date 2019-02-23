using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

        #region Bulk Encode

        /// <summary>
        /// Bulk encodes a directory of files
        /// </summary>
        /// <param name="directory">Input directory, this is recursively enumerated</param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncode(string directory)
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
            return BulkEncode(files);
        }

        /// <summary>
        /// Bulk encodes a list of file names
        /// </summary>
        /// <param name="filenames"></param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncode(IEnumerable<string> filenames)
        {
            var resultSet = new ConcurrentDictionary<string, CASRecord>();

            Parallel.ForEach(filenames, file => resultSet.TryAdd(file, Encode(file)));
            return resultSet;
        }

        #endregion

        #region Encode and Export

        /// <summary>
        /// Encodes a byte array and saves the result to disk
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encodingmap"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(byte[] data, EMap encodingmap, string directory)
        {
            using (var bt = new BlockTableStreamWriter(encodingmap))
            {
                bt.Write(data);
                var record = bt.Finalise();

                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.FileName = saveLocation;
                }

                return record;
            }
        }

        /// <summary>
        /// Encodes a file using Blizzard-esque rules and saves the result to disk
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(string filename, string directory)
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
                {
                    bt.WriteTo(fs);
                    record.FileName = saveLocation;
                }

                return record;
            }
        }

        /// <summary>
        /// Encodes a stream using Blizzard-esque rules and saves the result to disk
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encodingmap"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(Stream stream, EMap encodingmap, string directory)
        {
            using (var bt = new BlockTableStreamWriter(encodingmap))
            {
                stream.Position = 0;
                stream.CopyTo(bt);
                var record = bt.Finalise();

                string saveLocation = Helpers.GetCDNPath(record.EKey.ToString(), "data", directory, true);
                using (var fs = File.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.FileName = saveLocation;
                }

                return record;
            }
        }

        #endregion

        #region Bulk Encode and Export

        /// <summary>
        /// Bulk encodes a directory of files and saves the results to disk
        /// </summary>
        /// <param name="inputDirectory">Input directory, this is recursively enumerated</param>
        /// <param name="outputDirectory"></param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncodeAndExport(string inputDirectory, string outputDirectory)
        {
            var resultSet = new ConcurrentDictionary<string, CASRecord>();
            var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories);

            return BulkEncodeAndExport(files, outputDirectory);
        }

        /// <summary>
        /// Bulk encodes a collection of files and saves the results to disk
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncodeAndExport(IEnumerable<string> filenames, string directory)
        {
            var resultSet = new ConcurrentDictionary<string, CASRecord>();

            Parallel.ForEach(filenames, file => resultSet.TryAdd(file, EncodeAndExport(directory, file)));
            return resultSet;
        }

        #endregion

        #region Decode and Export

        /// <summary>
        /// Decodes a byte array and saves the result to disk
        /// </summary>
        /// <param name="data"></param>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static void DecodeAndExport(byte[] data, string filepath)
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
        /// <param name="inputPath"></param>
        /// <param name="outputPath"></param>
        /// <returns></returns>
        public static void DecodeAndExport(string inputPath, string outputPath)
        {
            using (var bt = new BlockTableStreamReader(inputPath))
            using (var fs = File.Create(outputPath))
            {
                bt.Position = 0;
                bt.CopyTo(fs);
            }
        }

        /// <summary>
        /// Decodes a stream and saves the result to disk
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static void DecodeAndExport(Stream stream, string filepath)
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
        /// <param name="filesize">Small files are ignored from compression</param>
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

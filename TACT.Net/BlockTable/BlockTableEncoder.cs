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
        /// <param name="encodingMap"></param>
        /// <param name="rootName">Root filename</param>
        /// <returns></returns>
        public static CASRecord Encode(byte[] data, EMap encodingMap, string rootName = null)
        {
            using (var bt = new BlockTableStreamWriter(encodingMap))
            {
                bt.Write(data);

                var record = bt.Finalise();
                record.FileName = rootName;
                return record;
            }
        }

        /// <summary>
        /// Encodes a file using Blizzard-esque rules
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="rootName">Root filename</param>
        /// <returns></returns>
        public static CASRecord Encode(string filename, string rootName = null)
        {
            using (var fs = File.OpenRead(filename))
            using (var bt = new BlockTableStreamWriter(GetEMapFromExtension(filename)))
            {
                fs.CopyTo(bt);

                var record = bt.Finalise();
                record.FileName = rootName;
                return record;
            }
        }

        /// <summary>
        /// Encodes a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encodingMap"></param>
        /// <param name="rootName">Root filename</param>
        /// <returns></returns>
        public static CASRecord Encode(Stream stream, EMap encodingMap, string rootName = null)
        {
            using (var bt = new BlockTableStreamWriter(encodingMap))
            {
                stream.Position = 0;
                stream.CopyTo(bt);

                var record = bt.Finalise();
                record.FileName = rootName;
                return record;
            }
        }

        #endregion

        #region Bulk Encode

        /// <summary>
        /// Bulk encodes a directory of files
        /// </summary>
        /// <param name="directory">Input directory, this is recursively enumerated</param>
        /// <param name="nameFactory">Root name generation function, input is the source filename</param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncode(string directory, Func<string, string> nameFactory = null)
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
            return BulkEncode(files, nameFactory);
        }

        /// <summary>
        /// Bulk encodes a list of file names
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="nameFactory">Root name generation function, input is the source filename</param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncode(IEnumerable<string> filenames, Func<string, string> nameFactory = null)
        {
            var resultSet = new ConcurrentDictionary<string, CASRecord>();

            Parallel.ForEach(filenames, file => resultSet.TryAdd(file, Encode(file, nameFactory?.Invoke(file))));
            return resultSet;
        }

        #endregion

        #region Encode and Export

        /// <summary>
        /// Encodes a byte array and saves the result to disk
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encodingMap"></param>
        /// <param name="directory"></param>
        /// <param name="rootName">Root filename</param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(byte[] data, EMap encodingMap, string directory, string rootName = null)
        {
            using (var bt = new BlockTableStreamWriter(encodingMap))
            {
                bt.Write(data);
                var record = bt.Finalise();

                string saveLocation = Path.Combine(directory, record.EKey.ToString());
                using (var fs = Helpers.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                    record.FileName = rootName;
                }

                return record;
            }
        }

        /// <summary>
        /// Encodes a file using Blizzard-esque rules and saves the result to disk
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="directory"></param>
        /// <param name="rootName">Root filename</param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(string filename, string directory, string rootName = null)
        {
            using (var bt = new BlockTableStreamWriter(GetEMapFromExtension(filename)))
            {
                // read the file into the BlockTableStream
                using (var fs = File.OpenRead(filename))
                    fs.CopyTo(bt);

                // encode
                var record = bt.Finalise();

                // save the encoded file
                string saveLocation = Path.Combine(directory, record.EKey.ToString());
                using (var fs = Helpers.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                    record.FileName = rootName;
                }

                return record;
            }
        }

        /// <summary>
        /// Encodes a stream using Blizzard-esque rules and saves the result to disk
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encodingMap"></param>
        /// <param name="directory"></param>
        /// <param name="rootName">Root filename</param>
        /// <returns></returns>
        public static CASRecord EncodeAndExport(Stream stream, EMap encodingMap, string directory, string rootName = null)
        {
            using (var bt = new BlockTableStreamWriter(encodingMap))
            {
                stream.Position = 0;
                stream.CopyTo(bt);
                var record = bt.Finalise();

                string saveLocation = Path.Combine(directory, record.EKey.ToString());
                using (var fs = Helpers.Create(saveLocation))
                {
                    bt.WriteTo(fs);
                    record.BLTEPath = saveLocation;
                    record.FileName = rootName;
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
        /// <param name="nameFactory">Root name generation function, input is the source filename</param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncodeAndExport(string inputDirectory, string outputDirectory, Func<string, string> nameFactory = null)
        {
            var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories);

            return BulkEncodeAndExport(files, outputDirectory, nameFactory);
        }

        /// <summary>
        /// Bulk encodes a collection of files and saves the results to disk
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="directory"></param>
        /// <param name="nameFactory">Root name generation function, input is the source filename</param>
        /// <returns></returns>
        public static IDictionary<string, CASRecord> BulkEncodeAndExport(IEnumerable<string> filenames, string directory, Func<string, string> nameFactory = null)
        {
            var resultSet = new ConcurrentDictionary<string, CASRecord>();

            Parallel.ForEach(filenames, file => resultSet.TryAdd(file, EncodeAndExport(file, directory, nameFactory?.Invoke(file))));
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
            using (var fs = Helpers.Create(filepath))
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
            using (var fs = Helpers.Create(outputPath))
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
            using (var fs = Helpers.Create(filepath))
            {
                bt.Position = 0;
                bt.CopyTo(fs);
            }

        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns a simple EncodingMap based on Blizzard-esque rules
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="filesize">Small files are ignored from compression</param>
        /// <returns></returns>
        public static EMap GetEMapFromExtension(string filename, long? filesize = null)
        {
            if (string.IsNullOrWhiteSpace(filename) || filename.IndexOf('.') == -1 || filename.EndsWith('.'))
                throw new ArgumentException("Invalid Filename");

            // not worth compressing files < 20 bytes
            if (filesize.HasValue && filesize >= 0 && filesize < 20)
                return new EMap(EType.None, 0);

            // Blizzard uses multiple blocks to compress different parts of a file AND tailors this on a per-file basis to get peek output
            //   e.g. DB2 string block will be best while the rest will be mpq (if data is repetative) or none (if not)
            // The below is simplified to a single block of the most common compression type per filetype to avoid file parsing
            // The BlockTableStreamWriter should be used if the best speed/compression ratio is required
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

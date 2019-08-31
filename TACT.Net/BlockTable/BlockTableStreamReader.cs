using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.BlockTable
{
    /// <summary>
    /// A stream reader for Block Table Encoded files 
    /// </summary>
    public sealed class BlockTableStreamReader : Stream
    {
        // Original Implementation by TOM_RUS in CASCExplorer
        // Original Source: https://github.com/WoW-Tools/CASCExplorer/blob/master/CascLib/BLTEStream.cs

        public const int BLTE_MAGIC = 0x45544C42;

        private MemoryStream memStream;
        private readonly BinaryReader reader;
        private readonly Stream stream;
        private int blockIndex;
        private long length;

        public EBlock[] EBlocks { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => memStream.Position;
            set
            {
                while (value > memStream.Length)
                    if (!ProcessNextBlock())
                        break;

                memStream.Position = value;
            }
        }

        #region Constructors

        public BlockTableStreamReader(string filename)
        {
            stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BinaryReader(stream);
            Parse();
        }

        public BlockTableStreamReader(Stream src)
        {
            stream = src ?? throw new ArgumentException(nameof(src));
            reader = new BinaryReader(src);
            Parse();
        }

        public BlockTableStreamReader(byte[] data)
        {
            stream = new MemoryStream(data);
            reader = new BinaryReader(stream);
            Parse();
        }

        #endregion

        #region Parsing

        private void Parse()
        {
            uint size = (uint)reader.BaseStream.Length;
            if (size < 8)
                throw new InvalidDataException("Stream is malformed");

            int magic = reader.ReadInt32();
            if (magic != BLTE_MAGIC)
                throw new InvalidDataException("Invalid Header");

            uint headerSize = reader.ReadUInt32BE();
            uint chunkCount = 1;

            if (headerSize > 0)
            {
                if (size < 12)
                    throw new InvalidDataException("Stream is malformed");

                byte flags = reader.ReadByte();
                chunkCount = reader.ReadUInt24BE();

                if (flags != 0xF || chunkCount == 0)
                    throw new InvalidDataException($"Bad table format 0x{flags.ToString("X2")}, numBlocks {chunkCount}");

                uint frameHeaderSize = 24 * chunkCount + 12;
                if (headerSize != frameHeaderSize)
                    throw new InvalidDataException($"Invalid Header Size");

                if (size < frameHeaderSize)
                    throw new InvalidDataException($"Stream is incomplete");
            }

            EBlocks = new EBlock[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                EBlock block = new EBlock();
                if (headerSize != 0)
                {
                    block.CompressedSize = reader.ReadUInt32BE();
                    block.DecompressedSize = reader.ReadUInt32BE();
                    block.EKey = new MD5Hash(reader.ReadBytes(16));
                }
                else
                {
                    block.CompressedSize = size - 8;
                    block.DecompressedSize = size - 8 - 1;
                }

                EBlocks[i] = block;
            }

            memStream = new MemoryStream((int)EBlocks.Sum(b => b.DecompressedSize));
            ProcessNextBlock();
            length = headerSize == 0 ? memStream.Length : memStream.Capacity;
        }

        private bool ProcessNextBlock()
        {
            if (blockIndex == EBlocks.Length)
                return false;

            long startPos = memStream.Position;
            memStream.Position = memStream.Length;

            var block = EBlocks[blockIndex];
            byte[] data = reader.ReadBytes((int)block.CompressedSize);

Process:
            block.EncodingMap.Type = (EType)data[0];
            switch (block.EncodingMap.Type)
            {
                case EType.Encrypted:
                    data = Decrypt(block, data, blockIndex);
                    goto Process;
                case EType.ZLib:
                    Decompress(block, data, memStream);
                    break;
                case EType.None:
                    memStream.Write(data, 1, data.Length - 1);
                    break;
                default:
                    throw new NotImplementedException($"Unknown BLTE block type {(char)block.EncodingMap.Type} (0x{block.EncodingMap.Type.ToString("X2")})");
            }

            blockIndex++;
            memStream.Position = startPos;
            return true;
        }

        private void Decompress(EBlock block, byte[] data, MemoryStream outStream)
        {
            // ZLib compression level
            block.EncodingMap.Level = (byte)(data[2] >> 6); // FLEVEL bits
            if (block.EncodingMap.Level > 1)
                block.EncodingMap.Level *= 3;

            // ignore EType and ZLib header
            using (var ms = new MemoryStream(data, 3, data.Length - 3))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                ds.CopyTo(outStream);
        }

        private byte[] Decrypt(EBlock block, byte[] data, int index)
        {
            byte keyNameSize = data[1];
            if (keyNameSize != 8)
                throw new Exception("Invalid KeyName size");

            block.EncryptionKeyName = BitConverter.ToUInt64(data, 2);

            // if the key doesn't exist, create an empty block with the non-compressed indicator
            if (!KeyService.TryGetKey(block.EncryptionKeyName, out byte[] key))
            {
                byte[] buffer = new byte[block.DecompressedSize + 1];
                buffer[0] = (byte)EType.None;
                return buffer;
            }                

            byte IVSize = data[keyNameSize + 2];
            if (IVSize != 4)
                throw new Exception("Invalid IV size");

            byte[] IV = new byte[8];
            Array.Copy(data, keyNameSize + 3, IV, 0, IVSize);

            for (int shift = 0, i = 0; i < 4; shift += 8, i++)
                IV[i] ^= (byte)((index >> shift) & 0xFF);

            if (data.Length < IVSize + keyNameSize + 4)
                throw new Exception("Not enough data");

            int dataOffset = keyNameSize + IVSize + 3;

            byte encType = data[dataOffset];
            if (encType != 0x53) // 'S'
                throw new NotImplementedException($"Encryption type {encType} not implemented");

            dataOffset++;

            var decryptor = KeyService.Salsa20.CreateDecryptor(key, IV);
            return decryptor.TransformFinalBlock(data, dataOffset, data.Length - dataOffset);
        }

        #endregion

        #region Methods

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (memStream.Position + count > memStream.Length && blockIndex < EBlocks.Length)
                return ProcessNextBlock() ? Read(buffer, offset, count) : 0;

            return memStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }

            return Position;
        }

        #endregion

        #region Interface Methods

        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream?.Dispose();
                reader?.Dispose();
                memStream?.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}

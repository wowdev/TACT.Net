using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.BlockTable
{
    /// <summary>
    /// A stream writer for Block Table Encoded files
    /// </summary>
    public sealed class BlockTableStreamWriter : Stream
    {
        internal IEnumerable<BlockTableSubStream> SubStreams => _blocks.Values;

        private readonly MemoryStream memStream;
        private readonly SortedList<int, BlockTableSubStream> _blocks;

        private int _curIndex = -1;

        public bool Finalised { get; private set; }
        public CASRecord Result { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => !Finalised;
        public override long Length => Finalised ? memStream.Length : _blocks[_curIndex].Length;

        public BlockTableStreamWriter(EMap blockencoding, int blockindex = -1)
        {
            memStream = new MemoryStream();
            _blocks = new SortedList<int, BlockTableSubStream>();
            AddBlock(blockencoding, blockindex);
        }

        #region Methods

        /// <summary>
        /// Adds a new block to the stream. This finalises the previous block preventing access
        /// </summary>
        /// <param name="blockencoding"></param>
        /// <param name="blockindex"></param>
        public void AddBlock(EMap blockencoding, int blockindex = -1)
        {
            // lock the previous substream
            if (_blocks.Count > 0)
                _blocks[_curIndex].Lock();

            // compute the new index
            if (blockindex == -1)
                blockindex = _blocks.Count;

            _blocks.Add(blockindex, new BlockTableSubStream(memStream, blockencoding));
            _curIndex = blockindex;
        }

        /// <summary>
        /// Finalises and encodes the stream's contents
        /// </summary>
        /// <returns></returns>
        public CASRecord Finalise()
        {
            if (Finalised)
                return Result;

            // lock the final block
            _blocks[_blocks.Count - 1].Lock();

            MD5Hash EKey, CKey = ComputeCKey();
            EMap encoding = _blocks.Count == 1 ? _blocks[0].EncodingMap : new EMap(EType.ZLib, 9);
            uint decompressedSize = 0;
            string eSpec;

            using (var md5 = MD5.Create())
            using (var ms = new MemoryStream((int)memStream.Length + 0x100))
            using (var bw = new BinaryWriter(ms))
            {
                // replace the stream contents with the BLTE structure
                uint headerSize = (uint)(_blocks.Count == 1 ? 0 : 0x18 * _blocks.Count + 0xC);

                // Header
                bw.Write(BlockTableStreamReader.BLTE_MAGIC);
                bw.WriteUInt32BE(headerSize);

                // Frame
                if (headerSize > 0)
                {
                    bw.Write((byte)0xF); // flag
                    bw.WriteUInt24BE((uint)_blocks.Count); // chunkCount

                    // EBlock meta
                    foreach (var block in _blocks.Values)
                    {
                        block.Finalise(); // apply encoding byte and any compression
                        block.Position = 0;

                        bw.WriteUInt32BE(block.CompressedSize);
                        bw.WriteUInt32BE(block.DecompressedSize);
                        bw.Write(md5.ComputeHash(block));
                        decompressedSize += block.DecompressedSize;
                    }
                }
                else
                {
                    var block = _blocks[0];
                    block.Finalise(); // apply encoding byte and any compression
                    decompressedSize = block.DecompressedSize;
                }

                foreach (var block in _blocks.Values)
                {
                    block.Position = 0;
                    block.CopyTo(ms);
                }

                // calculate the EKey
                if (headerSize == 0)
                {
                    ms.Position = 0;
                    EKey = new MD5Hash(md5.ComputeHash(ms));
                }
                else
                {
                    EKey = ms.HashSlice(md5, 0, headerSize);
                }

                // set ESpec
                eSpec = string.Join(",", _blocks.Values);

                // merge the streams
                memStream.Position = 0;
                memStream.SetLength(ms.Length);
                memStream.Capacity = (int)ms.Length;
                ms.WriteTo(memStream);

                // cleanup
                _blocks.Clear();
                Finalised = true;
            }

            // store for repeat finalisation
            return Result = new CASRecord()
            {
                CKey = CKey,
                EBlock = new EBlock()
                {
                    DecompressedSize = decompressedSize,
                    CompressedSize = (uint)memStream.Length,
                    EKey = EKey,
                    EncodingMap = encoding
                },
                ESpec = "b:{" + eSpec + "}"
            };
        }

        #endregion

        #region Interface

        public int Capacity
        {
            get => memStream.Capacity;
            set => memStream.Capacity = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Finalised)
                return memStream.Read(buffer, offset, count);

            return _blocks[_curIndex].Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Finalised)
                throw new NotSupportedException("Write not supported once finalised");

            memStream.Write(buffer, offset, count);
        }

        public void WriteTo(Stream stream) => memStream.WriteTo(stream);

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (Finalised)
                return memStream.Seek(offset, origin);

            return _blocks[_curIndex].Seek(offset, origin);
        }

        public override long Position
        {
            get => Finalised ? memStream.Position : _blocks[_curIndex].Position;
            set => (Finalised ? (Stream)memStream : _blocks[_curIndex]).Position = value;
        }

        public override void SetLength(long value)
        {
            if (Finalised)
                throw new NotSupportedException("SetLength not supported once finalised");

            _blocks[_curIndex].SetLength(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                memStream?.Dispose();
                _blocks.Clear();
            }

            base.Dispose(disposing);
        }

        public override void Flush() => memStream.Flush();

        #endregion

        #region Helpers

        private MD5Hash ComputeCKey()
        {
            // hash the substreams in order
            using (var md5 = MD5.Create())
            {
                foreach (var block in _blocks.Values)
                    block.Hash(md5);
                md5.TransformFinalBlock(new byte[0], 0, 0);
                return new MD5Hash(md5.Hash);
            }
        }

        #endregion
    }
}

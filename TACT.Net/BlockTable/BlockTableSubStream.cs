using System;
using System.IO;
using System.Security.Cryptography;
using Joveler.Compression.ZLib;
using TACT.Net.Common;
using TACT.Net.Common.ZLib;

namespace TACT.Net.BlockTable
{
    internal class BlockTableSubStream : Stream
    {
        public EMap EncodingMap { get; private set; }
        public uint DecompressedSize { get; private set; }
        public uint CompressedSize { get; private set; }

        private readonly MemoryStream _innerStream;
        private readonly long _startPos;
        private bool _finalised;
        private bool _locked;

        public BlockTableSubStream(MemoryStream inner, EMap map)
        {
            _innerStream = inner;
            _startPos = inner.Position;
            EncodingMap = map;
        }

        #region Interface Methods

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite && !_locked && !_finalised;

        public override long Length
        {
            get
            {
                if (_finalised)
                    return CompressedSize;
                if (_locked)
                    return DecompressedSize;
                return _innerStream.Length - _startPos;
            }
        }
        public override long Position
        {
            get => _innerStream.Position - _startPos;
            set => _innerStream.Position = Math.Max(value + _startPos, _startPos);
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            //_innerStream.Seek(offset + _startPos, origin) - _startPos;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return Position = offset;
                case SeekOrigin.Current:
                    return Position += offset;
                case SeekOrigin.End:
                    return Position = Length + offset;
                default:
                    return Position;
            }
        }
        public override void SetLength(long value) => _innerStream.SetLength(Math.Max(_startPos, _startPos + value));
        public override void Flush() => _innerStream.Flush();

        #endregion

        #region Methods

        public override int Read(byte[] buffer, int offset, int count)
        {
            // prevent overflowing into the next substream
            // account for the encoding type byte not being part of the stream
            long position = Position + (_finalised ? 1 : 0);
            if (position + count > Length)
                count = (int)(Length - position);

            if (count <= 0)
                return 0;

            // prefix the encoding type if finalised and at start
            if (_finalised && position == 1)
            {
                buffer[0] = (byte)EncodingMap.Type;
                return _innerStream.Read(buffer, offset + 1, count - 1) + 1;
            }

            return _innerStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Finalises the stream and applies any compression
        /// </summary>
        public void Finalise()
        {
            if (_finalised)
                return;

            if (EncodingMap.Type == EType.ZLib)
            {
                var writeType = EncodingMap.MPQ ? 0 : ZLibWriteType.ZLib;
                using (var zlib = ZLibFactory.CreateStream(this, ZLibMode.Compress, (ZLibCompLevel)EncodingMap.Level, writeType, true))
                {
                    zlib.WriteBasestream();
                    CompressedSize = (uint)zlib.TotalOut + 1;
                }
            }

            _finalised = true;
        }

        /// <summary>
        /// Prevents write access
        /// </summary>
        public void Lock()
        {
            // set lengths
            DecompressedSize = (uint)Length;
            CompressedSize = DecompressedSize + 1; // factor encoding byte

            _locked = true;

            // set the position for next block
            _innerStream.Position = _innerStream.Length;

            // buffer to the compress bound for "worse case" compressions
            // https://www.zlib.net/zlib_tech.html - Maximum Expansion Factor
            if (EncodingMap.Type == EType.ZLib)
                _innerStream.Position += (DecompressedSize >> 12) + (DecompressedSize >> 14) + (DecompressedSize >> 25) + 13;
        }

        /// <summary>
        /// Hashes the stream using block transforms and buffering
        /// </summary>
        /// <param name="md5"></param>
        public void Hash(MD5 md5)
        {
            if (_finalised)
                return;

            Position = 0;

            // pre-LOH magic number
            byte[] buffer = new byte[81920];
            int read;
            while ((read = Read(buffer, 0, buffer.Length)) != 0)
                md5.TransformBlock(buffer, 0, read, null, 0);
        }

        #endregion

        #region Misc/Unimplemented

        protected override void Dispose(bool disposing) { /* prevented */ }

        public override void Close() { /* prevented */ }

        /// <summary>
        /// Returns an ESpec representation of the stream
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string spec;

            // 256K* is the max that Blizzard documents
            if (CompressedSize >= 1024 * 256)
                spec = "256K*=";
            // closest floored KB + greedy
            else if (CompressedSize > 1024)
                spec = (int)Math.Floor(CompressedSize / 1024d) + "K*=";
            // actual size + greedy
            else
                spec = CompressedSize + "*=";

            spec += EncodingMap.ToString();

            return spec.ToLowerInvariant();
        }

        #endregion


    }
}

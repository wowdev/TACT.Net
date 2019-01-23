using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TACT.Net.Common.ZLib
{
    public sealed class ZLibStream : Stream
    {
        #region Fields

        const int BufferSize = 81920;

        public long TotalIn { get; private set; } = 0;
        public long TotalOut { get; private set; } = 0;
        public Stream BaseStream { get; }

        private readonly ZLibMode _mode;
        private readonly ZLibOpenType _openType = ZLibOpenType.ZLib;
        private readonly ZLibWriteType _writeType = ZLibWriteType.ZLib;
        private readonly bool _leaveOpen;
        private bool _disposed = false;
        private ZStream _zstream;
        private GCHandle _zstreamPtr;

        private readonly byte[] _internalBuf;
        private int _internalBufPos = 0;

        #endregion

        #region Constructors

        public ZLibStream(Stream stream, ZLibMode mode, ZLibCompLevel level, ZLibWriteType writeType, bool leaveOpen = false) : this(stream, mode, level, leaveOpen)
        {
            _writeType = writeType;
        }
        public ZLibStream(Stream stream, ZLibMode mode, ZLibCompLevel level, ZLibOpenType openType, bool leaveOpen = false) : this(stream, mode, level, leaveOpen)
        {
            _openType = openType;
        }
        public ZLibStream(Stream stream, ZLibMode mode, ZLibCompLevel level, bool leaveOpen = false)
        {
            BaseStream = stream;

            _zstream = new ZStream();
            _zstreamPtr = GCHandle.Alloc(_zstream, GCHandleType.Pinned);

            _leaveOpen = leaveOpen;
            _mode = mode;
            _internalBufPos = 0;
            _internalBuf = new byte[BufferSize];

            ZLibReturnCode ret;
            if (_mode == ZLibMode.Compress)
                ret = ZLibNative.DeflateInit(_zstream, level, _writeType);
            else
                ret = ZLibNative.InflateInit(_zstream, _openType);

            if (ret != ZLibReturnCode.OK)
                throw new Exception(ret + " " + _zstream.LastErrorMsg);
        }

        #endregion

        #region Disposable Pattern
        ~ZLibStream()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (BaseStream != null)
                {
                    if (_mode == ZLibMode.Compress)
                        Flush();

                    if (!_leaveOpen)
                        BaseStream.Close();
                }

                if (_zstream != null)
                {
                    if (_mode == ZLibMode.Compress)
                        ZLibNative.DeflateEnd(_zstream);
                    else
                        ZLibNative.InflateEnd(_zstream);

                    _zstreamPtr.Free();
                    _zstream = null;
                }

                _disposed = true;
            }
        }
        #endregion

        #region ValidateReadWriteArgs

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateReadWriteArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        #endregion

        #region Stream Methods

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_mode != ZLibMode.Decompress)
                throw new NotSupportedException("Read not supported on compression");

            ValidateReadWriteArgs(buffer, offset, count);

            int readLen = 0;
            if (_internalBufPos != -1)
            {
                using (PinnedArray pinRead = new PinnedArray(_internalBuf)) // [In] Compressed
                using (PinnedArray pinWrite = new PinnedArray(buffer)) // [Out] Will-be-decompressed
                {
                    _zstream.NextIn = pinRead[_internalBufPos];
                    _zstream.NextOut = pinWrite[offset];
                    _zstream.AvailOut = (uint)count;

                    while (0 < _zstream.AvailOut)
                    {
                        if (_zstream.AvailIn == 0)
                        {
                            // Compressed Data is no longer available in array, so read more from _stream
                            int baseReadSize = BaseStream.Read(_internalBuf, 0, _internalBuf.Length);

                            _internalBufPos = 0;
                            _zstream.NextIn = pinRead;
                            _zstream.AvailIn = (uint)baseReadSize;
                            TotalIn += baseReadSize;
                        }

                        uint inCount = _zstream.AvailIn;
                        uint outCount = _zstream.AvailOut;

                        // flush method for inflate has no effect
                        ZLibReturnCode ret = ZLibNative.Inflate(_zstream, ZLibFlush.NO_FLUSH);

                        _internalBufPos += (int)(inCount - _zstream.AvailIn);
                        readLen += (int)(outCount - _zstream.AvailOut);

                        if (ret == ZLibReturnCode.STREAM_END)
                        {
                            _internalBufPos = -1; // magic for StreamEnd
                            break;
                        }

                        if (ret != ZLibReturnCode.OK)
                            throw new Exception(ret + " " + _zstream.LastErrorMsg);
                    }

                    TotalOut += readLen;
                }
            }
            return readLen;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_mode != ZLibMode.Compress)
                throw new NotSupportedException("Write not supported on decompression");

            TotalIn += count;

            using (PinnedArray pinRead = new PinnedArray(buffer))
            using (PinnedArray pinWrite = new PinnedArray(_internalBuf))
            {
                _zstream.NextIn = pinRead[offset];
                _zstream.AvailIn = (uint)count;
                _zstream.NextOut = pinWrite[_internalBufPos];
                _zstream.AvailOut = (uint)(_internalBuf.Length - _internalBufPos);

                while (_zstream.AvailIn != 0)
                {
                    uint outCount = _zstream.AvailOut;
                    ZLibReturnCode ret = ZLibNative.Deflate(_zstream, ZLibFlush.NO_FLUSH);
                    _internalBufPos += (int)(outCount - _zstream.AvailOut);

                    if (_zstream.AvailOut == 0)
                    {
                        BaseStream.Write(_internalBuf, 0, _internalBuf.Length);
                        TotalOut += _internalBuf.Length;

                        _internalBufPos = 0;
                        _zstream.NextOut = pinWrite;
                        _zstream.AvailOut = (uint)_internalBuf.Length;
                    }

                    if (ret != ZLibReturnCode.OK)
                        throw new Exception(ret + " " + _zstream.LastErrorMsg);
                }
            }
        }

        /// <summary>
        /// Compresses and overrides the BaseStream
        /// </summary>
        /// <param name="offset"></param>
        public void WriteBasestream(long offset = 0)
        {
            BaseStream.Position = 0;

            // the largest multiple of 4096 smaller than the LOH threshold
            byte[] buffer = new byte[81920];

            // calculate the read position delta
            offset -= TotalIn;

            // reading in chunks and jumping stream position is faster and allocates less with large streams
            int read;
            while ((read = BaseStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                // jump to the write position
                BaseStream.Position = offset + TotalOut;
                Write(buffer, 0, read);

                // reset to the next read position
                BaseStream.Position = offset + TotalIn;
            }

            // jump to the final write position and flush the buffer
            BaseStream.Position = offset + TotalOut;
            Flush();
        }


        public override void Flush()
        {
            if (_mode == ZLibMode.Decompress)
            {
                BaseStream.Flush();
                return;
            }

            using (PinnedArray pinWrite = new PinnedArray(_internalBuf))
            {
                _zstream.NextIn = IntPtr.Zero;
                _zstream.AvailIn = 0;
                _zstream.NextOut = pinWrite[_internalBufPos];
                _zstream.AvailOut = (uint)(_internalBuf.Length - _internalBufPos);

                ZLibReturnCode ret = ZLibReturnCode.OK;
                while (ret != ZLibReturnCode.STREAM_END)
                {
                    if (_zstream.AvailOut != 0)
                    {
                        uint outCount = _zstream.AvailOut;
                        ret = ZLibNative.Deflate(_zstream, ZLibFlush.FINISH);

                        _internalBufPos += (int)(outCount - _zstream.AvailOut);

                        if (ret != ZLibReturnCode.STREAM_END && ret != ZLibReturnCode.OK)
                            throw new Exception(ret + " " + _zstream.LastErrorMsg);
                    }

                    BaseStream.Write(_internalBuf, 0, _internalBufPos);
                    TotalOut += _internalBufPos;

                    _internalBufPos = 0;
                    _zstream.NextOut = pinWrite;
                    _zstream.AvailOut = (uint)_internalBuf.Length;
                }
            }

            BaseStream.Flush();
        }

        public override bool CanRead => _mode == ZLibMode.Decompress && BaseStream.CanRead;
        public override bool CanWrite => _mode == ZLibMode.Compress && BaseStream.CanWrite;
        public override bool CanSeek => false;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public double CompressionRatio
        {
            get
            {
                if (_mode == ZLibMode.Compress)
                {
                    if (TotalIn != 0)
                        return 100 - TotalOut * 100.0 / TotalIn;
                    return 0;
                }
                else
                {
                    if (TotalOut != 0)
                        return 100 - TotalIn * 100.0 / TotalOut;
                    return 0;
                }
            }
        }

        #endregion

        private class PinnedArray : IDisposable
        {
            public IntPtr Ptr => _hArray.AddrOfPinnedObject();
            public Array Array;

            public IntPtr this[int idx] => Marshal.UnsafeAddrOfPinnedArrayElement(Array, idx);
            public static implicit operator IntPtr(PinnedArray fixedArray) => fixedArray[0];

            private GCHandle _hArray;

            public PinnedArray(Array array)
            {
                Array = array;
                _hArray = GCHandle.Alloc(array, GCHandleType.Pinned);
            }

            ~PinnedArray()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hArray.IsAllocated)
                        _hArray.Free();
                }
            }
        }
    }
}

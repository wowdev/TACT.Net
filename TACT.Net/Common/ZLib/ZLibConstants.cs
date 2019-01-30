using System;
using System.Runtime.InteropServices;

namespace TACT.Net.Common.ZLib
{
    #region Enums
    internal enum ZLibFlush : int
    {
        NO_FLUSH = 0,
        PARTIAL_FLUSH = 1,
        SYNC_FLUSH = 2,
        FULL_FLUSH = 3,
        FINISH = 4,
        BLOCK = 5,
        TREES = 6,
    }

    /// <summary>
    /// Return codes for the compression/decompression functions.
    /// Negative values are errors, positive values are used for special but normal events.
    /// </summary>
    internal enum ZLibReturnCode
    {
        OK = 0,
        STREAM_END = 1,
        NEED_DICTIONARY = 2,
        ERRNO = -1,
        STREAM_ERROR = -2,
        DATA_ERROR = -3,
        MEMORY_ERROR = -4,
        BUFFER_ERROR = -5,
        VERSION_ERROR = -6,
    }

    internal enum ZLibCompLevel : int
    {
        Default = -1,
        NoCompression = 0,
        BestSpeed = 1,
        BestCompression = 9,
        Level0 = 0,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5,
        Level6 = 6,
        Level7 = 7,
        Level8 = 8,
        Level9 = 9,
    }

    internal enum ZLibCompressionStrategy : int
    {
        FLITERED = 1,
        HUFFMAN_ONLY = 2,
        RLE = 3,
        FIXED = 4,
        DEFAULT_STRATEGY = 0,
    }

    /// <summary>
    /// The deflate compression method (the only one supported in this version)
    /// </summary>
    internal enum ZLibCompMethod : int
    {
        DEFLATED = 8,
    }

    internal enum ZLibOpenType : int
    {
        // If a compressed stream with a larger window
        // size is given as input, inflate() will return with the error code
        // Z_DATA_ERROR instead of trying to allocate a larger window.
        Deflate = -15, // -8..-15
        MPQ = 0,
        ZLib = 15, // 8..15, 0 = use the window size in the zlib header of the compressed stream.
    }

    /// <summary>
    /// WindowBits
    /// </summary>
    internal enum ZLibWriteType : int
    {
        // If a compressed stream with a larger window
        // size is given as input, inflate() will return with the error code
        // Z_DATA_ERROR instead of trying to allocate a larger window.
        Deflate = -15, // -8..-15
        MPQ = 0,
        ZLib = 15, // 8..15, 0 = use the window size in the zlib header of the compressed stream.
    }

    internal enum ZLibMode
    {
        Compress,
        Decompress,
    }
    #endregion

    #region ZStream
    [StructLayout(LayoutKind.Sequential)]
    internal class ZStream
    {
#pragma warning disable 169
#pragma warning disable IDE0044
        /// <summary>
        /// next input byte
        /// </summary>
        public IntPtr NextIn = IntPtr.Zero;
        /// <summary>
        /// number of bytes available at next_in
        /// </summary>
        public uint AvailIn;
        /// <summary>
        /// total number of input bytes read so far
        /// </summary>
        public uint TotalIn;

        /// <summary>
        /// next output byte will go here
        /// </summary>
        public IntPtr NextOut = IntPtr.Zero;
        /// <summary>
        /// remaining free space at next_out
        /// </summary>
        public uint AvailOut;
        /// <summary>
        /// total number of bytes output so far
        /// </summary>
        public uint TotalOut;

        private IntPtr Msg = IntPtr.Zero;
        /// <summary>
        /// last error message, NULL if no error
        /// </summary>
        public string LastErrorMsg => Marshal.PtrToStringAnsi(Msg);
        /// <summary>
        /// not visible by applications
        /// </summary>
        private IntPtr State = IntPtr.Zero;

        /// <summary>
        /// used to allocate the internal state
        /// </summary>
        private IntPtr ZAlloc = IntPtr.Zero;
        /// <summary>
        /// used to free the internal state
        /// </summary>
        private IntPtr ZFree = IntPtr.Zero;
        /// <summary>
        /// private data object passed to zalloc and zfree
        /// </summary>
        private IntPtr Opaque = IntPtr.Zero;

        /// <summary>
        /// best guess about the data type: binary or text for deflate, or the decoding state for inflate
        /// </summary>
        public int DataType;
        /// <summary>
        /// Adler-32 or CRC-32 value of the uncompressed data
        /// </summary>
        public uint Adler;
        /// <summary>
        /// reserved for future use
        /// </summary>
        private uint Reserved;
#pragma warning restore 169
#pragma warning restore IDE0044
    }
    #endregion
}

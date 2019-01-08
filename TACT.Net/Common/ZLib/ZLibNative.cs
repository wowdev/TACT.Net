using System.IO;
using System.Runtime.InteropServices;

namespace TACT.Net.Common.ZLib
{
    internal static class ZLibNative
    {
        public const int DEF_MEM_LEVEL = 8;
        public const string ZLIB_VERSION = "1.2.11"; // structs from zlib 1.2.11's zlib.h

        static ZLibNative()
        {
            // copy the clrcompression dll locally
            string executionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string clrcompressionPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "clrcompression.dll");
            string dllCopyPath = Path.Combine(executionPath, "clrcompression.dll");

            if (!File.Exists(dllCopyPath))
                File.Copy(clrcompressionPath, dllCopyPath, true);
        }

        #region Structs

        [DllImport("clrcompression", EntryPoint = "deflateInit2_")]
        internal static extern ZLibReturnCode DeflateInit2(
            ZStream strm,
            ZLibCompLevel level,
            ZLibCompMethod method,
            ZLibWriteType windowBits,
            int memLevel,
            ZLibCompressionStrategy strategy,
            [MarshalAs(UnmanagedType.LPStr)] string version,
            int stream_size);

        [DllImport("clrcompression", EntryPoint = "deflate")]
        internal static extern ZLibReturnCode Deflate(ZStream strm, ZLibFlush flush);

        [DllImport("clrcompression", EntryPoint = "deflateEnd")]
        internal static extern ZLibReturnCode DeflateEnd(ZStream strm);


        [DllImport("clrcompression", EntryPoint = "inflateInit2_")]
        internal static extern ZLibReturnCode InflateInit2(ZStream strm, ZLibOpenType windowBits, [MarshalAs(UnmanagedType.LPStr)] string version, int stream_size);

        [DllImport("clrcompression", EntryPoint = "inflate")]
        internal static extern ZLibReturnCode Inflate(ZStream strm, ZLibFlush flush);

        [DllImport("clrcompression", EntryPoint = "inflateEnd")]
        internal static extern ZLibReturnCode InflateEnd(ZStream strm);

        #endregion

        #region Helpers

        internal static ZLibReturnCode DeflateInit(ZStream stream, ZLibCompLevel level, ZLibWriteType windowBits)
        {
            return DeflateInit2(stream, level, ZLibCompMethod.DEFLATED, windowBits, DEF_MEM_LEVEL, ZLibCompressionStrategy.DEFAULT_STRATEGY, ZLIB_VERSION, Marshal.SizeOf(typeof(ZStream)));
        }

        internal static ZLibReturnCode InflateInit(ZStream stream, ZLibOpenType windowBits)
        {
            return InflateInit2(stream, windowBits, ZLIB_VERSION, Marshal.SizeOf(typeof(ZStream)));
        }

        #endregion
    }
}

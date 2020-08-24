using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Joveler.Compression.ZLib;

namespace TACT.Net.Common.ZLib
{
    internal class ZLibFactory
    {
        static ZLibFactory() => NativeGlobalInit();
        ~ZLibFactory() => ZLibInit.GlobalCleanup();


        public static ZLibStream CreateStream(Stream stream, ZLibMode mode, ZLibCompLevel level, ZLibWriteType writeType = ZLibWriteType.ZLib, bool leaveOpen = false)
        {
            return writeType switch
            {
                // 0 windowBits - inflate uses the window size in the zlib header
                0 => new ZLibMPQStream(stream, mode, level, leaveOpen),
                // 15 windowBits
                _ => new ZLibStream(stream, mode, level, leaveOpen),
            };
        }

        private static void NativeGlobalInit()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                throw new PlatformNotSupportedException();

            string libName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zlibwapi.dll" : "libz.so";

            string zlibPath = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x64", libName),
                Architecture.X86 => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x86", libName),
                Architecture.Arm => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "armhf", libName),
                Architecture.Arm64 => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "arm64", libName),
                _ => throw new PlatformNotSupportedException(),
            };

            ZLibInit.GlobalInit(zlibPath, 64 * 1024);
        }
    }
}

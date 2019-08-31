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
            switch (writeType)
            {
                // 0 windowBits - inflate uses the window size in the zlib header
                case 0:
                    return new ZLibMPQStream(stream, mode, level, leaveOpen);
                // 15 windowBits
                default:
                    return new ZLibStream(stream, mode, level, leaveOpen);
            }
        }

        private static void NativeGlobalInit()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                throw new PlatformNotSupportedException();

            string zlibPath = null;
            string libName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zlibwapi.dll" : "libz.so";

            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    zlibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x64", libName);
                    break;
                case Architecture.X86:
                    zlibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x86", libName);
                    break;
                case Architecture.Arm:
                    zlibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "armhf", libName);
                    break;
                case Architecture.Arm64:
                    zlibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "arm64", libName);
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            ZLibInit.GlobalInit(zlibPath, 64 * 1024);
        }
    }
}

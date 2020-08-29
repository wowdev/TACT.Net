using System;
using System.ComponentModel.DataAnnotations;
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


        public static ZLibStream CreateStream(Stream stream, ZLibCompLevel level, ZLibWindowBits windowBits = ZLibWindowBits.Default, bool leaveOpen = false)
        {
            var options = new ZLibCompressOptions()
            {
                Level = level,
                WindowBits = windowBits,
                LeaveOpen = leaveOpen
            };

            return new ZLibStream(stream, options);
        }

        private static void NativeGlobalInit()
        {
            var directory = Path.Combine(Environment.CurrentDirectory, "runtimes");
            var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ZLibInit.GlobalInit(Path.Combine(directory, "win-" + arch, "native", "zlibwapi.dll"));
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                ZLibInit.GlobalInit(Path.Combine(directory, "osx-" + arch, "native", "libz.dylib")); // TODO should this use system installed?
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                ZLibInit.GlobalInit(); // Linux binaries are not portable            
        }
    }
}

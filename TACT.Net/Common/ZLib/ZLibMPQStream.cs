using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Joveler.Compression.ZLib;

namespace TACT.Net.Common.ZLib
{
    internal class ZLibMPQStream : ZLibStream
    {
        public ZLibMPQStream(Stream stream, ZLibMode mode)
            : base(stream, mode) { }

        public ZLibMPQStream(Stream stream, ZLibMode mode, bool leaveOpen) :
            base(stream, mode, leaveOpen)
        { }

        public ZLibMPQStream(Stream stream, ZLibMode mode, ZLibCompLevel level) :
            base(stream, mode, level)
        { }

        public ZLibMPQStream(Stream stream, ZLibMode mode, ZLibCompLevel level, bool leaveOpen) :
            base(stream, mode, level, leaveOpen)
        { }

        protected override ZLibOpenType OpenType => 0;
        protected override ZLibWriteType WriteType => 0;
    }
}

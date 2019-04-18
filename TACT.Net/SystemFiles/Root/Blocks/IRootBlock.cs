using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TACT.Net.Root;

namespace TACT.Net.Root.Blocks
{
    public interface IRootBlock
    {
        ContentFlags ContentFlags { get; set; }
        LocaleFlags LocaleFlags { get; set; }
        Dictionary<uint, RootRecord> Records { get; set; }

        void Read(BinaryReader br);
        void Write(BinaryWriter bw);
    }
}

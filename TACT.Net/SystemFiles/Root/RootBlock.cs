using System.Collections.Generic;

namespace TACT.Net.Root
{
    public class RootBlock
    {
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
        public Dictionary<ulong, RootRecord> Records;
    }
}

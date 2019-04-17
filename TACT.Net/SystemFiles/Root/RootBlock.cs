using System.Collections.Generic;

namespace TACT.Net.Root
{
    public class RootBlock
    {
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
        public Dictionary<uint, RootRecord> Records;

        internal bool HasNameHash => (ContentFlags & ContentFlags.NoNameHash) != ContentFlags.NoNameHash;
    }
}

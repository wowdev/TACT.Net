using System;

namespace TACT.Net.Root
{
    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        LowViolence = 0x80, // many models have this flag
        Bundle = 0x40000000,
        NoCompression = 0x80000000, // sounds have this flag
    }
}

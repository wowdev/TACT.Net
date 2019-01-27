using System;

namespace TACT.Net.Indices
{
    [Flags]
    public enum IndexType
    {
        Unknown = 0,
        Loose = 1,
        Data = 2,
        Patch = 4,
        Group = 8
    }
}

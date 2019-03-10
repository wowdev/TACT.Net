using System;

namespace TACT.Net.Root
{
    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        Windows = 0x8, // HLSL shaders + updateplugin.dll
        Mac = 0x10, // MTL shaders + updateplugin.dylib
        LowViolence = 0x80, // many models have this flag
        Unknown0x800 = 0x800, // updateplugin.dll + updateplugin.dylib
        Bundle = 0x40000000,
        NoCompression = 0x80000000, // sounds have this flag
    }
}

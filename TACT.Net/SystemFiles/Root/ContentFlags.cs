using System;

namespace TACT.Net.Root
{
    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        /// <summary>
        /// e.g. HLSL shaders
        /// </summary>
        Windows = 0x8,
        /// <summary>
        /// e.g. MTL shaders
        /// </summary>
        Mac = 0x10,
        /// <summary>
        /// Many models have this flag
        /// </summary>
        LowViolence = 0x80,
        /// <summary>
        /// UpdatePlugin.dll + UpdatePlugin.dylib
        /// </summary>
        Unknown_800 = 0x800,
        /// <summary>
        /// Files require a TACT key to be viewed
        /// </summary>
        Encrypted = 0x8000000,
        /// <summary>
        /// Files do not have a namehash
        /// </summary>
        NoNameHash = 0x10000000,
        /// <summary>
        /// All non-1280px wide Cinematics have this
        /// </summary>
        UncommonResolution = 0x20000000,
        /// <summary>
        /// 
        /// </summary>
        Bundle = 0x40000000,
        /// <summary>
        /// Sounds have this flag
        /// </summary>
        Uncompressed = 0x80000000,
    }
}

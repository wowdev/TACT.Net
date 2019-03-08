namespace TACT.Net.Common
{
    /// <summary>
    /// Big Endian helper class
    /// </summary>
    internal static class Endian
    {
        public static ushort SwapUInt16(ushort v) => (ushort)(((v & 0xFF) << 8) | ((v >> 8) & 0xFF));

        public static uint SwapUInt24(byte[] b) => (uint)(b[0] << 16 | b[1] << 8 | b[2]);

        public static uint SwapUInt32(uint v) => (uint)(((SwapUInt16((ushort)v) & 0xFFFF) << 0x10) | (SwapUInt16((ushort)(v >> 0x10)) & 0xFFFF));

        public static ulong SwapUInt40(byte[] b) => (ulong)(b[0] << 32 | b[1] << 24 | b[2] << 16 | b[3] << 8 | b[4]);

        public static ulong SwapUInt64(ulong v) => (ulong)(((SwapUInt32((uint)v) & 0xFFFFFFFFL) << 0x20) | (SwapUInt32((uint)(v >> 0x20)) & 0xFFFFFFFFL));

        public static ulong SwapUInt64(byte[] b)
        {
            ulong i1 = (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
            uint i2 = (uint)((b[4] << 24) | (b[5] << 16) | (b[6] << 8) | b[7]);
            return (i1 << 32) | i2;
        }
    }
}

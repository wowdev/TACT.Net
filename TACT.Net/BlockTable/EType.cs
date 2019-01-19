namespace TACT.Net.BlockTable
{
    /// <summary>
    /// Encoding Type
    /// </summary>
    public enum EType : byte
    {
        Encrypted = 0x45,
        Frame = 0x46,
        ZLib = 0x5A,
        None = 0x4E
    }
}

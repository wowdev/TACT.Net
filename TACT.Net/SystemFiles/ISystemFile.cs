using TACT.Net.Cryptography;

namespace TACT.Net
{
    public interface ISystemFile
    {
        MD5Hash Checksum { get; }
    }
}

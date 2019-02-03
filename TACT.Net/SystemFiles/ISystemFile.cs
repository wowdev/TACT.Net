using TACT.Net.Cryptography;

namespace TACT.Net.SystemFiles
{
    public interface ISystemFile
    {
        MD5Hash Checksum { get; }
    }
}

using TACT.Net.Cryptography;

namespace TACT.Net
{
    public interface ISystemFile
    {
        MD5Hash Checksum { get; }
        string FilePath { get; }

        void AddOrUpdate(CASRecord record, TACTRepo repo = null);
        CASRecord Write(string directory, TACTRepo tactRepo = null);
    }
}

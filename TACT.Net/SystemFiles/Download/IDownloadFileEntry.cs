using TACT.Net.Cryptography;

namespace TACT.Net.Download
{
    public interface IDownloadFileEntry
    {
        MD5Hash EKey { get; set; }
    }
}

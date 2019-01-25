using TACT.Net.Common.Cryptography;

namespace TACT.Net.Download
{
    public interface IDownloadFileEntry
    {
        MD5Hash EKey { get; set; }
    }
}

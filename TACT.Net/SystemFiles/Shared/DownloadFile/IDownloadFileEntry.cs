using TACT.Net.Common.Cryptography;

namespace TACT.Net.Shared.DownloadFile
{
    public interface IDownloadFileEntry
    {
        MD5Hash EKey { get; set; }
    }
}

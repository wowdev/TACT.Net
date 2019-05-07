namespace TACT.Net.FileLookup
{
    public interface IFileLookup
    {
        bool IsLoaded { get; }

        /// <summary>
        /// Opens the FileLookup and loads its contents
        /// </summary>
        void Open();

        /// <summary>
        /// Saves the FileLookup to it's backing storage
        /// </summary>
        void Close();

        /// <summary>
        /// Returns the fileid associated to the specified filename
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        uint GetOrCreateFileId(string filename);
    }
}

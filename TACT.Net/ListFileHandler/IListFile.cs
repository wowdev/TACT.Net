namespace TACT.Net.ListFileHandler
{
    public interface IListFile
    {
        void Open();
        void Close();

        uint GetOrCreateFileId(string filename);
    }
}

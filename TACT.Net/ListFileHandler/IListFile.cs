using System;
using System.Collections.Generic;
using System.Text;

namespace TACT.Net.ListFileHandler
{
    public interface IListFile
    {
        void Open();
        void Close();

        uint GetOrCreateFileId(string filename);
    }
}

using System.IO;
using System.Linq;

namespace TACT.Net.Common
{
    internal static class Helpers
    {
        /// <summary>
        /// Returns the Blizzard CDN file path format. Optionally creates the directories
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="folder"></param>
        /// <param name="directory"></param>
        /// <param name="create"></param>
        /// <returns></returns>
        public static string GetCDNPath(string filename, string folder = "", string directory = "", bool create = false, bool url = false)
        {
            string dir = Path.Combine(directory, "tpr", "wow", folder, filename.Substring(0, 2), filename.Substring(2, 2));
            if (create)
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, filename);

            if (url)
                path = path.Replace('\\', '/');

            return path;
        }

        /// <summary>
        /// Creates a new file with sharing enabled
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static FileStream Create(string filename)
        {
            return new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        /// <summary>
        /// Determines a file exists before attempting to delete
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="deleteParentFolder"></param>
        public static void Delete(string filename, bool deleteParentFolder = false)
        {
            if (File.Exists(filename))
                File.Delete(filename);

            string folderPath = Path.GetDirectoryName(filename);

            if (!Directory.EnumerateFileSystemEntries(folderPath).Any())
            {
                Directory.Delete(folderPath);

                string parentFolder = Directory.GetParent(folderPath).FullName;
                if (!Directory.EnumerateFileSystemEntries(parentFolder).Any())
                {
                    Directory.Delete(parentFolder);
                }
            }
        }

        /// <summary>
        /// Delete if file and if folder is empty the folder and parent
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="directory"></param>
        /// <param name="folder"></param>
        public static void Delete(string filename, string directory, string folder = "data")
        {
            string filePath = GetCDNPath(filename, folder, directory);
            Delete(filePath, true);
        }

        /// <summary>
        /// Determines if a filepath contains a specific directory
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static bool PathContainsDirectory(string filepath, string directory)
        {
            return filepath.Split(Path.DirectorySeparatorChar).IndexOf(directory) != -1;
        }
    }
}

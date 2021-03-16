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
        public static string GetCDNPath(string filename, string folder = "", string directory = "", bool create = false)
        {
            string dir = Path.Combine(directory, folder, filename[..2], filename[2..4]);
            if (create)
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, filename);
        }

        /// <summary>
        /// Returns the Blizzard CDN Url path for a file.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="folder"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static string GetCDNUrl(string filename, string folder)
        {
            return string.Join("/", "tpr", "wow", folder, filename[..2], filename[2..4], filename);
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
        public static void Delete(string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);

            string folderPath = Path.GetDirectoryName(filename);

            if (Directory.Exists(folderPath) && !Directory.EnumerateFileSystemEntries(folderPath).Any())
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
            var filePath = GetCDNPath(filename, folder, directory);
            Delete(filePath);
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

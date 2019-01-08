using System;
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
            string dir = Path.Combine(directory, "tpr", "wow", folder, filename.Substring(0, 2), filename.Substring(2, 2));
            if (create)
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, filename);
        }

        /// <summary>
        /// Determines the file exists before attempting to delete
        /// </summary>
        /// <param name="filename"></param>
        public static void Delete(string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);
        }

        /// <summary>
        /// Determines if a filepath contains a specific directory
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static bool PathContainsDirectory(string filepath, string directory)
        {
            return filepath.Split(Path.DirectorySeparatorChar).Contains(directory, StringComparer.OrdinalIgnoreCase);
        }
    }
}

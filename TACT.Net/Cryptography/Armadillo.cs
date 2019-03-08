using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TACT.Net.Common;

namespace TACT.Net.Cryptography
{
    /// <summary>
    /// Armadillo is Blizzard's repository encryption implementation
    /// <para>See https://wowdev.wiki/TACT#Armadillo</para>
    /// </summary>
    public sealed class Armadillo
    {
        public byte[] Key { get; private set; }

        private readonly Salsa20 Salsa20;
        private readonly string AppDataPath;

        #region Constructors

        public Armadillo()
        {
            Salsa20 = new Salsa20();
            AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "battle.net", "armadillo");
        }

        #endregion

        #region Methods

        /// <summary>
        /// Attempts to load an Armadillo key. Supports both filepaths and Battle.Net key directory lookups
        /// </summary>
        /// <param name="filePathOrKeyName"></param>
        /// <returns></returns>
        public bool SetKey(string filePathOrKeyName)
        {
            // check if the full path is provided otherwise
            // fallback to the battle.net app's data directory
            string filepath = filePathOrKeyName;

            if (!File.Exists(filePathOrKeyName))
                filepath = Path.Combine(AppDataPath, Path.ChangeExtension(filePathOrKeyName, ".ak"));
            if (!File.Exists(filepath))
                return false;

            using (var fs = File.OpenRead(filepath))
            using (var br = new BinaryReader(fs))
            using (var md5 = MD5.Create())
            {
                // invalid size
                if (fs.Length != 20)
                    return false;

                // read the key
                byte[] key = br.ReadBytes(0x10);

                // validate the file's checksum - first 4 bytes of MD5(key)
                if (br.ReadUInt32() != BitConverter.ToUInt32(md5.ComputeHash(key), 0))
                    return false;

                Key = key;
                return true;
            }
        }

        /// <summary>
        /// Decrypts a local file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public Stream Decrypt(string filename)
        {
            using (var fs = File.OpenRead(filename))
                return Decrypt(filename, fs);
        }

        /// <summary>
        /// Decrypts a stream
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public Stream Decrypt(string filename, Stream stream)
        {
            filename = Path.GetFileNameWithoutExtension(filename);

            if (!IsValidName(filename))
                throw new ArgumentException("Name should be a CDN hash");

            // final 8 bytes of CDN hash
            byte[] IV = filename.Substring(16).ToByteArray();

            var decryptor = Salsa20.CreateDecryptor(Key, IV);
            return new CryptoStream(stream, decryptor, CryptoStreamMode.Read);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Validates a name is a valid MD5 hash string
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool IsValidName(string name)
        {
            if (name == null || name.Length != 32)
                return false;

            return Regex.IsMatch(name, "^[0-9a-fA-F]{32}$", RegexOptions.Compiled);
        }

        #endregion
    }
}

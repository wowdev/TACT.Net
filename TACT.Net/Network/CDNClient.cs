using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using TACT.Net.Cryptography;

namespace TACT.Net.Network
{
    public sealed class CDNClient
    {
        public readonly List<string> Hosts;
        public readonly Armadillo Armadillo;
        /// <summary>
        /// Designates if CDN responses require decryption
        /// </summary>
        public bool Decrypt { get; set; }

        #region Constructors

        private CDNClient(bool decrypt)
        {
            Hosts = new List<string>();
            Armadillo = new Armadillo();
            Decrypt = decrypt;
        }

        public CDNClient(Configs.ConfigContainer configContainer, bool decrypt = false) : this(decrypt)
        {
            if (configContainer.CDNsFile == null)
                throw new ArgumentException("Unable to load CDNsFile");

            string[] hosts = configContainer.CDNsFile.GetValue("Hosts", configContainer.Locale)?.Split(' ');
            foreach (var host in hosts)
                Hosts.Add(host.Split('?')[0]);

            if (Hosts.Count == 0)
                throw new FormatException("No hosts found");
        }

        public CDNClient(IEnumerable<string> hosts, bool decrypt = false) : this(decrypt)
        {
            foreach (var host in hosts)
                Hosts.Add(host.Split('?')[0]);

            if (Hosts.Count == 0)
                throw new ArgumentException("No hosts found");
        }

        #endregion

        #region Methods

        /// <summary>
        /// Attempts to return a stream to a file from Blizzard's CDN
        /// </summary>
        /// <param name="cdnpath">CDN file path excluding the host</param>
        /// <returns></returns>
        public async Task<Stream> OpenStream(string cdnpath)
        {
            if (await GetContentLength(cdnpath) == -1)
                return null;

            foreach (var host in Hosts)
            {
                HttpWebRequest req = WebRequest.CreateHttp("http://" + host + "/" + cdnpath);

                try
                {
                    using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                    {
                        if (Decrypt)
                            return Armadillo.Decrypt(cdnpath, resp.GetResponseStream());

                        return resp.GetResponseStream();
                    }
                }
                catch (WebException) { }
            }

            return null;
        }

        /// <summary>
        /// Attempts to download a file from Blizzard's CDN
        /// </summary>
        /// <param name="cdnpath">CDN file path excluding the host</param>
        /// <param name="filepath">File save location</param>
        /// <returns></returns>
        public async Task<bool> DownloadFile(string cdnpath, string filepath)
        {
            if (await GetContentLength(cdnpath) == -1)
                return false;

            foreach (var host in Hosts)
            {
                HttpWebRequest req = WebRequest.CreateHttp("http://" + host + "/" + cdnpath);

                try
                {
                    using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                    using (var stream = resp.GetResponseStream())
                    using (var fs = File.Create(filepath))
                    {
                        (Decrypt ? Armadillo.Decrypt(cdnpath, stream) : stream).CopyTo(fs);
                    }

                    return true;

                }
                catch (WebException) { }
            }

            return false;
        }

        /// <summary>
        /// Returns the size of a file or -1 if it is inaccessible
        /// </summary>
        /// <param name="cdnpath"></param>
        /// <returns></returns>
        public async Task<long> GetContentLength(string cdnpath)
        {
            foreach (var host in Hosts)
            {
                HttpWebRequest req = WebRequest.CreateHttp("http://" + host + "/" + cdnpath);
                req.Method = "HEAD";

                try
                {
                    using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                        if (resp.StatusCode == HttpStatusCode.OK)
                            return resp.ContentLength;
                }
                catch (WebException) { }
            }

            return -1;
        }

        /// <summary>
        /// Attempts to load an Armadillo key. Supports both filepaths and Battle.Net key directory lookups
        /// </summary>
        /// <param name="filePathOrKeyName"></param>
        /// <returns></returns>
        public bool SetDecryptionKey(string filePathOrKeyName) => Armadillo.SetKey(filePathOrKeyName);

        #endregion
    }
}

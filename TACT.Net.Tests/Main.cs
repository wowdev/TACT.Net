using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TACT.Net.Configs;

namespace TACT.Net.Tests
{
    [TestClass]
    public class Main
    {
        const string PATH = @"C:\Users\spear\Downloads\";

        [TestInitialize()]
        public void Startup()
        {
            Directory.CreateDirectory("test");
        }

        [TestCleanup()]
        public void Cleanup()
        {
            Directory.Delete("test", true);
        }


        [TestMethod]
        public void TestCKeys()
        {
            string ckey = "0052ea9a56fd7b3b6fe7d1d906e6cdef";
            Archives.ArchiveIndex index = new Archives.ArchiveIndex(Path.Combine(PATH, "0052ea9a56fd7b3b6fe7d1d906e6cdef.index"));
            index.Write("test");
            Assert.AreEqual(ckey, index.Checksum.ToString());

            ckey = "499a7e060cdbb4de3cebc92aef4b90f8";
            Root.RootFile rootFile = new Root.RootFile(Path.Combine(PATH, "b785e5c1ff3a3fc9805baef91ea732e8"));
            Assert.AreEqual(ckey, rootFile.Write(@"test").CKey.ToString());

            ckey = "03387434f35c215499a27b96dc3aeac4";
            Encoding.EncodingFile encodingFile = new Encoding.EncodingFile(Path.Combine(PATH, "992b470cdeac795d7134920593b5997d"));
            Assert.AreEqual(ckey, encodingFile.Write("test").CKey.ToString());

            ckey = "3a5f72cb49b57206a0a1b3d586008dae";
            Install.InstallFile installFile = new Install.InstallFile(Path.Combine(PATH, "9e85df74a6280ee0d1c78b96c75d2384"));
            Assert.AreEqual(ckey, installFile.Write("test").CKey.ToString());

            ckey = "e792910ee5d0c50dd89fc48562a4b80a";
            Download.DownloadFile downloadFile = new Download.DownloadFile(Path.Combine(PATH, "64318bc48003848f6fb5f1604d314935"));
            Assert.AreEqual(ckey, downloadFile.Write("test").CKey.ToString());

            ckey = "a6173b06ed490cdc8c94b7c2a521278d";
            Download.DownloadSizeFile downloadSizeFile = new Download.DownloadSizeFile(Path.Combine(PATH, "07373402cad6fa1ade7d9075ab14cc69"));
            Assert.AreEqual(ckey, downloadSizeFile.Write("test").CKey.ToString());
        }

        [TestMethod]
        public void TestRibbit()
        {
            var rc = new Ribbit.RibbitClient(Locale.US);
            var resp = rc.GetString("v1/products/wowt/versions");
            Assert.IsTrue(resp.Contains("CDNConfig!"));
        }

        [TestMethod]
        public void TestConfigs()
        {
            TACT tact = new TACT();
            ConfigContainer configContainer = new ConfigContainer("wowt", Locale.US, tact);

            //configContainer.OpenRemote(@"D:\Backup\");
            //Assert.IsNotNull(configContainer.VersionsFile);

            configContainer.OpenLocal(@"D:\Backup\", @"D:\Backup\");
            Assert.IsNotNull(configContainer.VersionsFile);
            Assert.IsNotNull(configContainer.BuildConfig);
            Assert.IsFalse(configContainer.RootMD5.IsEmpty);
        }

        [TestMethod]
        public void TestOpenFile()
        {
            TACT tactInstance = new TACT(@"D:\Backup\");
            ConfigContainer configContainer = new ConfigContainer("wowt", Locale.US, tactInstance);
            configContainer.OpenLocal(tactInstance.BaseDirectory, tactInstance.BaseDirectory);

            Archives.ArchiveContainer archiveContainer = new Archives.ArchiveContainer(tactInstance);
            archiveContainer.Open(tactInstance.BaseDirectory);
            Assert.IsTrue(archiveContainer.ArchiveIndices.Count > 0);

            Encoding.EncodingFile encoding = new Encoding.EncodingFile(tactInstance.BaseDirectory, configContainer.EncodingEKey, tactInstance);

            Assert.IsTrue(encoding.TryGetContentEntry(configContainer.RootMD5, out var rootCEntry));

            Root.RootFile root = new Root.RootFile(tactInstance.BaseDirectory, rootCEntry.EKeys.First(), tactInstance);

            var fileEntry = root.Get("world/arttest/boxtest/xyz.m2").FirstOrDefault();
            Assert.IsNotNull(fileEntry);

            Assert.IsTrue(encoding.TryGetContentEntry(fileEntry.CKey, out var fileEntryCEntry));

            using (var fs = archiveContainer.OpenFile(fileEntryCEntry.EKeys.First()))
            {
                Assert.IsNotNull(fs);

                byte[] buffer = new byte[4];
                fs.Read(buffer);

                // MD21
                Assert.AreEqual(BitConverter.ToUInt32(buffer), 0x3132444Du);
            }
        }


        [TestMethod]
        public void TestDebugStuff()
        {
            //WOW-28807patch8.1.0_PTR

            //Archives.ArchiveIndex index = new Archives.ArchiveIndex(@"C:\Users\TomSpearman\Downloads\0052ea9a56fd7b3b6fe7d1d906e6cdef.index");
            //var entry = index.Entries.First(x => x.Offset == 0);

            //Patch.PatchFile patchFile = new Patch.PatchFile(@"C:\Users\TomSpearman\Downloads\284bff5cb89beb6ba2de5e012eb9ed1c");

            //Install.InstallFile installFile = new Install.InstallFile(@"C:\Users\spear\Downloads\9e85df74a6280ee0d1c78b96c75d2384");
            //installFile.Write("");

            //index.Write(@"C:\Users\TomSpearman\Downloads\Arctium WoW Client Launcher");

            //Root.RootFile rootFile = new Root.RootFile(@"C:\Users\TomSpearman\Downloads\b785e5c1ff3a3fc9805baef91ea732e8");
            //rootFile.Write("");

            //Encoding.EncodingFile encodingFile = new Encoding.EncodingFile(@"C:\Users\TomSpearman\Downloads\fc8bb2fcd439453504e8758ddd7e7535");
            //var b1 = encodingFile.GetContentEntryByEKey(new Common.Cryptography.MD5Hash("3afb13d370fe03be2d0b8622952621c3")).First();
            //encodingFile.TryGetEncodedEntry(new Common.Cryptography.MD5Hash("3afb13d370fe03be2d0b8622952621c3"), out var b);


            //encodingFile.Write("");

            //var b = encodingFile.Get(index.Entries.First().EKey, out Encoding.EncodingEKeyEntry entry);
            //var b2 = encodingFile.GetByEKey(entry.EKey).First();
            //var b3 = rootFile.Get(b2.CKey).FirstOrDefault();


            //Download.DownloadFile downloadFile = new Download.DownloadFile(@"C:\Users\spear\Downloads\64318bc48003848f6fb5f1604d314935");
            //downloadFile.Write("");

            //Download.DownloadSizeFile downloadSizeFile = new Download.DownloadSizeFile(@"C:\Users\spear\Downloads\07373402cad6fa1ade7d9075ab14cc69");
            //downloadSizeFile.Write("");

            //64318bc48003848f6fb5f1604d314935

        }

        public string ToHex(byte[] barray)
        {
            char[] c = new char[barray.Length * 2];

            byte b;
            for (int i = 0; i < barray.Length; ++i)
            {
                b = (byte)(barray[i] >> 4);
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = (byte)(barray[i] & 0xF);
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }

            return new string(c).ToLowerInvariant();
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TACT.Net.Configs;

namespace TACT.Net.Tests
{
    [TestClass]
    public class Main
    {
        const string PATH = @"D:\Backup\";

        [TestInitialize()]
        public void Startup()
        {
            if (Directory.Exists("test"))
                Directory.Delete("test", true);
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
            string ckey = "1a5047b2eebe491069f2f718aee082eb";
            Indices.IndexFile index = new Indices.IndexFile(Path.Combine(PATH, @"tpr\wow\data\1a\50", "1a5047b2eebe491069f2f718aee082eb.index"));
            index.Write("test");
            Assert.AreEqual(ckey, index.Checksum.ToString());

            ckey = "1228b5ef225fa4b85eebc5e32b1ca238";
            Root.RootFile rootFile = new Root.RootFile(PATH, new Cryptography.MD5Hash("fc52ef45efbbc6beca39076f89bad99f"));
            Assert.AreEqual(ckey, rootFile.Write(@"test").CKey.ToString());

            ckey = "eb25fe8bd9e5b9400cc236d196975972";
            Encoding.EncodingFile encodingFile = new Encoding.EncodingFile(PATH, new Cryptography.MD5Hash("fc8bb2fcd439453504e8758ddd7e7535"));
            Assert.AreEqual(ckey, encodingFile.Write("test").CKey.ToString());

            ckey = "e42b5c7faa58e88534192c2ad0fe2245";
            Install.InstallFile installFile = new Install.InstallFile(PATH, new Cryptography.MD5Hash("9b926ccdf5c51ff2cb5461cac7d9112b"));
            Assert.AreEqual(ckey, installFile.Write("test").CKey.ToString());

            ckey = "430df253ca137be4778763a02d25d9c3";
            Download.DownloadFile downloadFile = new Download.DownloadFile(PATH, new Cryptography.MD5Hash("eab82b2c1d2bf7dd315c87b28ed92cd5"));
            downloadFile.DownloadHeader.IncludeChecksum = true;
            Assert.AreEqual(ckey, downloadFile.Write("test").CKey.ToString());

            ckey = "408833604e3cc75670e283e51743e9a9";
            Download.DownloadSizeFile downloadSizeFile = new Download.DownloadSizeFile(PATH, new Cryptography.MD5Hash("af083d582f98a708881576df14e3c606"));
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
            TACT tact = new TACT()
            {
                ConfigContainer = new ConfigContainer("wowt", Locale.US)
            };

            //configContainer.OpenRemote(@"D:\Backup\");
            //Assert.IsNotNull(configContainer.VersionsFile);

            tact.ConfigContainer.OpenLocal(@"D:\Backup\", @"D:\Backup\");
            Assert.IsNotNull(tact.ConfigContainer.VersionsFile);
            Assert.IsNotNull(tact.ConfigContainer.BuildConfig);
            Assert.IsFalse(tact.ConfigContainer.RootMD5.IsEmpty);
        }

        [TestMethod]
        public void TestOpenFile()
        {
            // create an instance
            TACT tact = new TACT(@"D:\Backup\")
            {
                ConfigContainer = new ConfigContainer("wowt", Locale.US)
            };

            // open the configs
            tact.ConfigContainer.OpenLocal(tact.BaseDirectory, tact.BaseDirectory);

            // load the archives
            tact.IndexContainer = new Indices.IndexContainer();
            tact.IndexContainer.Open(tact.BaseDirectory);
            Assert.IsTrue(tact.IndexContainer.DataIndices.Any());

            // open the encoding
            tact.EncodingFile = new Encoding.EncodingFile(tact.BaseDirectory, tact.ConfigContainer.EncodingEKey);

            // get the root ckey
            Assert.IsTrue(tact.EncodingFile.TryGetContentEntry(tact.ConfigContainer.RootMD5, out var rootCEntry));

            // open the root
            tact.RootFile = new Root.RootFile(tact.BaseDirectory, rootCEntry.EKey, tact);

            // try and get a file
            var fileEntry = tact.RootFile.Get("world/arttest/boxtest/xyz.m2").FirstOrDefault();
            Assert.IsNotNull(fileEntry);

            // get the file's contententry
            Assert.IsTrue(tact.EncodingFile.TryGetContentEntry(fileEntry.CKey, out var fileEntryCEntry));

            // open the file from the blobs
            using (var fs = tact.IndexContainer.OpenFile(fileEntryCEntry.EKey))
            {
                Assert.IsNotNull(fs);

                byte[] buffer = new byte[4];
                fs.Read(buffer);

                // check for MD21 magic
                Assert.AreEqual(BitConverter.ToUInt32(buffer), 0x3132444Du);
            }
        }

        [TestMethod]
        public void TestArmadillo()
        {
            var armadillo = new Cryptography.Armadillo();
            Assert.IsTrue(armadillo.LoadKey(Path.Combine(PATH, "sc1Dev.ak")));
        }

        [TestMethod]
        public void TestZBSPatchingDummy()
        {
            string originalText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. In et pulvinar eros, id vulputate nibh.";
            string modifiedText = "Lorem ipsum dolor amet sit, consectetur adipiscing elit. nibh. id vulputate et pulvinar eros, In";

            byte[] original = System.Text.Encoding.UTF8.GetBytes(originalText);
            byte[] modified = System.Text.Encoding.UTF8.GetBytes(modifiedText);

            using (var input = new MemoryStream(original))
            using (var patch = new MemoryStream())
            using (var output = new MemoryStream())
            {
                Common.Patching.ZBSDiff.Create(original, modified, patch);
                
                patch.Position = 0;
                Common.Patching.ZBSPatch.Apply(input, patch, output);

                string resultText = System.Text.Encoding.UTF8.GetString(output.ToArray());
                Assert.AreEqual(modifiedText, resultText);
            }
        }

        [TestMethod]
        public void TestZBSPatchingReal()
        {
            // create an instance
            TACT tact = new TACT(@"D:\Backup\")
            {
                ConfigContainer = new ConfigContainer("wowt", Locale.US)
            };

            // open the configs
            tact.ConfigContainer.OpenLocal(tact.BaseDirectory, tact.BaseDirectory);

            // load the archives
            tact.IndexContainer = new Indices.IndexContainer();
            tact.IndexContainer.Open(tact.BaseDirectory);

            // open the patch file
            tact.PatchFile = new Patch.PatchFile(tact.BaseDirectory, tact.ConfigContainer.PatchMD5);

            // get the seagiant2.m2 patch
            Assert.IsTrue(tact.PatchFile.TryGet(new Cryptography.MD5Hash("8fbb9c89e91e0b30ab5eeec1cee0666d"), out var patchEntry));

            // read the patch entry from the archives
            // load the original file from disk - build 27826
            // apply the ZBSPatch (patch entry) to the original
            // verify the produced output is byte identical with the patched model - build 28807
            using (var patch = tact.IndexContainer.OpenPatch(patchEntry.Records[0].PatchEKey))
            using (var original = File.OpenRead(Path.Combine(PATH, "seagiant2_27826.m2")))
            using (var output = new MemoryStream())
            {
                Common.Patching.ZBSPatch.Apply(original, patch, output);

                var b = File.ReadAllBytes(Path.Combine(PATH, "seagiant2_28807.m2"));
                Assert.IsTrue(b.SequenceEqual(output.ToArray()));
            }
        }


        [TestMethod]
        public void TestDebugStuff()
        {
            var sw = Stopwatch.StartNew();
            Download.DownloadSizeFile downloadSizeFile = new Download.DownloadSizeFile(PATH, new Cryptography.MD5Hash("af083d582f98a708881576df14e3c606"));
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.Write("");

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

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TACT.Net.Configs;

namespace TACT.Net.Tests
{
    [TestClass]
    public class Main
    {
        const string PATH = @"D:\Backup\";

        [ClassInitialize()]
        public static void Startup(TestContext context)
        {
            Cleanup();
            Directory.CreateDirectory("test");
        }

        [ClassCleanup()]
        public static void Cleanup()
        {
            if (Directory.Exists("test"))
                Directory.Delete("test", true);
        }


        [TestMethod]
        public void TestCKeys()
        {
            // WOW-28807patch8.1.0_PTR

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
            var rc = new Network.RibbitClient(Locale.US);
            var resp = rc.GetString("v1/products/wowt/versions").Result;
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

            tact.ConfigContainer.OpenLocal(@"D:\Backup\");
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
                ConfigContainer = new ConfigContainer("wow", Locale.US)
            };

            // open the configs
            tact.ConfigContainer.OpenLocal(tact.BaseDirectory);

            // load the archives
            tact.IndexContainer = new Indices.IndexContainer();
            tact.IndexContainer.Open(tact.BaseDirectory);
            Assert.IsTrue(tact.IndexContainer.DataIndices.Any());

            // open the encoding
            tact.EncodingFile = new Encoding.EncodingFile(tact.BaseDirectory, tact.ConfigContainer.EncodingEKey);

            // get the root ckey
            Assert.IsTrue(tact.EncodingFile.TryGetContentEntry(tact.ConfigContainer.RootMD5, out var rootCEntry));

            // open the root
            tact.RootFile = new Root.RootFile(tact.BaseDirectory, rootCEntry.EKey);

            // read a normal file then an encrypted file
            string[] filenames = new[] { "world/arttest/boxtest/xyz.m2", "creature/encrypted05/encrypted05.m2" };
            foreach (var filename in filenames)
            {
                // open a stream to the file
                // gets the file's ckey from the root
                // gets the file's ekey from the encoding
                // loads the IndexEntry from the IndexContainer
                // returns a BLTE stream to the file segment in the data blob
                using (var fs = tact.RootFile.OpenFile(filename, tact))
                {
                    Assert.IsNotNull(fs);

                    byte[] buffer = new byte[4];
                    fs.Read(buffer);

                    // check for MD21 magic
                    Assert.AreEqual(BitConverter.ToUInt32(buffer), 0x3132444Du);
                }
            }
        }

        [TestMethod]
        public void TestArmadillo()
        {
            var armadillo = new Cryptography.Armadillo();
            Assert.IsTrue(armadillo.SetKey("Resources/sc1Dev.ak"));
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
            tact.ConfigContainer.OpenLocal(tact.BaseDirectory);

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
            using (var original = File.OpenRead("Resources/seagiant2_27826.m2"))
            using (var output = new MemoryStream())
            {
                Assert.IsTrue(tact.PatchFile.ApplyPatch(patchEntry, tact.IndexContainer, original, output));

                var b = File.ReadAllBytes("Resources/seagiant2_28807.m2");
                Assert.IsTrue(b.SequenceEqual(output.ToArray()));
            }
        }

        [TestMethod]
        //[Ignore]
        public void CreateNewTactRepo()
        {
            if (Directory.Exists(@"C:\wamp64\www\tpr"))
                Directory.Delete(@"C:\wamp64\www\tpr", true);
            if (Directory.Exists(@"C:\wamp64\www\wow"))
                Directory.Delete(@"C:\wamp64\www\wow", true);


            string buildName = "WOW-15595patch4.3.4_Retail";
            string buildId = "15595";
            string versionName = "4.3.4.15595";

            string tempPath = Path.Combine("test", "temp");
            Directory.CreateDirectory(tempPath);

            // open a new tact instance
            TACT tact = new TACT();
            tact.Create("wow", Locale.US, uint.Parse(buildId));

            // update the configs
            // build info and server locations
            tact.ConfigContainer.VersionsFile.SetValue("BuildId", buildId);
            tact.ConfigContainer.VersionsFile.SetValue("VersionsName", versionName);
            tact.ConfigContainer.BuildConfig.SetValue("Build-Name", buildName, 0);
            tact.ConfigContainer.BuildConfig.SetValue("Build-UID", "wow", 0);
            tact.ConfigContainer.BuildConfig.SetValue("Build-Product", "WoW", 0);
            tact.ConfigContainer.CDNsFile.SetValue("Hosts", "localhost");
            tact.ConfigContainer.CDNsFile.SetValue("Servers", "http://127.0.0.1");

            // set root variables
            tact.RootFile.LocaleFlags = Root.LocaleFlags.enUS;
            tact.RootFile.FileLookup = new MockFileLookup();

            var record = BlockTable.BlockTableEncoder.EncodeAndExport("Resources/seagiant2_27826.m2", tempPath, "creature/seagiant2/seagiant2.m2");
            tact.RootFile.AddOrUpdate(record, tact);

            record.FileName = "WoW.exe";
            tact.InstallFile.AddOrUpdate(record, tact);

            tact.Save(tact.BaseDirectory);
        }


        [TestMethod]
        public void TestDebugStuff()
        {
           

        }
    }
}

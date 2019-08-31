using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TACT.Net.Configs;

namespace TACT.Net.Tests
{
    [TestClass]
    public class Main
    {
        const string MANIFEST_PATH = @"D:\Backup\wowt";
        const string PATH = @"D:\Backup\tpr\wow";

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
            Indices.IndexFile index = new Indices.IndexFile(Path.Combine(PATH, @"data\1a\50", "1a5047b2eebe491069f2f718aee082eb.index"));
            index.Write("test");
            Assert.AreEqual(ckey, index.Checksum.ToString());

            ckey = "1228b5ef225fa4b85eebc5e32b1ca238";
            Root.RootFile rootFile = new Root.RootFile(PATH, new Cryptography.MD5Hash("fc52ef45efbbc6beca39076f89bad99f"));
            Assert.AreEqual(ckey, rootFile.Write(@"test").CKey.ToString());

            ckey = "c21a5f9d0a7f8ac9d332ddb15c79b97d";
            Root.RootFile rootFileV2 = new Root.RootFile(Path.Combine(@"D:\Backup\", "3e6e2458346ad9507cc5a98655b192eb"));
            Assert.AreEqual(ckey, rootFileV2.Write("test").CKey.ToString());

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
            TACTRepo tactRepo = new TACTRepo("wow", Locale.US, MANIFEST_PATH, PATH)
            {
                ManifestContainer = new ManifestContainer("wow", Locale.US),
                ConfigContainer = new ConfigContainer()
            };

            //configContainer.OpenRemote(PATH);
            //Assert.IsNotNull(configContainer.VersionsFile);

            tactRepo.ManifestContainer.OpenLocal(MANIFEST_PATH);
            tactRepo.ConfigContainer.OpenLocal(PATH, tactRepo.ManifestContainer);
            Assert.IsNotNull(tactRepo.ManifestContainer.VersionsFile);
            Assert.IsNotNull(tactRepo.ConfigContainer.BuildConfig);
            Assert.IsFalse(tactRepo.ConfigContainer.RootCKey.IsEmpty);
        }

        [TestMethod]
        public void TestOpenFile()
        {
            // create an instance
            TACTRepo tactRepo = new TACTRepo(PATH)
            {
                ManifestContainer = new ManifestContainer("wow", Locale.US),
                ConfigContainer = new ConfigContainer()
            };

            // open the configs
            tactRepo.ManifestContainer.OpenLocal(MANIFEST_PATH);
            tactRepo.ConfigContainer.OpenLocal(tactRepo.BaseDirectory, tactRepo.ManifestContainer);

            // load the archives
            tactRepo.IndexContainer = new Indices.IndexContainer();
            tactRepo.IndexContainer.Open(tactRepo.BaseDirectory);
            Assert.IsTrue(tactRepo.IndexContainer.DataIndices.Any());

            // open the encoding
            tactRepo.EncodingFile = new Encoding.EncodingFile(tactRepo.BaseDirectory, tactRepo.ConfigContainer.EncodingEKey);

            // get the root ckey
            Assert.IsTrue(tactRepo.EncodingFile.TryGetCKeyEntry(tactRepo.ConfigContainer.RootCKey, out var rootCEntry));

            // open the root
            tactRepo.RootFile = new Root.RootFile(tactRepo.BaseDirectory, rootCEntry.EKey);

            // read a normal file then an encrypted file
            string[] filenames = new[] { "world/arttest/boxtest/xyz.m2", "creature/encrypted05/encrypted05.m2" };
            foreach (var filename in filenames)
            {
                // open a stream to the file
                // gets the file's ckey from the root
                // gets the file's ekey from the encoding
                // loads the IndexEntry from the IndexContainer
                // returns a BLTE stream to the file segment in the data blob
                using (var fs = tactRepo.RootFile.OpenFile(filename, tactRepo))
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
            TACTRepo tactRepo = new TACTRepo(PATH)
            {
                ManifestContainer = new ManifestContainer("wow", Locale.US),
                ConfigContainer = new ConfigContainer()
            };

            // open the configs
            tactRepo.ManifestContainer.OpenLocal(MANIFEST_PATH);
            tactRepo.ConfigContainer.OpenLocal(tactRepo.BaseDirectory, tactRepo.ManifestContainer);

            // load the archives
            tactRepo.IndexContainer = new Indices.IndexContainer();
            tactRepo.IndexContainer.Open(tactRepo.BaseDirectory);

            // open the patch file
            tactRepo.PatchFile = new Patch.PatchFile(tactRepo.BaseDirectory, tactRepo.ConfigContainer.PatchEKey);

            // get the seagiant2.m2 patch
            Assert.IsTrue(tactRepo.PatchFile.TryGet(new Cryptography.MD5Hash("8fbb9c89e91e0b30ab5eeec1cee0666d"), out var patchEntry));

            // read the patch entry from the archives
            // load the original file from disk - build 27826
            // apply the ZBSPatch (patch entry) to the original
            // verify the produced output is byte identical with the patched model - build 28807
            using (var original = File.OpenRead("Resources/seagiant2_27826.m2"))
            using (var output = new MemoryStream())
            {
                Assert.IsTrue(tactRepo.PatchFile.ApplyPatch(patchEntry, tactRepo.IndexContainer, original, output));

                var b = File.ReadAllBytes("Resources/seagiant2_28807.m2");
                Assert.IsTrue(b.SequenceEqual(output.ToArray()));
            }
        }

        [TestMethod]
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
            TACTRepo tactRepo = new TACTRepo("test");
            tactRepo.Create("wow", Locale.US, uint.Parse(buildId));

            // update the configs
            // build info and server locations
            tactRepo.ManifestContainer.VersionsFile.SetValue("BuildId", buildId);
            tactRepo.ManifestContainer.VersionsFile.SetValue("VersionsName", versionName);
            tactRepo.ConfigContainer.BuildConfig.SetValue("Build-Name", buildName, 0);
            tactRepo.ConfigContainer.BuildConfig.SetValue("Build-UID", "wow", 0);
            tactRepo.ConfigContainer.BuildConfig.SetValue("Build-Product", "WoW", 0);
            tactRepo.ManifestContainer.CDNsFile.SetValue("Hosts", "localhost");
            tactRepo.ManifestContainer.CDNsFile.SetValue("Servers", "http://127.0.0.1");

            // set root variables
            tactRepo.RootFile.LocaleFlags = Root.LocaleFlags.enUS;
            tactRepo.RootFile.FileLookup = new MockFileLookup();
            tactRepo.RootFile.AddBlock(Root.LocaleFlags.All_WoW, 0);

            var record = BlockTable.BlockTableEncoder.EncodeAndExport("Resources/seagiant2_27826.m2", tempPath, "creature/seagiant2/seagiant2.m2");
            tactRepo.RootFile.AddOrUpdate(record, tactRepo);

            record.FileName = "WoW.exe";
            tactRepo.InstallFile.AddOrUpdate(record, tactRepo);

            tactRepo.Save(tactRepo.BaseDirectory, tactRepo.BaseDirectory);
        }

        [TestMethod]
        [Ignore]
        public void OverrideExistingFile_Simple()
        {
            // This is "simple" as I only have the Encoding, Root and configs downloaded - no CDN backup.
            // By using CDNClient all of this can be achieved without any source files on disk.

            string customfilepath = "Resources/ui_mainmenu_legion_27826.m2"; // local filename
            string targetfilename = "interface/glues/models/ui_mainmenu_battleforazeroth/ui_mainmenu_battleforazeroth.m2"; // root filename
            uint targetfileid = 2021650; // filedataid of the above

            TACTRepo tactRepo = new TACTRepo(@"C:\wamp64\www")
            {
                ManifestContainer = new ManifestContainer("wow", Locale.US),
                ConfigContainer = new ConfigContainer()
            };

            // open the configs
            // note: the Patch file is removed since it isn't accessible (aka downloaded)
            tactRepo.ManifestContainer.OpenLocal(MANIFEST_PATH);
            tactRepo.ConfigContainer.OpenLocal(tactRepo.BaseDirectory, tactRepo.ManifestContainer);

            // update the cdns config to point to localhost
            var hosts = tactRepo.ManifestContainer.CDNsFile.GetValue("hosts", Locale.EU);
            if (!hosts.Contains("127.0.0.1"))
                tactRepo.ManifestContainer.CDNsFile.SetValue("hosts", hosts.Insert(0, "127.0.0.1 "), Locale.EU);

            var servers = tactRepo.ManifestContainer.CDNsFile.GetValue("servers", Locale.EU);
            if (!servers.Contains("http://127.0.0.1"))
                tactRepo.ManifestContainer.CDNsFile.SetValue("servers", hosts.Insert(0, "http://127.0.0.1 "), Locale.EU);

            // create an index container
            tactRepo.IndexContainer = new Indices.IndexContainer();

            // open encoding
            tactRepo.EncodingFile = new Encoding.EncodingFile(tactRepo.BaseDirectory, tactRepo.ConfigContainer.EncodingEKey);

            // open root
            tactRepo.EncodingFile.TryGetCKeyEntry(tactRepo.ConfigContainer.RootCKey, out var rootCKeyEntry);
            tactRepo.RootFile = new Root.RootFile(tactRepo.BaseDirectory, rootCKeyEntry.EKey)
            {
                FileLookup = new MockFileLookup()
                {
                    [targetfilename] = targetfileid // mock the custom file
                }
            };

            // encode and export the "custom" file to a temp folder
            // - one must export otherwise the file won't be added to an archive
            var blte = BlockTable.BlockTableEncoder.EncodeAndExport(customfilepath, "test", targetfilename);

            // add the "custom" file to root, this will propagate via 'tactRepo'
            tactRepo.RootFile.AddOrUpdate(blte, tactRepo);

            // save the repo
            tactRepo.Save(tactRepo.BaseDirectory);
        }


        [TestMethod]
        [Ignore]
        public void TestDebugStuff()
        {
        }
    }
}

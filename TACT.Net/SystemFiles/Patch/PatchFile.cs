using System;
using System.Collections.Generic;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Common.Patching;
using TACT.Net.Cryptography;
using TACT.Net.Network;

namespace TACT.Net.Patch
{
    public class PatchFile : ISystemFile
    {
        public PatchHeader PatchHeader { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public string FilePath { get; private set; }
        public IEnumerable<PatchEntry> PatchEntries => _PatchEntries.Values;

        private readonly SortedList<MD5Hash, PatchEntry> _PatchEntries;

        #region Constructors

        /// <summary>
        /// Creates a new PatchFile
        /// </summary>
        public PatchFile()
        {
            PatchHeader = new PatchHeader();
            _PatchEntries = new SortedList<MD5Hash, PatchEntry>(new MD5HashComparer());
        }

        /// <summary>
        /// Loads an existing PatchFile
        /// </summary>
        /// <param name="path"></param>
        public PatchFile(string path) : this()
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Unable to open PatchFile", path);

            FilePath = path;

            using (var fs = File.OpenRead(path))
                Read(fs);
        }

        /// <summary>
        /// Loads an existing PatchFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="ekey">PatchFile MD5</param>
        public PatchFile(string directory, MD5Hash ekey) :
            this(Helpers.GetCDNPath(ekey.ToString(), "patch", directory))
        { }

        /// <summary>
        /// Loads an existing PatchFile from a remote CDN
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ekey"></param>
        public PatchFile(CDNClient client, MD5Hash ekey) : this()
        {
            string url = Helpers.GetCDNUrl(ekey.ToString(), "patch");

            using (var stream = client.OpenStream(url).Result)
                Read(stream);
        }

        /// <summary>
        /// Loads an existing PatchFile
        /// </summary>
        /// <param name="stream"></param>
        public PatchFile(Stream stream) : this()
        {
            Read(stream);
        }

        #endregion

        #region IO

        private void Read(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead || stream.Length <= 1)
                throw new NotSupportedException($"Unable to read PatchFile stream");

            using (var reader = new BinaryReader(stream))
            {
                PatchHeader.Read(reader);

                // read the patch entries
                int pageSize = 1 << PatchHeader.PageSize;
                long length = reader.BaseStream.Length;

                foreach (var offset in GetOffsets(reader))
                {
                    reader.BaseStream.Position = offset;

                    // buffer the page then read the entries to minimise file reads
                    using (var ms = new MemoryStream(reader.ReadBytes(pageSize)))
                    using (var br = new BinaryReader(ms))
                    {
                        var block = new PatchEntry();
                        while (block.Read(br, PatchHeader))
                        {
                            _PatchEntries[block.CKey] = block;
                            block = new PatchEntry();
                        }
                    }
                }

                #region Unknown Data

                // Unknown = br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));

                /*
                // the following is unknown entries that comprise of:
                // a keysize byte followed by either a CKey or Ekey
                // the key type relevant size (decompressed/compressed)
                // and optionally an ESpec string
                // blocks are always a pair of 112 bytes then 160, Encoding Entry then Content Entry
                //
                // struct encodin_entry {
                //    char _unk0[4]; // always 0
                //    char KeySize;
                //    char Hash[KeySize];
                //    char _unk15[7];
                //    uint32_t some_offset;
                //    
                //    // 0x44 CompressedSize;
                // } 
                // 
                // struct content_entry {
                //    char _unk0[4]; // always 0
                //    char KeySize;
                //    char Hash[KeySize];
                //    char _unk15[7];
                //    uint32_t some_offset;
                //    
                //    // 0x34 DeompressedSize;
                //    // 0x6C partial ESpec   always ends in 0xA0
                // }
                */
                #endregion

                Checksum = stream.MD5Hash();
            }
        }

        public CASRecord Write(string directory, TACTRepo tactRepo = null)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a PatchEntry by CKey
        /// </summary>
        /// <param name="ckey"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public bool TryGet(MD5Hash ckey, out PatchEntry entry) => _PatchEntries.TryGetValue(ckey, out entry);

        /// <summary>
        /// Determines whether the specific CKey exists
        /// </summary>
        /// <param name="ckey"></param>
        /// <returns></returns>
        public bool ContainsKey(MD5Hash ckey) => _PatchEntries.ContainsKey(ckey);

        /// <summary>
        /// Applies all patches to a file
        /// </summary>
        /// <param name="patchEntry"></param>
        /// <param name="indexContainer"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        public bool ApplyPatch(PatchEntry patchEntry, Indices.IndexContainer indexContainer, Stream input, Stream output)
        {
            if (indexContainer == null || input == null || output == null || patchEntry == null)
                return false;

            if (patchEntry.Records.Count == 0)
                return false;

            // get the correct order - just in case
            patchEntry.Records.Sort((x, y) => x.Ordinal - y.Ordinal);

            // iterate the patches
            for (int i = 0; i < patchEntry.Records.Count; i++)
            {
                // if applying more than one the previous output is required
                // as patches are incremental
                if (i > 0)
                    input = output;

                using (var patch = indexContainer.OpenPatch(patchEntry.Records[i].PatchEKey))
                {
                    if (patch == null)
                        return false;

                    ZBSPatch.Apply(input, patch, output);
                }
            }

            return true;
        }

        public void AddOrUpdate(CASRecord record, TACTRepo repo = null)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Helpers

        private uint[] GetOffsets(BinaryReader br)
        {
            uint[] offsets = new uint[PatchHeader.BlockCount];

            for (int i = 0; i < offsets.Length; i++)
            {
                // skip the last page key and page hash
                br.BaseStream.Position += PatchHeader.FileKeySize + 16;
                offsets[i] = br.ReadUInt32BE();
            }

            return offsets;
        }        

        #endregion
    }
}

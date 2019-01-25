using System.Collections.Generic;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Common.Cryptography;
using TACT.Net.SystemFiles;

namespace TACT.Net.Patch
{
    public class PatchFile : SystemFileBase
    {
        public PatchHeader PatchHeader { get; private set; }
        public MD5Hash Checksum { get; private set; }
        public IEnumerable<PatchEntry> PatchEntries => _PatchEntries.Values;


        private readonly SortedDictionary<MD5Hash, PatchEntry> _PatchEntries;

        #region Constructors

        /// <summary>
        /// Creates a new PatchFile
        /// </summary>
        public PatchFile(TACT container = null) : base(container)
        {
            PatchHeader = new PatchHeader();
            _PatchEntries = new SortedDictionary<MD5Hash, PatchEntry>(new HashComparer());
        }

        /// <summary>
        /// Loads an existing PatchFile
        /// </summary>
        /// <param name="path">BLTE encoded file path</param>
        public PatchFile(string path, TACT container = null) : this(container)
        {
            using (var fs = File.OpenRead(path))
                Read(fs);
        }

        /// <summary>
        /// Loads an existing PatchFile
        /// </summary>
        /// <param name="directory">Base directory</param>
        /// <param name="hash">PatchFile MD5</param>
        public PatchFile(string directory, MD5Hash hash, TACT container = null) :
            this(Helpers.GetCDNPath(hash.ToString(), "patch", directory), container)
        { }

        /// <summary>
        /// Loads an existing PatchFile
        /// </summary>
        /// <param name="stream"></param>
        public PatchFile(Stream stream, TACT container = null) : this(container)
        {
            Read(stream);
        }

        #endregion

        #region IO

        private void Read(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                PatchHeader.Read(br);

                // read the patch entries
                int pageSize = 1 << PatchHeader.PageSize;
                foreach (var offset in GetOffsets(br))
                {
                    br.BaseStream.Position = offset;
                    long endPos = offset + pageSize;

                    var block = new PatchEntry();
                    while (br.BaseStream.Position < endPos && block.Read(br, PatchHeader))
                    {
                        _PatchEntries[block.CKey] = block;
                        block = new PatchEntry();
                    }
                }

                #region Unknown Data
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

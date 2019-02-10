using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.SystemFiles;

namespace TACT.Net.Tags
{
    public class TagFileBase : ISystemFile
    {
        public IEnumerable<TagEntry> Tags => _TagEntries.Values;
        public MD5Hash Checksum { get; protected set; }

        protected readonly Dictionary<string, TagEntry> _TagEntries;

        #region Constructors

        protected TagFileBase()
        {
            _TagEntries = new Dictionary<string, TagEntry>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region IO 

        protected void ReadTags(BinaryReader br, uint tagCount, uint entryCount)
        {
            _TagEntries.EnsureCapacity((int)tagCount);

            for (int i = 0; i < tagCount; i++)
            {
                var tagEntry = new TagEntry();
                tagEntry.Read(br, entryCount);
                _TagEntries.Add(tagEntry.Name, tagEntry);
            }

            _TagEntries.TrimExcess();
        }

        protected void WriteTags(BinaryWriter bw)
        {
            foreach (var tagEntry in SortTags(_TagEntries.Values))
                tagEntry.Write(bw);
        }

        #endregion

        #region Methods

        protected void AddOrUpdateTag(TagEntry tagEntry, int fileCount)
        {
            // initialise the mask for new entries
            if (!_TagEntries.ContainsKey(tagEntry.Name))
                tagEntry.FileMask = new BoolArray((fileCount + 7) / 8);

            _TagEntries[tagEntry.Name] = tagEntry;
        }

        /// <summary>
        /// Removes the specified TagEntry from the collection
        /// </summary>
        /// <param name="tagEntry"></param>
        public void Remove(TagEntry tagEntry) => _TagEntries.Remove(tagEntry.Name);

        protected void RemoveFile(int index)
        {
            if (index > -1)
            {
                foreach (var tagEntry in _TagEntries.Values)
                    tagEntry.FileMask.Remove(index);
            }
        }

        /// <summary>
        /// Returns a TagEntry by name
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="tagEntry"></param>
        /// <returns></returns>
        public bool TryGet(string tag, out TagEntry tagEntry) => _TagEntries.TryGetValue(tag, out tagEntry);

        /// <summary>
        /// Determines if the specific Tag exists
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public bool ContainsTag(string tag) => _TagEntries.ContainsKey(tag);

        protected IEnumerable<string> GetTags(int index)
        {
            if (index > -1)
            {
                foreach (var tagEntry in _TagEntries.Values)
                    if (tagEntry.FileMask[index])
                        yield return tagEntry.Name;
            }
        }

        /// <summary>
        /// Enables/disables a the file at index for the supplied tags. If no tags are provided this applies to them all.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <param name="tags"></param>
        protected void SetTags(int index, bool value, params string[] tags)
        {
            if (index > -1)
            {
                if (tags == null || tags.Length == 0)
                    tags = _TagEntries.Keys.ToArray();

                foreach (var tag in tags)
                    if (_TagEntries.TryGetValue(tag, out var tagEntry))
                        tagEntry.FileMask[index] = value;
            }
        }

        #endregion

        #region Helpers

        private IOrderedEnumerable<TagEntry> SortTags(IEnumerable<TagEntry> tagEntries)
        {
            // order by type then name, Alternate is Locale although differentiated
            return tagEntries.OrderBy(x => x.TypeId == 0x4000 ? 3 : x.TypeId).ThenBy(x => x.Name);
        }

        #endregion
    }
}

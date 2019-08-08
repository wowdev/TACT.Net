using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TACT.Net.Common;
using TACT.Net.Cryptography;

namespace TACT.Net.Tags
{
    public class TagFileBase
    {
        public IEnumerable<TagEntry> Tags => _TagEntries.Values;

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
        }

        protected void WriteTags(BinaryWriter bw, int entryCount)
        {
            foreach (var tagEntry in SortTags(_TagEntries.Values))
            {
                // force the tag entries to be the same size
                tagEntry.FileMask.Expand(entryCount);
                tagEntry.Write(bw);
            }
        }

        #endregion

        #region Methods

        protected void Add(string name, ushort typeId, int fileCount)
        {
            TagEntry tagEntry = new TagEntry()
            {
                Name = name,
                TypeId = typeId
            };

            AddOrUpdateTag(tagEntry, fileCount);
        }

        protected void AddOrUpdateTag(TagEntry tagEntry, int fileCount)
        {
            // initialise the mask for new entries
            if (tagEntry.FileMask == null)
                tagEntry.FileMask = new BoolArray((uint)fileCount);

            _TagEntries[tagEntry.Name] = tagEntry;
        }

        /// <summary>
        /// Removes the specified TagEntry from the collection
        /// </summary>
        /// <param name="tagEntry"></param>
        public bool Remove(TagEntry tagEntry) => _TagEntries.Remove(tagEntry.Name);

        protected bool RemoveFile(int index)
        {
            if (index <= -1)
                return false;

            foreach (var tagEntry in _TagEntries.Values)
                tagEntry.FileMask.Remove(index);

            return true;
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
            if (index <= -1)
                yield break;

            foreach (var tagEntry in _TagEntries.Values)
                if (tagEntry.FileMask[index])
                    yield return tagEntry.Name;
        }

        /// <summary>
        /// Enables/disables a the file at index for the supplied tags. If no tags are provided this applies to them all.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <param name="tags"></param>
        protected void SetTags(int index, bool value, params string[] tags)
        {
            if (index <= -1 || _TagEntries.Count == 0)
                return;

            if (tags == null || tags.Length == 0)
                tags = _TagEntries.Keys.ToArray();

            foreach (var tag in tags)
                if (_TagEntries.TryGetValue(tag, out var tagEntry))
                    tagEntry.FileMask[index] = value;
        }

        /// <summary>
        /// Loads the default tags for the specific build
        /// </summary>
        /// <param name="build"></param>
        /// <param name="fileCount"></param>
        protected void SetDefaultTags(uint build, int fileCount)
        {
            _TagEntries.Clear();

            var typeIds = TagTypeHelper.GetTypeIds(build);

            // Platform
            if (typeIds.TryGetValue("Platform", out var id))
            {
                Add("OSX", id, fileCount);
                Add("Web", id, fileCount);
                Add("Windows", id, fileCount);
            }

            // Architecture
            if (typeIds.TryGetValue("Architecture", out id))
            {
                Add("x86_32", id, fileCount);
                Add("x86_64", id, fileCount);
            }

            // Locale
            if (typeIds.TryGetValue("Locale", out id))
            {
                Add("deDE", id, fileCount);
                Add("enUS", id, fileCount);
                Add("esES", id, fileCount);
                Add("esMX", id, fileCount);
                Add("frFR", id, fileCount);
                Add("itIT", id, fileCount);
                Add("koKR", id, fileCount);
                Add("ptBR", id, fileCount);
                Add("ruRU", id, fileCount);
                Add("zhCN", id, fileCount);
                Add("zhTW", id, fileCount);
            }

            //Region
            if (typeIds.TryGetValue("Region", out id))
            {
                Add("CN", id, fileCount);
                Add("EU", id, fileCount);
                Add("KR", id, fileCount);
                Add("TW", id, fileCount);
                Add("US", id, fileCount);
            }

            // Category
            if (typeIds.TryGetValue("Category", out id))
            {
                Add("speech", id, fileCount);
                Add("text", id, fileCount);
            }

            // Alternate
            if (typeIds.TryGetValue("Alternate", out id))
            {
                Add("Alternate", id, fileCount);
            }
        }

        /// <summary>
        /// Sets the File Mask capacity for all Tags
        /// </summary>
        /// <param name="capacity"></param>
        public void SetTagsCapacity(int capacity)
        {
            foreach (var tag in _TagEntries.Values)
                tag.FileMask.Capacity = capacity;
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

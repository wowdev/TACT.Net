using System.Collections.Generic;
using System.IO;
using TACT.Net.Common;
using TACT.Net.Cryptography;
using TACT.Net.Tags;

namespace TACT.Net.Download
{
    public abstract class DownloadFileBase<T> : TagFileBase where T : IDownloadFileEntry
    {
        public IEnumerable<T> Files => _FileEntries.Values;

        protected Dictionary<MD5Hash, T> _FileEntries;

        #region Constructors

        protected DownloadFileBase()
        {
            _FileEntries = new Dictionary<MD5Hash, T>(new HashComparer());
        }

        #endregion

        #region IO

        protected abstract void Read(Stream stream);

        public abstract CASRecord Write(string directory, TACT tactInstance = null);

        #endregion

        #region Methods

        public void AddOrUpdate(T fileEntry, params string[] tags)
        {
            _FileEntries[fileEntry.EKey] = fileEntry;

            // update the tag masks
            int index = _FileEntries.IndexOfKey(x => x == fileEntry.EKey);
            SetTags(index, true, tags);
        }

        public void AddOrUpdate(TagEntry tagEntry)
        {
            AddOrUpdateTag(tagEntry, _FileEntries.Count);
        }

        public void Remove(T fileEntry)
        {
            int index = _FileEntries.IndexOfKey(x => x == fileEntry.EKey);
            if (index > -1)
            {
                _FileEntries.Remove(fileEntry.EKey);
                RemoveFile(index);
            }
        }

        /// <summary>
        /// Returns a FileEntry by EKey
        /// </summary>
        /// <param name="ekey"></param>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        public bool TryGet(MD5Hash ekey, out T fileEntry) => _FileEntries.TryGetValue(ekey, out fileEntry);

        /// <summary>
        /// Determine if the specific FileEntry exists
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public bool Contains(MD5Hash ekey) => _FileEntries.ContainsKey(ekey);

        /// <summary>
        /// Returns all Tags for the specified FileEntry key
        /// </summary>
        /// <param name="ekey"></param>
        /// <returns></returns>
        public IEnumerable<string> GetTags(MD5Hash ekey)
        {
            int index = _FileEntries.IndexOfKey(x => x == ekey);
            return GetTags(index);
        }

        /// <summary>
        /// Enables/disables the supplied tags for a FileEntry
        /// </summary>
        /// <param name="ekey"></param>
        /// <param name="value"></param>
        /// <param name="tags"></param>
        public void SetTags(MD5Hash ekey, bool value, params string[] tags)
        {
            int index = _FileEntries.IndexOfKey(x => x == ekey);
            SetTags(index, value, tags);
        }

        #endregion
    }
}

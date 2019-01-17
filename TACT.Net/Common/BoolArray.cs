using System;
using System.Collections;
using System.Collections.Generic;

namespace TACT.Net.Common
{
    /// <summary>
    /// Resizable BE Boolean Collection
    /// </summary>
    internal class BoolArray : ICollection<bool>
    {
        private readonly List<bool> _values;

        #region Constructors

        public BoolArray(byte[] bytes)
        {
            _values = new List<bool>(bytes.Length * 8);
            for (int i = 0; i < bytes.Length * 8; i++)
                _values.Add((bytes[i / 8] & (1 << 7 - (i % 8))) != 0);
        }

        public BoolArray(int count)
        {
            _values = new List<bool>(new bool[count]);
        }

        #endregion

        #region Methods

        public bool this[int index]
        {
            get => _values[index];
            set
            {
                // expand the collection automatically
                int diff = index - _values.Count + 1;
                if (diff > 0)
                    _values.AddRange(new bool[diff]);

                _values[index] = value;
            }
        }

        public void Add(bool v) => _values.Add(v);

        public void Remove(int index) => _values.RemoveAt(index);

        public void Insert(int i, bool v) => _values.Insert(i, v);

        public void Clear() => _values.Clear();

        public byte[] ToByteArray()
        {
            byte[] bytes = new byte[(_values.Count + 7) / 8];

            for (int i = 0; i < _values.Count; i++)
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));

            return bytes;
        }

        #endregion

        #region Interface Methods

        public int Count => _values.Count;

        public bool IsReadOnly => false;

        public bool Contains(bool item) => throw new NotImplementedException();

        public bool Remove(bool item) => throw new NotImplementedException();

        public void CopyTo(bool[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);

        public IEnumerator<bool> GetEnumerator() => _values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

        #endregion

    }
}

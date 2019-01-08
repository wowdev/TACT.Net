using System;
using System.Collections;
using System.Linq;

namespace TACT.Net.Common.Cryptography
{
    public struct MD5Hash : IEquatable<MD5Hash>
    {
        public readonly byte[] Value;

        // cache these values as this struct is readonly
        private int? hashcode;
        private string stringvalue;

        #region Constructors

        public MD5Hash(string hash)
        {
            Value = hash.ToByteArray();
            hashcode = null;
            stringvalue = null;
        }

        public MD5Hash(byte[] hash)
        {
            Value = hash;
            hashcode = null;
            stringvalue = null;
        }

        #endregion

        public bool IsEmpty => Value == null || Value.Length == 0 || Value.All(x => x == 0);

        #region Operators

        public static bool operator ==(MD5Hash hash1, MD5Hash hash2) => hash1.Equals(hash2);

        public static bool operator !=(MD5Hash hash1, MD5Hash hash2) => !hash1.Equals(hash2);

        public override bool Equals(object obj)
        {
            if (obj is MD5Hash other)
                return Equals(other);

            return false;
        }

        public bool Equals(MD5Hash other)
        {
            return ((IStructuralEquatable)Value).Equals(other.Value, StructuralComparisons.StructuralEqualityComparer);
        }

        #endregion

        public override string ToString() => stringvalue ?? (stringvalue = Value.ToHex());

        public override int GetHashCode()
        {
            if (!hashcode.HasValue)
            {
                unchecked
                {
                    hashcode = (int)2166136261;
                    for (int i = 0; i < Value.Length; i++)
                        hashcode = (hashcode ^ Value[i]) * 16777619;

                    hashcode += hashcode << 13;
                    hashcode ^= hashcode >> 7;
                    hashcode += hashcode << 3;
                    hashcode ^= hashcode >> 17;
                    hashcode += hashcode << 5;
                }
            }

            return hashcode.Value;
        }
    }
}

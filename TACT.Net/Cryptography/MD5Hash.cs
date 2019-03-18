using System;
using System.Collections;
using TACT.Net.Common;

namespace TACT.Net.Cryptography
{
    public struct MD5Hash : IEquatable<MD5Hash>
    {
        public readonly byte[] Value;
        public readonly bool IsEmpty;

        // cache these values as this struct is readonly
        private int? hashcode;
        private string stringvalue;

        #region Constructors

        public MD5Hash(string hash)
        {
            Value = hash?.ToByteArray();
            hashcode = null;
            stringvalue = null;
            IsEmpty = Value == null || Value.Length == 0 || Array.TrueForAll(Value, x => x == 0);
        }

        public MD5Hash(byte[] hash)
        {
            Value = hash;
            hashcode = null;
            stringvalue = null;
            IsEmpty = Value == null || Value.Length == 0 || Array.TrueForAll(Value, x => x == 0);
        }

        #endregion

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
            // shortcuts
            if (IsEmpty || other.IsEmpty)
                return IsEmpty == other.IsEmpty;
            if (Value.Length != other.Value.Length)
                return false;
            if (Value[0] != other.Value[0])
                return false;

            return ((IStructuralEquatable)Value).Equals(other.Value, StructuralComparisons.StructuralEqualityComparer);
        }

        #endregion

        #region Static Initialisation

        public static MD5Hash Parse(string hash) => new MD5Hash(hash);

        public static MD5Hash Parse(byte[] hash) => new MD5Hash(hash);

        public static bool TryParse(string hash, out MD5Hash md5Hash)
        {
            return TryParse(hash?.ToByteArray(), out md5Hash);
        }

        public static bool TryParse(byte[] hash, out MD5Hash md5Hash)
        {
            if (hash == null || hash.Length == 0 || hash.Length > 16)
            {
                md5Hash = default;
                return false;
            }

            md5Hash = new MD5Hash(hash);
            return true;
        }

        #endregion

        public override string ToString()
        {
            return stringvalue ?? (stringvalue = Value.ToHex());
        }

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

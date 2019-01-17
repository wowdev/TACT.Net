﻿using System;
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

        #region Static Initialisation

        public static MD5Hash Parse(string hash) => new MD5Hash(hash);

        public static MD5Hash Parse(byte[] hash) => new MD5Hash(hash);

        public static bool TryParse(string hash, out MD5Hash md5Hash)
        {
            if (string.IsNullOrWhiteSpace(hash) || hash.Length > 32)
            {
                md5Hash = default(MD5Hash);
                return false;
            }

            md5Hash = new MD5Hash(hash);
            return true;                
        }

        public static bool TryParse(byte[] hash, out MD5Hash md5Hash)
        {
            if(hash == null || hash.Length == 0 || hash.Length > 16)
            {
                md5Hash = default(MD5Hash);
                return false;
            }

            md5Hash = new MD5Hash(hash);
            return true;
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

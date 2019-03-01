using System;
using System.Collections.Generic;

namespace TACT.Net.Tags
{
    internal static class TagTypeHelper
    {
        public static string TypeName(ushort type, uint build)
        {
            for (int i = 0; i < BuildLookup.Length; i++)
            {
                if (build <= BuildLookup[i])
                    return TypeNames.ContainsKey(type) ? TypeNames[type][i] : "";
            }

            return "";
        }

        public static ushort TypeId(string name, uint build)
        {
            for (int i = 0; i < BuildLookup.Length; i++)
            {
                if (build <= BuildLookup[i])
                {
                    foreach (var lookup in TypeNames)
                        if (lookup.Value[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                            return lookup.Key;

                    return ushort.MaxValue;
                }
            }

            return ushort.MaxValue;
        }

        #region Lookup

        private static readonly int[] BuildLookup = new[] { 18761, 20426, 99999 };

        private static readonly Dictionary<ushort, string[]> TypeNames = new Dictionary<ushort, string[]>
        {
            { 1,      new[] { "Architecture", "Architecture", "Platform" }      },
            { 2,      new[] { "Locale"      , "Category"    , "Architecture" }  },
            { 3,      new[] { "Platform"    , "Locale"      , "Locale" }        },
            { 4,      new[] { ""            , "Platform"    , "Region" }        },
            { 5,      new[] { ""            , "Region"      , "Category" }      },
            { 0x4000, new[] { ""            , ""            , "Alternate" }     },
        };

        #endregion
    }
}

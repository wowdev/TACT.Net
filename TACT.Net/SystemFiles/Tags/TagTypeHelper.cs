using System;
using System.Collections.Generic;

namespace TACT.Net.Tags
{
    using Lookup = Dictionary<uint, Dictionary<string, ushort>>;
    using LookupEntry = Dictionary<string, ushort>;

    internal static class TagTypeHelper
    {
        private const uint MaxBuild = 99999;

        public static string TypeName(ushort type, uint build)
        {
            build = Math.Min(build, MaxBuild);

            foreach (var entry in Lookup.Value)
            {
                if (build <= entry.Key)
                {
                    foreach (var item in entry.Value)
                        if (item.Value == type)
                            return item.Key;

                    return "";
                }
            }

            return "";
        }

        public static ushort TypeId(string name, uint build)
        {
            build = Math.Min(build, MaxBuild);

            foreach (var entry in Lookup.Value)
            {
                if (build <= entry.Key)
                {
                    entry.Value.TryGetValue(name, out var id);
                    return id;
                }
            }

            return ushort.MaxValue;
        }

        public static Dictionary<string, ushort> GetTypeIds(uint build)
        {
            build = Math.Min(build, MaxBuild);

            foreach (var entry in Lookup.Value)
                if (build <= entry.Key)
                    return entry.Value;

            return null;
        }

        #region Lookup

        private static readonly Lazy<Lookup> Lookup = new Lazy<Lookup>(() =>
        {
            return new Lookup
            {
                {
                    18761, new LookupEntry(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Architecture", 1 },
                        { "Locale", 2 },
                        { "Platform", 3 },
                    }
                },
                {
                    20426, new LookupEntry(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Architecture", 1 },
                        { "Category", 2 },
                        { "Locale", 3 },
                        { "Platform", 4 },
                        { "Region", 5 },
                    }
                },
                {
                    MaxBuild, new LookupEntry(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Platform", 1 },
                        { "Architecture", 2 },
                        { "Locale", 3 },
                        { "Region", 4 },
                        { "Category", 5 },
                        { "Alternate", 0x4000 },
                    }
                }
            };
        });

        #endregion
    }
}

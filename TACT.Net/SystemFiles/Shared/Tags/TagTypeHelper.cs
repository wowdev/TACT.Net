using System;

namespace TACT.Net.Shared.Tags
{
    // TODO make less awful
    internal static class TagTypeHelper
    {
        private static readonly (uint Build, Type Type)[] _TypeMap = new (uint, Type)[]
        {
            ( 18761, typeof(Types_18761) ),
            ( 20426, typeof(Types_20426) ),
            ( 99999, typeof(Types_99999) ),
        };

        public static string TypeName(int type, uint build)
        {
            foreach (var _ in _TypeMap)
            {
                if (build <= _.Build)
                {
                    if (!Enum.IsDefined(_.Type, type))
                        return "";

                    return Enum.Parse(_.Type, type.ToString()).ToString();
                }
            }

            return "";
        }

        #region Enums

        private enum Types_18761 : int
        {
            Architecture = 1,
            Locale = 2,
            Platform = 3
        }

        private enum Types_20426 : int
        {
            Architecture = 1,
            Category = 2,
            Locale = 3,
            Platform = 4,
            Region = 5
        }

        private enum Types_99999 : int
        {
            Platform = 1,
            Architecture = 2,
            Locale = 3,
            Region = 4,
            Category = 5,
            Alternate = 0x4000
        }

        #endregion
    }
}

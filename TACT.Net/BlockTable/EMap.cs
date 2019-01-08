namespace TACT.Net.BlockTable
{
    /// <summary>
    /// Encoding Type and Compression Level map
    /// </summary>
    public struct EMap
    {
        /// <summary>
        /// Encoding Type
        /// </summary>
        public EType Type;
        /// <summary>
        /// Compression Level
        /// </summary>
        public byte Level;
        /// <summary>
        /// 0 WindowBits
        /// </summary>
        public bool MPQ;

        public EMap(EType type, byte level, bool mpq = false)
        {
            Type = type;
            Level = level;
            MPQ = mpq;
        }

        /// <summary>
        /// Returns an ESpec representation of the map
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Type == EType.ZLib)
            {
                string value = "z";
                if (Level != 9 || MPQ)
                    value += ":{" + Level + (MPQ ? ",mpq" : "") + "}";
                return value;
            }

            return "n";
        }
    }
}

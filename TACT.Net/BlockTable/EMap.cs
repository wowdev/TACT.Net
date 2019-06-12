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

            // TODO - 0 windowbits appears to be unsupported by zlibwapi
            MPQ = false; // mpq;
        }

        /// <summary>
        /// Returns an ESpec representation of the map
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string value = new string((char)(Type + 0x20), 1);

            // additional information required if using a non-generic ZLib parameter
            if (Type == EType.ZLib && (Level != 9 || MPQ))
                value += $":{{{Level}{(MPQ ? ",mpq" : "")}}}";

            return value;
        }
    }
}

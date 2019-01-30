using System.IO;
using TACT.Net.Cryptography;

namespace TACT.Net.Encoding
{
    /// <summary>
    /// Base Encoding Key Entry
    /// </summary>
    public abstract class EncodingEntryBase
    {
        internal abstract MD5Hash Key { get; }
        internal abstract int Size { get; }

        public abstract bool Read(BinaryReader br, EncodingHeader header);
        public abstract void Write(BinaryWriter bw, EncodingHeader header);

        internal virtual void Validate()
        {
            if (Key.IsEmpty)
                throw new InvalidDataException("Invalid Key");
        }
    }
}

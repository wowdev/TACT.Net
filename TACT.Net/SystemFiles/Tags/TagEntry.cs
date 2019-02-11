using System.IO;
using TACT.Net.Common;

namespace TACT.Net.Tags
{
    public class TagEntry
    {
        public string Name;
        public ushort TypeId;
        public string TypeName(uint build) => TagTypeHelper.TypeName(TypeId, build);

        internal BoolArray FileMask;

        #region IO

        public virtual void Read(BinaryReader br, uint entryCount)
        {
            Name = br.ReadCString();
            TypeId = br.ReadUInt16BE();
            FileMask = new BoolArray(br, entryCount);
        }

        public virtual void Write(BinaryWriter bw)
        {
            bw.WriteCString(Name);
            bw.WriteUInt16BE(TypeId);
            bw.Write(FileMask.ToByteArray());
        }

        #endregion

    }
}

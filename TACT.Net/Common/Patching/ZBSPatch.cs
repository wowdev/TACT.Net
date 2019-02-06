using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace TACT.Net.Common.Patching
{
    public class ZBSPatch
    {
        private const long Signature = 0x314646494453425A; // ZBSDIFF1

        #region Constructors

        /// <summary>
        /// Applies a patch to a specific file
        /// </summary>
        /// <param name="input">Original unmodified file</param>
        /// <param name="patch">Stream of the ZBSDIFF1 patch</param>
        /// <param name="output">Output of the updated file</param>
        public void Apply(Stream input, Stream patch, Stream output)
        {
            ApplyImpl(input, patch, output);
        }

        #endregion


        #region Implementation

        private void ApplyImpl(Stream input, Stream patch, Stream output)
        {
            long outputSize = CreatePatchStreams(patch, out Stream ctrl, out Stream diff, out Stream extra);

            if (!input.CanRead || !input.CanSeek)
                throw new ArgumentException("Input stream must be readable and seekable");
            if (!output.CanWrite)
                throw new ArgumentException("Output stream must be writable");

            using (ctrl)
            using (diff)
            using (extra)
            using (var br = new BinaryReader(input))
            {
                while (output.Position < outputSize)
                {
                    // read control data:
                    // - add x bytes from original to x bytes from the diff block : o[x] = i[x] + d[x]
                    // - copy y bytes from the extra block : o[x] = e[x]
                    // - seek forwards in original by z bytes
                    long diffBlockSize = ctrl.ReadInt64BS();
                    long extraBlockSize = ctrl.ReadInt64BS();
                    long seekInInput = ctrl.ReadInt64BS();

                    if (output.Position + diffBlockSize + extraBlockSize > outputSize)
                        throw new InvalidOperationException("Corrupt patch");

                    // read diff block
                    // TODO test uint unrolling performance
                    foreach (byte[] newData in BufferedRead(diff, diffBlockSize))
                    {
                        // add old data to diff
                        byte[] inputData = br.ReadBytes(newData.Length);
                        for (int i = 0; i < newData.Length; i++)
                            newData[i] += inputData[i];

                        output.Write(newData);
                    }

                    // flat copy the extra block data into the output
                    extra.PartialCopyTo(output, extraBlockSize);

                    // adjust position
                    input.Seek(seekInInput, SeekOrigin.Current);
                }
            }
        }

        private long CreatePatchStreams(Stream patch, out Stream ctrl, out Stream diff, out Stream extra)
        {
            // read the header
            using (var br = new BinaryReader(patch))
            {
                // check patch stream capabilities
                if (!patch.CanRead || !patch.CanSeek)
                    throw new ArgumentException("Patch stream must be readable and seekable");

                // check the magic
                var signature = patch.ReadInt64BS();
                if (signature != Signature)
                    throw new FormatException($"Invalid signature. Expected {Signature} got {signature}.");

                // read lengths from header
                long controlSize = patch.ReadInt64BS();
                long diffSize = patch.ReadInt64BS();
                long outputSize = patch.ReadInt64BS();

                if (controlSize < 0 || diffSize < 0 || outputSize < 0)
                    throw new InvalidOperationException("Corrupt patch");

                // create a stream for each block
                ctrl = DecompressBlock(br.ReadBytes((int)controlSize));
                diff = DecompressBlock(br.ReadBytes((int)diffSize));
                extra = DecompressBlock(br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position))); // to EOF

                return outputSize;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads bytes from a stream in chunks
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="count"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        private IEnumerable<byte[]> BufferedRead(Stream stream, long count, int bufferSize = 0x1000)
        {
            int length = (int)count;
            if (length <= 0)
                yield break;

            using (var br = new BinaryReader(stream))
                for (; length > 0; length -= bufferSize)
                    yield return br.ReadBytes(Math.Min(length, bufferSize));
        }

        /// <summary>
        /// Deflates a byte array and returns a new stream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private Stream DecompressBlock(byte[] data)
        {
            MemoryStream outStream = new MemoryStream(data.Length);

            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var stream = new DeflateStream(ms, CompressionMode.Decompress))
                stream.CopyTo(outStream);

            outStream.Position = 0;
            return outStream;
        }

        #endregion
    }
}

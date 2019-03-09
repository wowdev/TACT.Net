using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

/*
    The original bsdiff.c source code (http://www.daemonology.net/bsdiff/) is
    distributed under the following license:
    Copyright 2003-2005 Colin Percival
    All rights reserved
    Redistribution and use in source and binary forms, with or without
    modification, are permitted providing that the following conditions 
    are met:
    1. Redistributions of source code must retain the above copyright
        notice, this list of conditions and the following disclaimer.
    2. Redistributions in binary form must reproduce the above copyright
        notice, this list of conditions and the following disclaimer in the
        documentation and/or other materials provided with the distribution.
    THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
    IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
    WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
    ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
    DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
    DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
    OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
    HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
    STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
    IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
    POSSIBILITY OF SUCH DAMAGE.
*/

namespace TACT.Net.Common.Patching
{
    internal static class ZBSPatch
    {
        private const long Signature = 0x314646494453425A; // ZBSDIFF1

        #region Methods

        /// <summary>
        /// Applies a patch to a specific file
        /// </summary>
        /// <param name="input">Original unmodified file</param>
        /// <param name="patch">Stream of the ZBSDIFF1 patch entry</param>
        /// <param name="output">Output of the updated file</param>
        public static void Apply(Stream input, Stream patch, Stream output)
        {
            ApplyImpl(input, patch, output);
        }

        #endregion

        #region Implementation

        private static void ApplyImpl(Stream input, Stream patch, Stream output)
        {
            long outputSize = CreatePatchStreams(patch, out Stream ctrl, out Stream diff, out Stream extra);

            if (!input.CanRead || !input.CanSeek)
                throw new ArgumentException("Input stream must be readable and seekable");
            if (!output.CanWrite)
                throw new ArgumentException("Output stream must be writable");

            // clear the output in the case of input == output
            output.SetLength(0);

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

        private static long CreatePatchStreams(Stream patch, out Stream ctrl, out Stream diff, out Stream extra)
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

                if (controlSize < 0 || diffSize < 0 || outputSize <= 0)
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
        private static IEnumerable<byte[]> BufferedRead(Stream stream, long count, int bufferSize = 0x1000)
        {
            int length = (int)count;
            if (length <= 0)
                yield break;

            for (; length > 0; length -= bufferSize)
            {
                byte[] buffer = new byte[Math.Min(length, bufferSize)];
                stream.Read(buffer);
                yield return buffer;
            }
        }

        /// <summary>
        /// Deflates a byte array and returns a new stream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static Stream DecompressBlock(byte[] data)
        {
            MemoryStream outStream = new MemoryStream(data.Length);

            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                ds.CopyTo(outStream);

            outStream.Capacity = (int)outStream.Length;
            outStream.Position = 0;
            return outStream;
        }

        #endregion
    }
}

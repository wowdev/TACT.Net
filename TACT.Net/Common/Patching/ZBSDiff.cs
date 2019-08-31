using System;
using System.IO;
using Joveler.Compression.ZLib;
using TACT.Net.Common.ZLib;

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
    internal static class ZBSDiff
    {
        private const long Signature = 0x314646494453425A; // ZBSDIFF1

        #region Methods

        public static void Create(byte[] original, byte[] modified, Stream output)
        {
            CreateImpl(original, modified, output);
        }

        #endregion

        #region Implementation

        private static void CreateImpl(Span<byte> original, Span<byte> modified, Stream output)
        {
            if (original == null || original.Length == 0)
                throw new ArgumentException(nameof(original));
            if (modified == null || modified.Length == 0)
                throw new ArgumentException(nameof(modified));
            if (output == null || !output.CanSeek || !output.CanRead)
                throw new ArgumentException("Output stream must be not null, readable and seekable");

            using (var ctrl = ZLibFactory.CreateStream(new MemoryStream(modified.Length), ZLibMode.Compress, ZLibCompLevel.BestCompression))
            using (var diff = ZLibFactory.CreateStream(new MemoryStream(modified.Length), ZLibMode.Compress, ZLibCompLevel.BestCompression))
            using (var extr = ZLibFactory.CreateStream(new MemoryStream(modified.Length), ZLibMode.Compress, ZLibCompLevel.BestCompression))
            {
                int scan = 0, pos = 0, len = 0, lastscan = 0, lastpos = 0, lastoffset = 0;
                int oldDataLen = original.Length, newDataLen = modified.Length;

                int[] I = SuffixSort(original);

                while (scan < newDataLen)
                {
                    int oldscore = 0;
                    for (int scsc = scan += len; scan < newDataLen; scan++)
                    {
                        len = Search(I, original, modified, scan, 0, oldDataLen, out pos);

                        for (; scsc < scan + len; scsc++)
                            if ((scsc + lastoffset < oldDataLen) && (original[scsc + lastoffset] == modified[scsc]))
                                oldscore++;

                        if ((len == oldscore && len != 0) || (len > oldscore + 8))
                            break;

                        if ((scan + lastoffset < oldDataLen) && (original[scan + lastoffset] == modified[scan]))
                            oldscore--;
                    }

                    if (len != oldscore || scan == newDataLen)
                    {
                        int s = 0, sf = 0, lenf = 0, lenb = 0;
                        for (var i = 0; (lastscan + i < scan) && (lastpos + i < oldDataLen);)
                        {
                            if (original[lastpos + i] == modified[lastscan + i])
                                s++;

                            i++;
                            if (s * 2 - i > sf * 2 - lenf)
                            {
                                sf = s;
                                lenf = i;
                            }
                        }

                        if (scan < newDataLen)
                        {
                            s = 0;
                            int sb = 0;
                            for (var i = 1; (scan >= lastscan + i) && (pos >= i); i++)
                            {
                                if (original[pos - i] == modified[scan - i])
                                    s++;

                                if (s * 2 - i > sb * 2 - lenb)
                                {
                                    sb = s;
                                    lenb = i;
                                }
                            }
                        }

                        if (lastscan + lenf > scan - lenb)
                        {
                            s = 0;

                            int overlap = lastscan + lenf - (scan - lenb), ss = 0, lens = 0;
                            for (var i = 0; i < overlap; i++)
                            {
                                if (modified[lastscan + lenf - overlap + i] == original[lastpos + lenf - overlap + i])
                                    s++;

                                if (modified[scan - lenb + i] == original[pos - lenb + i])
                                    s--;

                                if (s > ss)
                                {
                                    ss = s;
                                    lens = i + 1;
                                }
                            }

                            lenf += lens - overlap;
                            lenb -= lens;
                        }

                        // write diff chunk
                        byte[] buffer = new byte[lenf];
                        for (int i = 0; i < lenf; i++)
                            buffer[i] = (byte)(modified[lastscan + i] - original[lastpos + i]);
                        diff.Write(buffer);

                        // write extra chunk
                        var extraLength = scan - lenb - (lastscan + lenf);
                        if (extraLength > 0)
                            extr.Write(modified.Slice(lastscan + lenf, extraLength));

                        // write ctrl chunk
                        ctrl.WriteInt64BS(lenf);
                        ctrl.WriteInt64BS(extraLength);
                        ctrl.WriteInt64BS(pos - lenb - (lastpos + lenf));

                        lastscan = scan - lenb;
                        lastpos = pos - lenb;
                        lastoffset = pos - scan;
                    }
                }

                // flush the streams
                ctrl.Flush(); diff.Flush(); extr.Flush();

                // generate the output stream
                output.WriteInt64BS(Signature);
                output.WriteInt64BS(ctrl.TotalOut); // controlSize
                output.WriteInt64BS(diff.TotalOut); // diffSize
                output.WriteInt64BS(modified.Length); // outputSize
                (ctrl.BaseStream as MemoryStream).WriteTo(output);
                (diff.BaseStream as MemoryStream).WriteTo(output);
                (extr.BaseStream as MemoryStream).WriteTo(output);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Implementation of qsufsort
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static int[] SuffixSort(Span<byte> data)
        {
            int[] buckets = new int[256];

            foreach (byte b in data)
                buckets[b]++;
            for (int i = 1; i < 256; i++)
                buckets[i] += buckets[i - 1];
            for (int i = 255; i > 0; i--)
                buckets[i] = buckets[i - 1];
            buckets[0] = 0;

            int[] I = new int[data.Length + 1];
            for (int i = 0; i < data.Length; i++)
                I[++buckets[data[i]]] = i;

            int[] v = new int[data.Length + 1];
            for (int i = 0; i < data.Length; i++)
                v[i] = buckets[data[i]];

            for (int i = 1; i < 256; i++)
                if (buckets[i] == buckets[i - 1] + 1)
                    I[buckets[i]] = -1;

            I[0] = -1;

            for (int h = 1; I[0] != -(data.Length + 1); h += h)
            {
                int len = 0, i = 0;
                while (i < data.Length + 1)
                {
                    if (I[i] < 0)
                    {
                        len -= I[i];
                        i -= I[i];
                    }
                    else
                    {
                        if (len != 0)
                            I[i - len] = -len;
                        len = v[I[i]] + 1 - i;
                        Split(I, v, i, len, h);
                        i += len;
                        len = 0;
                    }
                }

                if (len != 0)
                    I[i - len] = -len;
            }

            for (int i = 0; i < data.Length + 1; i++)
                I[v[i]] = i;

            return I;
        }

        private static void Split(int[] I, int[] v, int start, int len, int h)
        {
            #region Swap
            int tmp;
            void Swap(ref int x, ref int y)
            {
                tmp = x; x = y; y = tmp;
            }
            #endregion

            if (len < 16)
            {
                int j, x, i;
                for (int k = start; k < start + len; k += j)
                {
                    j = 1;
                    x = v[I[k] + h];
                    for (i = 1; k + i < start + len; i++)
                    {
                        if (v[I[k + i] + h] < x)
                        {
                            x = v[I[k + i] + h];
                            j = 0;
                        }

                        if (v[I[k + i] + h] == x)
                        {
                            Swap(ref I[k + j], ref I[k + i]);
                            j++;
                        }
                    }

                    for (i = 0; i < j; i++)
                        v[I[k + i]] = k + j - 1;

                    if (j == 1)
                        I[k] = -1;
                }
            }
            else
            {
                int x = v[I[start + len / 2] + h];
                int jj = 0, kk = 0;
                for (int i2 = start; i2 < start + len; i2++)
                {
                    if (v[I[i2] + h] < x)
                        jj++;
                    if (v[I[i2] + h] == x)
                        kk++;
                }
                jj += start;
                kk += jj;

                int i = start, j = 0, k = 0;
                while (i < jj)
                {
                    if (v[I[i] + h] < x)
                    {
                        i++;
                    }
                    else if (v[I[i] + h] == x)
                    {
                        Swap(ref I[i], ref I[jj + j]);
                        j++;
                    }
                    else
                    {
                        Swap(ref I[i], ref I[kk + k]);
                        k++;
                    }
                }

                while (jj + j < kk)
                {
                    if (v[I[jj + j] + h] == x)
                    {
                        j++;
                    }
                    else
                    {
                        Swap(ref I[jj + j], ref I[kk + k]);
                        k++;
                    }
                }

                if (jj > start)
                    Split(I, v, start, jj - start, h);

                for (i = 0; i < kk - jj; i++)
                    v[I[jj + i]] = kk - 1;

                if (jj == kk - 1)
                    I[jj] = -1;

                if (start + len > kk)
                    Split(I, v, kk, start + len - kk, h);
            }
        }


        private static int Search(int[] I, Span<byte> original, Span<byte> modified, int newOffset, int start, int end, out int pos)
        {
            if (end - start < 2)
            {
                int startLength = MatchLength(original, I[start], modified, newOffset);
                int endLength = MatchLength(original, I[end], modified, newOffset);

                if (startLength > endLength)
                {
                    pos = I[start];
                    return startLength;
                }

                pos = I[end];
                return endLength;
            }
            else
            {
                int midPoint = start + (end - start) / 2;
                return CompareBytes(original, I[midPoint], modified, newOffset) < 0 ?
                    Search(I, original, modified, newOffset, midPoint, end, out pos) :
                    Search(I, original, modified, newOffset, start, midPoint, out pos);
            }
        }

        private static int CompareBytes(Span<byte> left, int leftOffset, Span<byte> right, int rightOffset)
        {
            int diff;
            for (int index = 0; index < left.Length - leftOffset && index < right.Length - rightOffset; index++)
            {
                diff = left[index + leftOffset] - right[index + rightOffset];
                if (diff != 0)
                    return diff;
            }

            return 0;
        }

        private static int MatchLength(Span<byte> original, int orgOffset, Span<byte> modified, int modOffset)
        {
            int i;
            for (i = 0; i < original.Length - orgOffset && i < modified.Length - modOffset; i++)
                if (original[i + orgOffset] != modified[i + modOffset])
                    break;

            return i;
        }

        #endregion
    }
}

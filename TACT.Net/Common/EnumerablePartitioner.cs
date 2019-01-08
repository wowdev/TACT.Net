using System;
using System.Collections.Generic;

namespace TACT.Net.Common
{
    internal static class EnumerablePartitioner
    {
        /// <summary>
        /// Fully Lazy Enumerable Partitioning.
        /// <para>Note: Requires full enumeration before conformity</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="batchSize"></param>
        /// <param name="sizeFunc"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> LazyBatch<T>(IEnumerable<T> source, long batchSize, Func<T, long> sizeFunc)
        {
            using (var enumerator = source.GetEnumerator())
                while (enumerator.MoveNext())
                    yield return Batch(enumerator);

            IEnumerable<T> Batch(IEnumerator<T> enumerator)
            {
                long size = sizeFunc.Invoke(enumerator.Current);
                yield return enumerator.Current;

                while (size < batchSize && enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                    size += sizeFunc.Invoke(enumerator.Current);
                }
            }
        }

        /// <summary>
        /// Concreted Enumerable Parititoning.
        /// <para>Note: Entries are reallocated to Lists</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="batchSize"></param>
        /// <param name="sizeFunc"></param>
        /// <returns></returns>
        public static IEnumerable<List<T>> ConcreteBatch<T>(IEnumerable<T> source, long batchSize, Func<T, long> sizeFunc)
        {
            // capacity based on pageSizes usually being 0x1000 and EncodingEKeyEntry being 0x19
            const int Capacity = 0xA5;

            List<T> partition = new List<T>(Capacity);

            long size = 0;
            foreach (var entry in source)
            {
                long entrySize = sizeFunc.Invoke(entry);

                if (size + entrySize > batchSize)
                {
                    yield return partition;
                    partition = new List<T>(Capacity);
                    size = 0;
                }

                size += entrySize;
                partition.Add(entry);
            }

            if (partition.Count > 0)
                yield return partition;
        }
    }
}

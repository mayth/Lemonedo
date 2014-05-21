using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Linq
{
    public static class EnumerableEx
    {
        public static IEnumerable<T[]> Slice<T>(this IEnumerable<T> collection, int count)
        {
            return Slice<T, T[]>(collection, count, x => x.ToArray());
        }

        public static IEnumerable<TResult> Slice<TSource, TResult>(this IEnumerable<TSource> collection, int count, Func<IEnumerable<TSource>, TResult> resultSelector)
        {
            var result = new List<TSource>(count);
            foreach (var v in collection)
            {
                result.Add(v);
                if (result.Count == count)
                {
                    yield return resultSelector(result);
                    result.Clear();
                }
            }
            if (0 < result.Count)
                yield return resultSelector(result);
        }
    }
}

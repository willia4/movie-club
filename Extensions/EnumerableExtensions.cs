using System.Collections.Immutable;

namespace zinfandel_movie_club;

public static class EnumerableExtensions
{
    /// <summary>
    /// Allocates the items from the enumerable into at-most <see cref="bucketCount"/> buckets
    /// </summary>
    /// <remarks>The order of the items in the buckets is not guaranteed</remarks>
    public static ImmutableList<ImmutableList<T>> ToBuckets<T>(this IEnumerable<T> items, int bucketCount)
    {
        var buckets =
            Enumerable.Range(0, bucketCount)
            .Select(_ => ImmutableList<T>.Empty)
            .ToList();

        var i = 0;
        foreach (var item in items)
        {
            buckets[i] = buckets[i].Add(item);
            i = (i + 1) % bucketCount;
        }
        
        return buckets.Where(l => !l.IsEmpty).ToImmutableList();
    }
}
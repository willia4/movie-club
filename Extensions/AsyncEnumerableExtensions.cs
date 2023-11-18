using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace zinfandel_movie_club;

public static class AsyncEnumerableExtensions
{
    public static async Task<ImmutableList<T>> ToImmutableList<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken)
    {
        var r = ImmutableList<T>.Empty;
        
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            r = r.Add(item);
        }

        return r;
    }

    public static async Task<ImmutableHashSet<T>> ToImmutableHashSet<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken)
    {
        var r = ImmutableHashSet<T>.Empty;
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            r = r.Add(item);
        }
        
        return r;
    }
    
    public static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken)
    {
        var r = new List<T>();
        
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            r.Add(item);
        }

        var x = r.Cast<object>();
        return r;
    }

    public static async Task<IEnumerable<T>> ToEnumerable<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken)
    {
        var list = await items.ToImmutableList(cancellationToken);
        return list;
    }

    public static async IAsyncEnumerable<U> Select<T, U>(this IAsyncEnumerable<T> items, Func<T, U> projection, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            yield return projection(item);
        }
    }

    public static async IAsyncEnumerable<T> Where<T>(this IAsyncEnumerable<T> items, Func<T, bool> predicate, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            if (predicate(item)) yield return item;
        }
    }

    public static async IAsyncEnumerable<U> OfType<U>(this IAsyncEnumerable<object> items, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            if (item is U value)
            {
                yield return value; 
            }
        }
    }

    public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken)
    {
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            return item;
        }

        return default;
    }

    public static async IAsyncEnumerable<T> TakeUpTo<T>(this IAsyncEnumerable<T> items, int max, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            if (count >= max) yield break;
            yield return item;
            count++;
        }
    }
}
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos.Linq;

namespace zinfandel_movie_club;
using Microsoft.Azure.Cosmos;
    
public static class CosmosExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this FeedIterator<T> iterator, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (iterator == null) yield break;
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                yield return item;
            }
        }
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IQueryable<T> query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (query == null) yield break;
        
        FeedIterator<T>? feedIterator = null;
        try
        {
            feedIterator = query.ToFeedIterator();
        }
        catch { }

        if (feedIterator != null)
        {
            await foreach (var item in feedIterator.ToAsyncEnumerable(cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            throw new ArgumentException($"Could not convert {query.GetType().FullName} to an IAsyncEnumerable");            
        }
    }

    public static async Task<ImmutableList<T>> ToImmutableList<T>(this FeedIterator<T> iterator, CancellationToken cancellationToken)
    {
        var r = ImmutableList<T>.Empty;
        await foreach (var item in iterator.ToAsyncEnumerable(cancellationToken))
        {
            r = r.Add(item);
        }
        
        return r;
    }

    public static async Task<ImmutableList<T>> ToImmutableList<T>(this IQueryable<T> query, CancellationToken cancellationToken)
    {
        var r = ImmutableList<T>.Empty;
        await foreach (var item in query.ToAsyncEnumerable(cancellationToken))
        {
            r = r.Add(item);
        }
        
        return r;
    }
}
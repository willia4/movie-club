using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Net.Http.Headers;

namespace zinfandel_movie_club.Data;

public interface IPurgeableMemoryCache<KeyT, ValueT> : IEnumerable<KeyValuePair<KeyT, ValueT>>
{
    public Task<ValueT> GetOrCreateAsync(KeyT key, Func<KeyT, CancellationToken, Task<(TimeSpan?, ValueT)>> f, CancellationToken cancellationToken = default);
    public bool Contains(KeyT key);
    public bool TryGetValue(KeyT key, out ValueT v);
    public bool TryRemove(KeyT key, out ValueT v);
    public void Remove(KeyT key);
    public void Purge();
    public ValueT Set(KeyT key, TimeSpan? expiration, ValueT newValue);
}

public class PurgeableMemoryCache<KeyT, ValueT> : IPurgeableMemoryCache<KeyT, ValueT> where KeyT: notnull
{
    private record CacheEntry(DateTimeOffset? ExpiresOn, ValueT Value);
    private readonly ConcurrentDictionary<KeyT, CacheEntry> _cache = new();

    public ValueT Set(KeyT key, TimeSpan? expiration, ValueT newValue)
    {
        var now = System.DateTime.Now;
        var expiresOn = expiration switch
        {
            null => null as DateTimeOffset?,
            TimeSpan xd when xd < TimeSpan.Zero => throw new InvalidOperationException("Cannot set a negative expiration date"),
            TimeSpan xd => now.Add(xd)
        };

        var newCacheEntry = new CacheEntry(ExpiresOn: expiresOn, Value: newValue);
        _cache[key] = newCacheEntry;
        return newValue;
    }
    
    public async Task<ValueT> GetOrCreateAsync(KeyT key, Func<KeyT, CancellationToken, Task<(TimeSpan?, ValueT)>> f, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGetValue(key, out var v)) return v;

        var (expiration, newValue) = await f(key, cancellationToken);
        return Set(key, expiration, newValue);
    }

    public bool Contains(KeyT key) =>  TryGetValue(key, out var _);

    public bool TryGetValue(KeyT key, out ValueT v)
    {
        var now = DateTimeOffset.Now;
        if (_cache.TryGetValue(key, out var cacheEntry))
        {
            if (cacheEntry.ExpiresOn > now)
            {
                v = cacheEntry.Value;
                return true;
            }
        }

        v = default!;
        return false;
    }

    public bool TryRemove(KeyT key, out ValueT v)
    {
        var now = DateTimeOffset.Now;
        if (_cache.TryRemove(key, out var cacheEntry))
        {
            if (cacheEntry.ExpiresOn > now)
            {
                v = cacheEntry.Value;
                return true;
            }
        }

        v = default!;
        return false;
    }

    public void Remove(KeyT key)
    {
        TryRemove(key, out var _);
    }

    public void Purge()
    {
        _cache.Clear();
    }

    public IEnumerator<KeyValuePair<KeyT, ValueT>> GetEnumerator()
    {
        // work from a moment-in-time idea of what "now" is; this will be constant throughout the iteration
        var now = DateTimeOffset.Now;
        
        // work from a moment-in-time snapshot of the array  
        var items = _cache.ToArray();

        return
            items
                .Where(kvp => kvp.Value.ExpiresOn > now)
                .Select(oldKvp => new KeyValuePair<KeyT, ValueT>(oldKvp.Key, oldKvp.Value.Value))
                .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
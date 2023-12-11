﻿using System.Collections.Immutable;
using CommunityToolkit.HighPerformance;

namespace zinfandel_movie_club;

public static class ArrayExtensions
{
    public static Stream AsStream(this ImmutableArray<byte> data) => data.AsMemory().AsStream(); 
    public static BinaryData AsBinaryData(this ImmutableArray<byte> data) => new BinaryData(data.AsMemory());
}

public static class IEnumerableExtensions
{
    public static IReadOnlyDictionary<K, V> ToReadonlyDictionary<K, V>(this IEnumerable<(K, V)> items) where K : notnull
    {
        return items.ToDictionary(t => t.First(), t => t.Second()).ToImmutableDictionary();
    }
}
using System.Collections.Immutable;
using CommunityToolkit.HighPerformance;

namespace zinfandel_movie_club;

public static class ArrayExtensions
{
    public static Stream AsStream(this ImmutableArray<byte> data) => data.AsMemory().AsStream(); 
    public static BinaryData AsBinaryData(this ImmutableArray<byte> data) => new BinaryData(data.AsMemory());
}
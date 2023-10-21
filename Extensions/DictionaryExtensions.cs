namespace zinfandel_movie_club;

public static class DictionaryExtensions
{
    public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key, V defaultValue) => dict.TryGetValue(key, out var value) ? value : defaultValue;
}
namespace zinfandel_movie_club;

public static class TimeSpanExtensions
{
    public static async Task<T> WithTimeoutAsync<T>(this TimeSpan ts, Func<CancellationToken, Task<T>> f)
    {
        using var cts = new CancellationTokenSource(ts);
        return await f(cts.Token);
    }

    public static async Task WithTimeoutAsync(this TimeSpan ts, Func<CancellationToken, Task> f)
    {
        using var cts = new CancellationTokenSource(ts);
        await f(cts.Token);
    }
    
    public static T WithTimeout<T>(this TimeSpan ts, Func<CancellationToken, T> f)
    {
        using var cts = new CancellationTokenSource(ts);
        return f(cts.Token);
    }

    public static void WithTimeout(this TimeSpan ts, Action<CancellationToken> f)
    {
        using var cts = new CancellationTokenSource(ts);
        f(cts.Token);
    }
}

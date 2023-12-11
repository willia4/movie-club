namespace zinfandel_movie_club;

public static class UriExtensions
{
    public static string FileExtension(this Uri uri)
    {
        var path = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path)) return "";

        var lastDot = path.LastIndexOf('.');
        if (lastDot < 0) return "";

        if (lastDot >= path.Length - 1) return "";
        return path.Substring(lastDot + 1);
    }

    public static T First<T, U>(this (T, U) t)
    {
        var (f, _) = t;
        return f;
    }
    
    public static U Second<T, U>(this (T, U) t)
    {
        var (_, s) = t;
        return s;
    }
}
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
}
namespace zinfandel_movie_club;

public static class StringExtensions
{
    public static Exception ToException(this string s) => new StringException(s);
    public static Exception ToException(this string s, Exception inner) => new StringException(s);

    public static string Or(this string s, string? orElse) => string.IsNullOrWhiteSpace(s) ? (orElse ?? "") : s;
}

public class StringException : Exception
{
    public StringException(string stringMessage) : base(stringMessage)
    {
        
    }

    public StringException(string stringMessage, Exception inner) : base(stringMessage, inner)
    {
        
    }
}
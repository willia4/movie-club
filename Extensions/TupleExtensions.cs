namespace zinfandel_movie_club;

public static class TupleExtensions
{
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
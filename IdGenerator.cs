namespace zinfandel_movie_club;
using NanoidDotNet;

public interface IIdGenerator
{
    public string MovieId();
    public string UserId();
    public string MovieRatingId();
}

public class IdGenerator : IIdGenerator
{
    // https://github.com/CyberAP/nanoid-dictionary/blob/master/src/nolookalikes-safe.js but all lowercase
    // we need lowercase ids because we have "options.LowercaseUrls" enabled and it can't tell the difference
    // between razor page names in the routes and ids in the routes. 
    //
    // see https://zelark.github.io/nano-id-cc/ for probability calculations
    private const string Alphabet = "26789abcdfghijkmnpqrtwz";
    private const int NanoIdLength = 17;

    private string GenerateId(string code) =>
        (code.Length == 3)
            ? $"1{code}{Nanoid.Generate(alphabet: Alphabet, size: NanoIdLength)}" 
            : throw new ArgumentException($"Id code must be of length 3; was '{code}", nameof(code));
    
    public string MovieId() => GenerateId("mov");
    public string UserId() => GenerateId("usr");
    public string MovieRatingId() => GenerateId("urt");
}

public class MovieIdGenerator
{
    private readonly IIdGenerator _id;

    public MovieIdGenerator(IIdGenerator id)
    {
        _id = id;
    }

    public string NewId() => _id.MovieId();
}

public class UserIdGenerator
{
    private readonly IIdGenerator _id;

    public UserIdGenerator(IIdGenerator id)
    {
        _id = id;
    }

    public string NewId() => _id.UserId();
}

public class UserRatingIdGenerator
{
    private readonly IIdGenerator _id;

    public UserRatingIdGenerator(IIdGenerator id)
    {
        _id = id;
    }

    public string NewId() => _id.MovieRatingId();
}
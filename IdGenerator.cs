namespace zinfandel_movie_club;
using NanoidDotNet;

public interface IIdGenerator
{
    public string MovieId();
    public string UserId();
}

public class IdGenerator : IIdGenerator
{
    // https://github.com/CyberAP/nanoid-dictionary/blob/master/src/nolookalikes-safe.js
    private const string Alphabet = "6789BCDFGHJKLMNPQRTWbcdfghjkmnpqrtwz";
    private const int NanoIdLength = 15;

    private string GenerateId(string code) =>
        (code.Length == 3)
            ? $"1{code}{Nanoid.Generate(alphabet: Alphabet, size: NanoIdLength)}" 
            : throw new ArgumentException($"Id code must be of length 3; was '{code}", nameof(code));
    
    public string MovieId() => GenerateId("mov");
    public string UserId() => GenerateId("usr");
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


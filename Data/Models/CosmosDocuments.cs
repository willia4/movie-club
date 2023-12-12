using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json.Serialization;

namespace zinfandel_movie_club.Data.Models;

public abstract class CosmosDocument
{
    public string? id { get; set; }
    public abstract string? DocumentType { get; }
}

public interface IUserProfile
{
    public string? id { get; }
    public string? DisplayName { get; }
    public string? Role { get; } 
}

public class UserProfileData : CosmosDocument, IUserProfile
{
    public static string _DocumentType => "UserProfile";
    public override string? DocumentType => _DocumentType;
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
}

public class MovieDocument : CosmosDocument
{
    public static string _DocumentType => "Movie";
    public override string DocumentType => _DocumentType;

    public string Title { get; init; } = "";
    public string? Overview { get; init; }
    public decimal? RottenTomatoesCriticScore { get; init; }
    public decimal? RottenTomatoesUserScore { get; init; }
    public int? RuntimeMinutes { get; init; }
    public string? ReleaseDate { get; init; }
    public string? TmdbId { get; init; }
    public string? CoverImageTimeStamp { get; init; }
    public string? CoverImageBlobPrefix { get; init; }
    public Dictionary<string, string> CoverImagesBySize { get; init; } = new();
    public Dictionary<string, decimal> UserRatings = new();

    public string SlugId()
    {
        var spaces = new System.Text.RegularExpressions.Regex("\\s+");
        var title = WebUtility.UrlEncode(spaces.Replace(Title, "-").ToLowerInvariant());
        
        // assume that the id won't contain hyphens because it's not in the id generator alphabet
        return $"{id}-{title}";
    }

    public static string IdFromSlugId(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "";
        var firstHyphen = slug.IndexOf("-", StringComparison.OrdinalIgnoreCase);
        return firstHyphen >= 0 ? slug.Substring(0, firstHyphen).Trim() : slug.Trim();
    }
}
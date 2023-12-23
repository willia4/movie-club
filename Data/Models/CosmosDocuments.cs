using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json.Serialization;

namespace zinfandel_movie_club.Data.Models;

public abstract class CosmosDocument
{
    public string? id { get; set; }
    public abstract string? DocumentType { get; }
}

public class UserProfileData : CosmosDocument
{
    public static string _DocumentType => "UserProfile";
    public override string? DocumentType => _DocumentType;
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    
    public string? ProfileImageTimeStamp { get; set; }
    public string? ProfileImageBlobPrefix { get; set; }
    
    public Dictionary<string, string> ProfileImagesBySize { get; set; } = new();
}

public class MovieDocument : CosmosDocument
{
    public static string _DocumentType => "Movie";
    public override string DocumentType => _DocumentType;

    public string Title { get; set; } = "";
    public string? Overview { get; set; }
    public decimal? RottenTomatoesCriticScore { get; set; }
    public decimal? RottenTomatoesUserScore { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? ReleaseDate { get; set; }
    public string? TmdbId { get; set; }
    public string? CoverImageTimeStamp { get; set; }
    public string? CoverImageBlobPrefix { get; set; }
    public List<DateOnly> WatchedDates { get; set; } = new();
    public DateOnly? MostRecentWatchedDate { get; set; } = null;
    public DateTimeOffset? DateAdded { get; set; } = null; 
    public Dictionary<string, string> CoverImagesBySize { get; set; } = new();

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

public class UserRatingDocument : CosmosDocument
{
    public static string _DocumentType => "UserRating";
    public override string DocumentType => _DocumentType;

    public string UserId { get; set; } = "";
    public string MovieId { get; set; } = "";
    public decimal Rating { get; set; } = 0.0M;
}
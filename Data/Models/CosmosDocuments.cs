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

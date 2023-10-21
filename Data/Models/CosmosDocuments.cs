using System.Text.Json.Serialization;

namespace zinfandel_movie_club.Data.Models;

public class CosmosDocument
{
    public string Id { get; init; }
    public string EnvironmentId { get; init; }
    public string DocumentType { get; init; }

    protected CosmosDocument(string id, string environmentId, string documentType)
    {
        Id = (id ?? "").Trim();
        EnvironmentId = (environmentId ?? "").Trim();
        DocumentType = (documentType ?? "").Trim();
    }
}

public interface IUserProfile
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Role { get; } 
}

public class UserProfile : CosmosDocument, IUserProfile
{
    public static string _DocumentType => "UserProfile";
    public string DisplayName { get; init; }
    public string Role { get; init; }
    
    public UserProfile(string environmentId, string documentType, string id, string displayName, string role) : base(environmentId: environmentId, documentType: documentType, id: id)
    {
        DisplayName = (displayName ?? "").Trim();
        Role = (role ?? "").Trim();
    }
}

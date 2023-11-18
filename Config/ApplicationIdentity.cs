namespace zinfandel_movie_club.Config;

public sealed class ApplicationIdentity
{
    public string TenantId { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
}


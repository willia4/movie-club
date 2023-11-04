using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public interface ISuperUserIdentifier
{
    public bool UserIsSuperuser(string? user);
    public bool UserIsSuperuser(ClaimsPrincipal? user);
    public bool UserIsSuperuser(IGraphUser? user);
}
public class SuperUserIdentifier : ISuperUserIdentifier
{
    private readonly ImmutableList<string> _superUserIds;

    public SuperUserIdentifier(IOptions<AppSettings> appSettings)
    {
        _superUserIds = appSettings.Value.SuperUserIds.ToImmutableList();
    }

    public bool UserIsSuperuser(string? user) => user switch
    {
        string => _superUserIds.Contains(user),
        _ => false
    };

    public bool UserIsSuperuser(ClaimsPrincipal? user) => UserIsSuperuser(user?.NameIdentifier());
    public bool UserIsSuperuser(IGraphUser? user) => UserIsSuperuser(user?.NameIdentifier);
}
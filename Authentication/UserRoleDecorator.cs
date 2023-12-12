using System.Collections.Immutable;
using System.Security.Claims;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public interface IUserRoleDecorator
{
    public bool IsAdmin(IGraphUser user);
    public bool IsMember(IGraphUser user);
    public bool IsSuperUser(IGraphUser user);

    public bool IsAdmin(string id, string? userRole);
    public bool IsMember(string id, string? userRole);
    public bool IsSuperUser(string id, string? userRole);
    
    public IEnumerable<Claim> RoleClaims(IGraphUser user);
}

public class UserRoleDecorator : IUserRoleDecorator
{
    private readonly ISuperUserIdentifier _superUserIdentifier;

    public UserRoleDecorator(ISuperUserIdentifier superUserIdentifier)
    {
        _superUserIdentifier = superUserIdentifier;
    }

    private static bool TestRole(string? userRole, string testRole) => string.Equals(userRole, testRole, StringComparison.OrdinalIgnoreCase);
    
    public bool IsAdmin(IGraphUser user) => IsAdmin(user.NameIdentifier, user.UserRole);

    public bool IsMember(IGraphUser user) => IsMember(user.NameIdentifier, user.UserRole);

    public bool IsSuperUser(IGraphUser user) => IsSuperUser(user.NameIdentifier, user.UserRole);

    public bool IsAdmin(string id, string? userRole) => IsSuperUser(id, userRole) || TestRole(userRole, AuthenticationExtensions.AdminRole);
    public bool IsMember(string id, string? userRole) => IsAdmin(id, userRole) || TestRole(userRole, AuthenticationExtensions.MemberRole);
    public bool IsSuperUser(string id, string? userRole) => _superUserIdentifier.UserIsSuperuser(id);

    public IEnumerable<Claim> RoleClaims(IGraphUser user)
    {
        static string s(bool v) => v ? "true" : "false";
        
        return 
            (string.IsNullOrWhiteSpace(user.UserRole) 
                ? ImmutableList<(string, string)>.Empty
                : ImmutableList<(string, string)>.Empty.Add((AuthenticationExtensions.UserRoleClaimType, user.UserRole)))
            .Add((AuthenticationExtensions.UserIsSuperUserClaimType, s(IsSuperUser(user))))
            .Add((AuthenticationExtensions.UserIsAdminClaimType, s(IsAdmin(user))))
            .Add((AuthenticationExtensions.UserIsMemberClaimType, s(IsMember(user))))
            .Select(t => new Claim(t.First(), t.Second()));
    }
}
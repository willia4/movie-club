using System.Collections.Immutable;
using System.Security.Claims;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public interface IUserRoleDecorator
{
    public bool IsAdmin(HttpRequest request, IGraphUser user);
    public bool IsMember(HttpRequest request, IGraphUser user);
    public bool IsSuperUser(HttpRequest request, IGraphUser user);

    public bool IsAdmin(HttpRequest request, string id, string? userRole);
    public bool IsMember(HttpRequest request, string id, string? userRole);
    public bool IsSuperUser(HttpRequest request, string id, string? userRole);
    
    public IEnumerable<Claim> RoleClaims(HttpRequest request, IGraphUser user);
}

public class UserRoleDecorator : IUserRoleDecorator
{
    private readonly ISuperUserIdentifier _superUserIdentifier;

    public UserRoleDecorator(ISuperUserIdentifier superUserIdentifier)
    {
        _superUserIdentifier = superUserIdentifier;
    }

    private static bool TestRole(string? userRole, string testRole) => string.Equals(userRole, testRole, StringComparison.OrdinalIgnoreCase);

    private static bool AllowAdmin(HttpRequest? request) => !(request?.Query?.ContainsKey("no-admin") ?? false);
    private bool IsSuperUserReal(string id, string? userRole) => _superUserIdentifier.UserIsSuperuser(id);
    private bool IsAdminReal(string id, string? userRole) => IsSuperUserReal(id, userRole) || TestRole(userRole, AuthenticationExtensions.AdminRole);
    
    
    public bool IsAdmin(HttpRequest request, IGraphUser user) => IsAdmin(request, user.NameIdentifier, user.UserRole);
    public bool IsMember(HttpRequest request, IGraphUser user) => IsMember(request, user.NameIdentifier, user.UserRole);
    public bool IsSuperUser(HttpRequest request, IGraphUser user) => IsSuperUser(request, user.NameIdentifier, user.UserRole);


    public bool IsAdmin(HttpRequest request, string id, string? userRole) => AllowAdmin(request) && IsAdminReal(id, userRole);
    public bool IsMember(HttpRequest request, string id, string? userRole) => IsAdminReal(id, userRole) || TestRole(userRole, AuthenticationExtensions.MemberRole);
    public bool IsSuperUser(HttpRequest request, string id, string? userRole) => AllowAdmin(request) && IsSuperUserReal(id, userRole);

    public IEnumerable<Claim> RoleClaims(HttpRequest request, IGraphUser user)
    {
        static string s(bool v) => v ? "true" : "false";
        
        return 
            (string.IsNullOrWhiteSpace(user.UserRole) 
                ? ImmutableList<(string, string)>.Empty
                : ImmutableList<(string, string)>.Empty.Add((AuthenticationExtensions.UserRoleClaimType, user.UserRole)))
            .Add((AuthenticationExtensions.UserIsSuperUserClaimType, s(IsSuperUser(request, user))))
            .Add((AuthenticationExtensions.UserIsAdminClaimType, s(IsAdmin(request, user))))
            .Add((AuthenticationExtensions.UserIsMemberClaimType, s(IsMember(request, user))))
            .Select(t => new Claim(t.First(), t.Second()));
    }
}
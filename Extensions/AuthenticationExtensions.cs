using System.Security.Claims;
using static zinfandel_movie_club.Authentication.ClaimRoleDecoratorMiddleware;
namespace zinfandel_movie_club;

public static class AuthenticationExtensions
{
    public const string UserRoleClaimType = "x-club-role";
    public const string UserIsSuperUserClaimType = "x-is-superuser";
    public const string UserIsAdminClaimType = "x-is-admin";
    public const string UserIsMemberClaimType = "x-is-member";
    public const string DisplayNameClaimType = "x-display-name";
    
    private static IEnumerable<Claim> SafeClaims(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal switch
        {
            ClaimsPrincipal => claimsPrincipal.Claims ?? Enumerable.Empty<Claim>(),
            _ => Enumerable.Empty<Claim>()
        };

    public static string? ClaimOfType(this ClaimsPrincipal claimsPrincipal, string claimType) =>
        claimsPrincipal
            .SafeClaims()
            .Where(c => c.Type == claimType)
            .Select(c => c.Value)
            .FirstOrDefault();

    public static string? NameIdentifier(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal.ClaimOfType(ClaimTypes.NameIdentifier);

    public static string? UserRole(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal.ClaimOfType(UserRoleClaimType);

    public const string AdminRole = "Admin";
    public const string MemberRole = "Member";

    public static bool IsSuperUser(this ClaimsPrincipal claimsPrincipal) =>
        string.Equals("true", claimsPrincipal.ClaimOfType(UserIsSuperUserClaimType));
    
    public static bool IsAdmin(this ClaimsPrincipal claimsPrincipal) =>
        string.Equals("true", claimsPrincipal.ClaimOfType(UserIsAdminClaimType));
    
    public static bool IsMember(this ClaimsPrincipal claimsPrincipal) =>
        string.Equals("true", claimsPrincipal.ClaimOfType(UserIsMemberClaimType));

    public static string DisplayName(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal.ClaimOfType(DisplayNameClaimType) ?? "";

    public static string ProfilePictureHref(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal.ClaimOfType(ProfileImageUrlClaimType) ?? "";
}
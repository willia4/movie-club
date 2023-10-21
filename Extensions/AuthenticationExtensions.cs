using System.Security.Claims;

namespace zinfandel_movie_club;

public static class AuthenticationExtensions
{
    public static string? NameIdentifier(this ClaimsPrincipal claimsPrincipal)
    {
        var claims = claimsPrincipal switch
        {
            ClaimsPrincipal => claimsPrincipal.Claims ?? Enumerable.Empty<Claim>(),
            _ => Enumerable.Empty<Claim>()
        };

        return claims
            .Where(c => c.Type == ClaimTypes.NameIdentifier)
            .Select(c => c.Value)
            .FirstOrDefault();
    }
}
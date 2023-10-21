using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public class AdminAuthorizationRequirement : IAuthorizationRequirement
{
    
}

public class AdminAuthorizationPolicy : AuthorizationHandler<AdminAuthorizationRequirement>
{
    private readonly IUserManager _userManager;
    private readonly ImmutableList<string> _superUserIds;
    
    public AdminAuthorizationPolicy(IUserManager userManager, IOptions<AppSettings> appSettings)
    {
        _userManager = userManager;
        _superUserIds = appSettings.Value.SuperUserIds.ToImmutableList();
    }

    private bool UserIsSuperUser(ClaimsPrincipal? user)
    {
        return user?.NameIdentifier() switch
        {
            string id => _superUserIds.Contains(id),
            _ => false
        };
    }
    
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAuthorizationRequirement requirement)
    {
        var user = context.User;

        if (UserIsSuperUser(user))
        {
            context.Succeed(requirement);
            return;
        }

        if (user.NameIdentifier() is string userId)
        {
            var userRole = await TimeSpan.FromSeconds(10).WithTimeoutAsync(
                async token => await _userManager.GetRoleForUser(userId, cancellationToken: token));

            if (string.Equals("Admin", userRole, StringComparison.InvariantCultureIgnoreCase))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
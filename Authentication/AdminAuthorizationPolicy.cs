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
    public AdminAuthorizationPolicy()
    {
        
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAuthorizationRequirement requirement)
    {
        if (context.User.IsAdmin())
        {
            context.Succeed(requirement);
        }
    }
}
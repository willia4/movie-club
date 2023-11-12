using System.Collections.Immutable;
using Microsoft.AspNetCore.Authorization;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public class MemberAuthorizationRequirement : IAuthorizationRequirement
{
    
}

public class MemberAuthorizationPolicy : AuthorizationHandler<MemberAuthorizationRequirement>
{
    public MemberAuthorizationPolicy()
    {

    }
    
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MemberAuthorizationRequirement requirement)
    {
        if (context.User.IsMember())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
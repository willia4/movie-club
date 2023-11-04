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
    
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MemberAuthorizationRequirement requirement)
    {
        if (context.User.IsMember())
        {
            context.Succeed(requirement);
        }
    }
}
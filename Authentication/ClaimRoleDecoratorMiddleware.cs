using System.Security.Claims;
using System.Security.Principal;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public class ClaimRoleDecoratorMiddleware : IMiddleware
{

    private readonly IUserManager _userManager;
    
    public ClaimRoleDecoratorMiddleware(IUserManager userManager)
    {
        _userManager = userManager;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User is ClaimsPrincipal { Identity: IIdentity { IsAuthenticated: true} } p && 
            p.NameIdentifier() is string id)
        {
            
            if (await _userManager.GetRoleForUser(id, context.RequestAborted) is string role)
            {
                var claims = new Claim[]
                {
                    new ("x-club-role", role)
                };
                context.User.AddIdentity(new ClaimsIdentity(claims));
            }
        }
        
        await next(context);
    }
}
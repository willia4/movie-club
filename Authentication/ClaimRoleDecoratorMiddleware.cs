using System.Security.Claims;
using System.Security.Principal;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public class ClaimRoleDecoratorMiddleware : IMiddleware
{
    public const string UserRoleClaimType = "x-club-role";
    public const string UserIsSuperUserClaimType = "x-is-superuser";
    public const string UserIsAdminClaimType = "x-is-admin";
    public const string UserIsMemberClaimType = "x-is-member";
    public const string DisplayNameClaimType = "x-display-name";
    
    private readonly IGraphUserManager _userManager;
    private readonly IUserRoleDecorator _roleDecorator;
    
    public ClaimRoleDecoratorMiddleware(IGraphUserManager userManager, IUserRoleDecorator roleDecorator)
    {
        _userManager = userManager;
        _roleDecorator = roleDecorator;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User is ClaimsPrincipal { Identity: IIdentity { IsAuthenticated: true} } p && 
            p.NameIdentifier() is { } id)
        {
            var graphUser = await TimeSpan.FromSeconds(10).WithTimeoutAsync(async token =>
                await _userManager.GetGraphUserAsync(id, token));

            var newClaims = Enumerable.Empty<Claim>();
            if (graphUser != null)
            {
                newClaims = newClaims.AppendRange(_roleDecorator.RoleClaims(graphUser));
            }

            newClaims = newClaims.Append(new Claim(DisplayNameClaimType, graphUser?.DisplayName ?? ""));

            context.User.AddIdentity(new ClaimsIdentity(newClaims));
        }
        
        await next(context);
    }
}
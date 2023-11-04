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
    private readonly ISuperUserIdentifier _superUserIdentifier;
    
    public ClaimRoleDecoratorMiddleware(IGraphUserManager userManager, ISuperUserIdentifier superUserIdentifier)
    {
        _userManager = userManager;
        _superUserIdentifier = superUserIdentifier;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User is ClaimsPrincipal { Identity: IIdentity { IsAuthenticated: true} } p && 
            p.NameIdentifier() is string id)
        {
            var isSuperUser = _superUserIdentifier.UserIsSuperuser(id);

            var graphUser = await TimeSpan.FromSeconds(10).WithTimeoutAsync(async token =>
                await _userManager.GetGraphUserAsync(id, token));
            
            var userRole =
                isSuperUser 
                    ? AuthenticationExtensions.AdminRole
                    : graphUser?.UserRole;
            
            var newClaims = new List<Claim>();
            if (userRole != null)
            {
                newClaims.Add(new Claim(UserRoleClaimType, userRole));
            }
            
            var isAdmin = string.Equals(AuthenticationExtensions.AdminRole, userRole, StringComparison.InvariantCultureIgnoreCase);
            var isMember = isAdmin || string.Equals(AuthenticationExtensions.MemberRole, userRole, StringComparison.InvariantCultureIgnoreCase);
            
            newClaims.Add(new Claim(UserIsSuperUserClaimType, isSuperUser ? "true" : "false"));
            newClaims.Add(new Claim(UserIsAdminClaimType, isAdmin ? "true" : "false"));
            newClaims.Add(new Claim(UserIsMemberClaimType, isMember ? "true" : "false"));

            newClaims.Add(new Claim(DisplayNameClaimType, graphUser?.DisplayName ?? ""));

            context.User.AddIdentity(new ClaimsIdentity(newClaims));
        }
        
        await next(context);
    }
}
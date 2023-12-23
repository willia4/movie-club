using System.Security.Claims;
using System.Security.Principal;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Authentication;

public class ClaimRoleDecoratorMiddleware : IMiddleware
{
    public const string DisplayNameClaimType = "x-display-name";
    public const string ProfileImageUrlClaimType = "x-profile-image";
    
    private readonly IGraphUserManager _userManager;
    private readonly IUserRoleDecorator _roleDecorator;
    private readonly IImageUrlProvider<IGraphUser> _profileImageProvider;
    
    public ClaimRoleDecoratorMiddleware(IGraphUserManager userManager, IUserRoleDecorator roleDecorator, IImageUrlProvider<IGraphUser> profileImageProvider)
    {
        _userManager = userManager;
        _roleDecorator = roleDecorator;
        _profileImageProvider = profileImageProvider;
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
                newClaims = newClaims
                    .AppendRange(_roleDecorator.RoleClaims(context.Request, graphUser))
                    .Append(new Claim(ProfileImageUrlClaimType, _profileImageProvider.ImageUri(graphUser, ImageSize.Size256).ToString()));
            }

            newClaims = newClaims
                .Append(new Claim(DisplayNameClaimType, graphUser?.DisplayName ?? ""));

            context.User.AddIdentity(new ClaimsIdentity(newClaims));
        }
        
        await next(context);
    }
}
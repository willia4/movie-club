using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Pages.Admin.Users;

public class Index : PageModel
{
    private readonly IGraphUserManager _userManager;
    private readonly AppSettings _appSettings;
    public Index(IGraphUserManager userManager, IOptions<AppSettings> appSettings)
    {
        _userManager = userManager;
        _appSettings = appSettings.Value;
    }

    public string CurrentUserId { get; set; } = "";
    public ImmutableList<IGraphUser> KnownUsers = ImmutableList<IGraphUser>.Empty;
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        CurrentUserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "";

        KnownUsers = await _userManager.GetGraphUsersAsync(cancellationToken).ToImmutableList(cancellationToken);
        // var users = await _userManager.GetUsersWithoutProfilesAsync(cancellationToken);
        // var superUser = users.FirstOrDefault(u => _appSettings.SuperUserIds.Contains(u.NameIdentifier));
        // if (superUser != null)
        // {
        //     await _userManager.AddOrUpdateProfileForGraphUser(superUser, "Admin", cancellationToken);
        // }
        // var users = await _userManager.GetUsersAsync(cancellationToken).ToEnumerable(cancellationToken);
        // var me = users.First(u => string.Equals(u.NameIdentifier, myId, StringComparison.InvariantCultureIgnoreCase));
        // await _userManager.SetRole(me.NameIdentifier, "Foo", cancellationToken);
        //await _userManager.RemoveRole(me.NameIdentifier, cancellationToken);
    }

    public async Task<IActionResult> OnPost([FromForm(Name = "userId")] string userId, [FromForm(Name="roleAction")] string roleAction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new BadRequestResult();
        
        var user = await _userManager.GetGraphUserAsync(userId, cancellationToken);
        if (user == null) return new NotFoundResult();

        var newRole = roleAction switch
        {
            string s when s == "set" => "foo",
            string s when s == "clear" => null,
            _ => throw new Exceptions.BadRequestParameterException(nameof(roleAction), $"Invalid role action: {roleAction}")
        };
        
        await _userManager.SetUserRole(user, newRole, cancellationToken);
        return new RedirectToPageResult("/Admin/Users");
    }
}
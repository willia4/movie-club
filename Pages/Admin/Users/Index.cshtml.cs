using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Pages.Admin.Users;

public class Index : PageModel
{
    private readonly IUserManager _userManager;
    private readonly AppSettings _appSettings;
    public Index(IUserManager userManager, IOptions<AppSettings> appSettings)
    {
        _userManager = userManager;
        _appSettings = appSettings.Value;
    }
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        var myId = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

        var users = await _userManager.GetUsersWithoutProfilesAsync(cancellationToken);
        var superUser = users.FirstOrDefault(u => _appSettings.SuperUserIds.Contains(u.NameIdentifier));
        if (superUser != null)
        {
            await _userManager.AddOrUpdateProfileForGraphUser(superUser, "Admin", cancellationToken);
        }
        // var users = await _userManager.GetUsersAsync(cancellationToken).ToEnumerable(cancellationToken);
        // var me = users.First(u => string.Equals(u.NameIdentifier, myId, StringComparison.InvariantCultureIgnoreCase));
        // await _userManager.SetRole(me.NameIdentifier, "Foo", cancellationToken);
        //await _userManager.RemoveRole(me.NameIdentifier, cancellationToken);
    }
}
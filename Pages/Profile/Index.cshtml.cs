using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Pages.Profile;

public class Index : PageModel
{
    private readonly IGraphUserManager _graphUserManager;
    
    public bool CanEdit = false;
    public string MemberRole = "";
    public string DisplayName = "";

    public Index(IGraphUserManager graphUserManager)
    {
        _graphUserManager = graphUserManager;
    }
    
    private bool UserCanEditPage(string pageId)
    {
        // an admin can do anything; if the page is for our own user, we're allowed to edit
        return User.IsAdmin() 
                || string.Equals(User.NameIdentifier(), pageId, StringComparison.InvariantCultureIgnoreCase);
    }

    private string FixupPageId(string id) =>
        id switch
        {
            null => throw new BadHttpRequestException("Invalid route"),
            string when string.IsNullOrWhiteSpace(id) => throw new BadHttpRequestException("Invalid route"),
            "_self" => User.NameIdentifier(),
            string => id.Trim()
        };
    
    public void OnGet(string id)
    {
        ViewData["Title"] = "User Profile";

        id = FixupPageId(id);
        CanEdit = UserCanEditPage(id);
        
        MemberRole = User.UserRole();
        DisplayName = User.DisplayName();
    }

    public async Task<IActionResult> OnPost(string id, [FromForm(Name = "displayName")] string displayName, CancellationToken cancellationToken)
    {
        id = FixupPageId(id);
        if (!UserCanEditPage(id)) return new UnauthorizedResult();

        displayName = displayName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(displayName)) return new BadRequestResult();

        if (displayName != User.DisplayName())
        {
            var user = await _graphUserManager.GetGraphUserAsync(id, cancellationToken);
            if (user == null) return new NotFoundResult();

            await _graphUserManager.SetUserDisplayName(user, displayName, cancellationToken);
        }
        
        return new RedirectToPageResult($"/Profile/{id}");
    }
}
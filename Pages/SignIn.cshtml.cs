using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace zinfandel_movie_club.Pages;

public class SignIn : PageModel
{
    public IActionResult OnGet()
    {
        return new RedirectToPageResult($"/Profile/Index", new { id = "" });
    }
}
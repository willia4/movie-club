using Microsoft.AspNetCore.Mvc.RazorPages;

namespace zinfandel_movie_club.Pages.Admin.Users;

public class User : PageModel
{
    public string Username { get; private set; } = "";
    public void OnGet(string username)
    {
        Username = username;
    }
}
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace zinfandel_movie_club.Pages.Movies;

public class Add : PageModel
{
    [BindProperty] [Required] public string Title { get; set; } = "";
    [BindProperty] public string Overview { get; set; } = "";
    [BindProperty(Name="rt-critic")] [Range(0.0, 10.0)] public decimal RottenTomatoesCriticScore { get; set; }
    [BindProperty(Name="rt-user")] [Range(0.0, 10.0)] public decimal RottenTomatoesUserScore { get; set; }
    [BindProperty(Name="runtime")] public int RuntimeMinutes { get; set; }

    [BindProperty(Name="tmdb-id")] public string TmdbId { get; set; } = "";
    [BindProperty(Name="tmdb-poster")] public string TmdbPoster { get; set; } = "";
    
    public void OnGet()
    {
        
    }

    public void OnPost()
    {
        
    }
}
using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages.Movies;

public class Index : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    private readonly IMovieRatingsManager _ratingsManager;

    public Index(ICosmosDocumentManager<MovieDocument> dataManager, IMovieRatingsManager ratingsManager)
    {
        _dataManager = dataManager;
        _ratingsManager = ratingsManager;
    }

    public ImmutableList<MovieDocument> UnwatchedMovies = ImmutableList<MovieDocument>.Empty;
    public ImmutableList<MovieDocument> WatchedMovies = ImmutableList<MovieDocument>.Empty;
    
    public ImmutableDictionary<string, string> OurRatingForMovies = ImmutableDictionary<string, string>.Empty;
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        var moviesTask = _dataManager.QueryDocuments(cancellationToken: cancellationToken);
        var ratingsTask = _ratingsManager.GetAllRatings(HttpContext, cancellationToken);

        await Task.WhenAll(new Task[] { moviesTask, ratingsTask });
            
        var movies = (await moviesTask).ValueOrThrow();
        var allRatings = (await ratingsTask).GroupBy(r => r.MovieId).ToImmutableDictionary(g => g.Key, g => g.ToImmutableList());

        OurRatingForMovies =
            movies
                .ToImmutableDictionary(
                    m => m.id!,
                    m => allRatings.GetValueOrDefault(m.id!, ImmutableList<MovieRating>.Empty).AverageRating().Second());

        UnwatchedMovies = 
            movies
                .Where(m => m.WatchedDates.Count == 0)
                .OrderBy(m => m.DateAdded)
                .ThenBy(m => m.Title)
                .ToImmutableList();
        
        WatchedMovies = 
            movies
                .Where(m => m.WatchedDates.Count > 0)
                .OrderBy(m => m.DateAdded)
                .ThenBy(m => m.Title)
                .ToImmutableList();
    }
}
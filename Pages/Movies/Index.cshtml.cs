using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages.Movies;

public class Index : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    private readonly IMovieRatingsManager _ratingsManager;
    private readonly IGraphUserManager _userManager;
    public Index(ICosmosDocumentManager<MovieDocument> dataManager, IMovieRatingsManager ratingsManager, IGraphUserManager userManager)
    {
        _dataManager = dataManager;
        _ratingsManager = ratingsManager;
        _userManager = userManager;
    }

    public ImmutableList<MovieListMoviePartialModel> UnwatchedMovies = ImmutableList<MovieListMoviePartialModel>.Empty;
    public ImmutableList<MovieListMoviePartialModel> WatchedMovies = ImmutableList<MovieListMoviePartialModel>.Empty;
    
    public ImmutableDictionary<string, string> OurRatingForMovies = ImmutableDictionary<string, string>.Empty;
    public ImmutableDictionary<string, string> MyRatingsForMovies = ImmutableDictionary<string, string>.Empty;
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        var currentUserId = User.NameIdentifier();
        var moviesTask = _dataManager.QueryDocuments(cancellationToken: cancellationToken);
        var membersTask = _userManager.GetMembersAsync(HttpContext, cancellationToken);
        
        await Task.WhenAll(new Task[] { moviesTask, membersTask });
            
        var movies = (await moviesTask).ValueOrThrow();
        var members = await membersTask;
        
        var allRatings = await _ratingsManager.GetAllRatings(HttpContext, movies, cancellationToken);

        var ratingsForMovies =
            allRatings
                .GroupBy(r => r.MovieId)
                .ToImmutableDictionary(g => g.Key, g => g.ToImmutableList());

        OurRatingForMovies =
            ratingsForMovies
                .ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.AverageRating().Second());


        MyRatingsForMovies =
            ratingsForMovies
                .ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp =>
                    {
                        var rating = kvp.Value.FirstOrDefault(r => r.UserId == currentUserId);
                        return rating is not { Rating: not null } ? "Not Yet" : rating.Rating.Value.ToString("N2");
                    });
            
        var unwatchedMovies = 
            movies
                .Where(m => m.WatchedDates.Count == 0)
                .OrderBy(m => m.DateAdded)
                .ThenBy(m => m.Title)
                .ToImmutableList();
        UnwatchedMovies = MovieListMoviePartialModel.MakeModelsForMovies(unwatchedMovies, allRatings, cancellationToken);
        
        var watchedMovies = 
            movies
                .Where(m => m.WatchedDates.Count > 0)
                .OrderByDescending(m => m.MostRecentWatchedDate)
                .ThenBy(m => m.DateAdded)
                .ThenBy(m => m.Title)
                .ToImmutableList();
        WatchedMovies = MovieListMoviePartialModel.MakeModelsForMovies(watchedMovies, allRatings, cancellationToken);
    }
}
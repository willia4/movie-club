using System.Collections.Immutable;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages;

public record MovieListMoviePartialModel(MovieDocument Movie, IEnumerable<MovieRating> Ratings)
{
    public string Title => Movie.Title;
    public bool Watched => Movie.MostRecentWatchedDate.HasValue;
    
    public string CurrentUserRating
    {
        get
        {
            var myRating = Ratings.FirstOrDefault(r => r.IsCurrentUser);
            return myRating == null || !myRating.Rating.HasValue
                ? "Not Yet"
                : myRating.Rating.Value.ToString("N2");
        }
    }
    
    public static async Task<ImmutableList<MovieListMoviePartialModel>> MakeModelsForMovies(IMovieRatingsManager ratingsManager, HttpContext httpContext, IEnumerable<MovieDocument> movies, CancellationToken cancellationToken)
    {
        var results = ImmutableList<MovieListMoviePartialModel>.Empty;
        foreach (var movie in movies)
        {
            var ratings = await ratingsManager.GetRatingsForMovie(httpContext, movie, cancellationToken);
            results = results.Add(new MovieListMoviePartialModel(movie, ratings));
        }

        return results;
    }
}
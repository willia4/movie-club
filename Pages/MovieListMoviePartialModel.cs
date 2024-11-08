using System.Collections.Immutable;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages;

public record MovieListMoviePartialModel(MovieDocument Movie, IEnumerable<MovieRating> Ratings)
{
    public string Title => Movie.Title;
    public bool Watched => Movie.MostRecentWatchedDate.HasValue;
    public DateTimeOffset DateAdded => Movie.DateAdded ?? DateTimeOffset.MinValue;

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
    
    public static ImmutableList<MovieListMoviePartialModel> MakeModelsForMovies(IEnumerable<MovieDocument> movies, IEnumerable<MovieRating> ratingsForMovies, CancellationToken cancellationToken)
    {
        var ratingsTable = ratingsForMovies
            .GroupBy(r => r.MovieId)
            .ToImmutableDictionary(x => x.Key, x => x.ToImmutableList());

        ImmutableList<MovieRating> GetRatingForMovie(MovieDocument? movie)
        {
            if (movie?.id is {} movieId && ratingsTable.TryGetValue(movieId, out var value))
            {
                return value;
            }
            return ImmutableList<MovieRating>.Empty;
        }

        MovieListMoviePartialModel MakeModel(MovieDocument movie) =>
            new MovieListMoviePartialModel(movie, GetRatingForMovie(movie));
        
        return
            movies
                .Select(MakeModel)
                .ToImmutableList();
    }
}
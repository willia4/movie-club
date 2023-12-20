using zinfandel_movie_club.Data.Models;
using System.Collections.Immutable;
using System.Security.Claims;

namespace zinfandel_movie_club.Data;

public interface IMovieRatingsManager
{
    public Task<ImmutableList<MovieRating>> GetRatingsForMovie(ClaimsPrincipal? currentUser, MovieDocument movie, CancellationToken cancellationToken);
}

public class MovieRatingsManager : IMovieRatingsManager
{
    private readonly IGraphUserManager _users;
    
    public MovieRatingsManager(IGraphUserManager users)
    {
        _users = users;
    }

    public async Task<ImmutableList<MovieRating>> GetRatingsForMovie(ClaimsPrincipal? currentUser, MovieDocument movie, CancellationToken cancellationToken)
    {
        var currentUserId = currentUser?.NameIdentifier();
        var movieId = movie.id ?? throw new ArgumentException($"MovieDocument with null id passed to {nameof(GetRatingsForMovie)}");
        
        var allMembers = (await _users.GetMembersAsync(cancellationToken)).ToImmutableList();

        var ratings =
            allMembers.Join(movie.UserRatings, r => r.NameIdentifier, m => m.Key, (m, r) =>
            {
                var (_, rating) = r;
                return new RatedMovieRating(
                    IsCurrentUser: currentUserId == m.NameIdentifier,
                    UserId: m.NameIdentifier,
                    UserName: m.DisplayName,
                    MovieId: movieId,
                    MovieTitle: movie.Title,
                    MovieRating: rating);
            }).ToImmutableList();

        var ratedUserIds = new HashSet<string>(ratings.Select(r => r.UserId));

        var unratedUsers =
            allMembers
                .Where(m => !ratedUserIds.Contains(m.NameIdentifier))
                .Select(m => new UnratedMovieRating(
                    IsCurrentUser: currentUserId == m.NameIdentifier,
                    UserId: m.NameIdentifier,
                    UserName: m.DisplayName,
                    MovieId: movieId,
                    MovieTitle: movie.Title))
                .ToImmutableList();

        return ((IEnumerable<MovieRating>) ratings).Concat(unratedUsers).ToImmutableList();
    }
}

public abstract record MovieRating(bool IsCurrentUser, string UserId, string UserName, string MovieId, string MovieTitle)
{
    public abstract decimal? Rating { get; }
    public abstract bool IsRated { get; }
}

public record UnratedMovieRating(bool IsCurrentUser, string UserId, string UserName, string MovieId, string MovieTitle) : MovieRating(IsCurrentUser, UserId, UserName, MovieId, MovieTitle)
{
    public override decimal? Rating => null;

    public override bool IsRated => false;
}

public record RatedMovieRating(bool IsCurrentUser, string UserId, string UserName, string MovieId, string MovieTitle, decimal MovieRating) : MovieRating(IsCurrentUser, UserId, UserName, MovieId, MovieTitle)
{
    public override decimal? Rating => MovieRating;
    public override bool IsRated => true;
}

public static class MovieRatingExtensions
{
    public static decimal? AverageRating(this IEnumerable<MovieRating> ratings)
    {
        var frozen  = ratings.ToImmutableList();
        
        if (frozen.Count <= 1) return frozen.Select(f => f.Rating).FirstOrDefault();
        var rated = frozen.Where(f => f.IsRated).ToImmutableList();
        if (rated.Count >= 2)
        {
            return rated.Select(r => r.Rating ?? 0).Average();
        }

        return null;
    }
}
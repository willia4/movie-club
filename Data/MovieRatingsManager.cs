using zinfandel_movie_club.Data.Models;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Data;

public interface IMovieRatingsManager
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="allMovies">Optional. If specified, ensure that unrated ratings exist for all movies cin this query in addition to all known ratings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ImmutableList<MovieRating>> GetAllRatings(HttpContext context, IEnumerable<MovieDocument>? allMovies, CancellationToken cancellationToken);
    public Task<ImmutableList<MovieRating>> GetRatingsForMovie(HttpContext context, MovieDocument movie, CancellationToken cancellationToken);
    public Task<ImmutableList<MovieRating>> GetRatingsForUser(HttpContext context, IGraphUser user, CancellationToken cancellationToken);
    public Task<MovieRating> UpdateRatingForMovie(HttpContext context, IGraphUser user, MovieDocument movie, decimal? newRating, CancellationToken cancellationToken);
    public Task DeleteRatingsForMovie(MovieDocument movie, CancellationToken cancellationToken);
}

public class MovieRatingsManager : IMovieRatingsManager
{
    private readonly IGraphUserManager _users;
    private readonly UserRatingIdGenerator _idGenerator;
    private readonly ICosmosDocumentManager<UserRatingDocument> _ratingsDocumentManager;
    
    public MovieRatingsManager(IGraphUserManager users, ICosmosDocumentManager<UserRatingDocument> ratingsDocumentManager, UserRatingIdGenerator idGenerator)
    {
        _users = users;
        _ratingsDocumentManager = ratingsDocumentManager;
        _idGenerator = idGenerator;
    }

    private MovieRating ToRating(string currentUserId, IGraphUser userForRating, UserRatingDocument doc) => 
        new RatedMovieRating(
            IsCurrentUser: currentUserId == doc.UserId,
            User: userForRating,
            MovieId: doc.MovieId,
            MovieRating: doc.Rating);
    
    private ImmutableList<MovieRating> CreateMovieRatings(string currentUserId, string movieId, ImmutableList<IGraphUser> allMembers, ImmutableList<UserRatingDocument> ratingDocs)
    {
        var existingRatings =
            ratingDocs
                .Join(allMembers, (r => r.UserId), (m => m.NameIdentifier), (r, m) => (r, m))
                .Select(t => ToRating(currentUserId, t.Second(), t.First()))
                .ToImmutableList();
        var ratedUserIds = existingRatings.Select(r => r.UserId).ToImmutableHashSet();

        var unratedUsers =
            allMembers
                .Where(m => !ratedUserIds.Contains(m.NameIdentifier))
                .Select(m => new UnratedMovieRating(
                    IsCurrentUser: currentUserId == m.NameIdentifier,
                    User: m,
                    MovieId: movieId))
                .ToImmutableList();
        
        return ((IEnumerable<MovieRating>) existingRatings).Concat(unratedUsers).OrderBy(r => r.UserName).ToImmutableList();
    }
    
    public async Task<ImmutableList<MovieRating>> GetAllRatings(HttpContext context, IEnumerable<MovieDocument>? allMovies, CancellationToken cancellationToken)
    {
        var currentUserId = context.User?.NameIdentifier() ?? "";
        
        var allMembersTask = _users.GetMembersAsync(context, cancellationToken);
        var allRatingsTask = _ratingsDocumentManager.GetAllDocuments(cancellationToken);
        
        await Task.WhenAll(new Task[] { allMembersTask, allRatingsTask });
        var allMembers = (await allMembersTask).ToImmutableList();
        var allRatings = (await allRatingsTask).ValueOrThrow();

        var results = ImmutableList<MovieRating>.Empty;

        var allMovieIds = (allMovies ?? Enumerable.Empty<MovieDocument>()).Where(m => !string.IsNullOrWhiteSpace(m.id)).Select(m => m.id!);
        
        var movieIds = 
            allRatings
                .Select(r => r.MovieId)
                .Concat(allMovieIds)
                .Distinct()
                .ToImmutableList();

        foreach (var movieId in movieIds)
        {
            var movieRatings = allRatings.Where(r => r.MovieId == movieId).ToImmutableList();
            results = results.AddRange(CreateMovieRatings(currentUserId, movieId, allMembers, movieRatings));
        }

        return results;
    }
    
    public async Task<ImmutableList<MovieRating>> GetRatingsForMovie(HttpContext context, MovieDocument movie, CancellationToken cancellationToken)
    {
         var currentUserId = context.User?.NameIdentifier() ?? "";
         var movieId = movie.id ?? throw new ArgumentException($"MovieDocument with null id passed to {nameof(GetRatingsForMovie)}");
         
        var allMembersTask = _users.GetMembersAsync(context, cancellationToken);
        var allRatingsTask = _ratingsDocumentManager.QueryDocuments(q => q.Where(d => d.MovieId == movieId), cancellationToken);

        await Task.WhenAll(new Task[] { allMembersTask, allRatingsTask });
        var allMembers = (await allMembersTask).ToImmutableList();
        var allRatingsDocuments = (await allRatingsTask).ValueOrThrow();

        return CreateMovieRatings(currentUserId, movieId, allMembers, allRatingsDocuments);
    }

    public async Task<ImmutableList<MovieRating>> GetRatingsForUser(HttpContext context, IGraphUser user, CancellationToken cancellationToken)
    {
        var currentUserId = context.User?.NameIdentifier() ?? "";
        var ratingsDocuments = (await _ratingsDocumentManager.QueryDocuments(q => q.Where(r => r.UserId == user.NameIdentifier), cancellationToken)).ValueOrThrow();

        return ratingsDocuments.Select(r => ToRating(currentUserId, user, r)).ToImmutableList();
    }
    
    public async Task<MovieRating> UpdateRatingForMovie(HttpContext context, IGraphUser user, MovieDocument movie, decimal? newRating, CancellationToken cancellationToken)
    {
        var currentUserId = context.User?.NameIdentifier();
        if (movie.id == null) throw new InvalidOperationException("Bad movie id");
        
        var existingRating = 
            (await _ratingsDocumentManager.QueryDocuments(q => q.Where(r => r.UserId == user.NameIdentifier && r.MovieId == movie.id), cancellationToken))
            .Map(docs => docs.FirstOrDefault())
            .Match(doc => doc, ex => ex is NotFoundException ? null : throw ex);

        // we don't want there to be a rating and there isn't one; we are done
        if (existingRating == null && !newRating.HasValue)
        {
            return new UnratedMovieRating(
                IsCurrentUser: currentUserId == user.NameIdentifier,
                User: user,
                MovieId: movie.id);
        }

        // we don't want there to be a rating, but there is one; so we need to delete it
        if (existingRating != null && !newRating.HasValue)
        {
            (await _ratingsDocumentManager.DeleteDocument(existingRating.id!, cancellationToken)).ThrowIfError();
            return new UnratedMovieRating(
                IsCurrentUser: currentUserId == user.NameIdentifier,
                User: user,
                MovieId: movie.id ?? "");
        }

        // should never get here with a null new rating, so assume it going forward
        System.Diagnostics.Debug.Assert(newRating.HasValue);
        
        if (existingRating == null)
        {
            existingRating = new UserRatingDocument()
            {
                id = _idGenerator.NewId(),
                MovieId = movie.id,
                Rating = newRating.Value,
                UserId = user.NameIdentifier
            };
        }
        else
        {
            existingRating.Rating = newRating.Value;
        }

        var updateRes =
            (await _ratingsDocumentManager.UpsertDocument(existingRating, cancellationToken)).ValueOrThrow();

        return new RatedMovieRating(
            IsCurrentUser: currentUserId == user.NameIdentifier,
            User: user,
            MovieId: movie.id ?? "",
            MovieRating: updateRes.Rating);
    }

    public async Task DeleteRatingsForMovie(MovieDocument movie, CancellationToken cancellationToken)
    {
        var movieId = movie.id ?? throw new ArgumentException($"MovieDocument with null id passed to {nameof(DeleteRatingsForMovie)}");
        var movieRatings = 
            (await _ratingsDocumentManager
                    .QueryDocuments(q => 
                        q.Where(d => d.MovieId == movieId), cancellationToken))
            .ValueOrThrow();

        const int taskCount = 4;
        var taskBuckets = movieRatings.ToBuckets(taskCount);
        var tasks = taskBuckets.Select(async bucket =>
        {
            if (bucket.IsEmpty)
                return;

            foreach (var doc in bucket)
            {
                if (!string.IsNullOrWhiteSpace(doc.id))
                    await _ratingsDocumentManager.DeleteDocument(doc.id, cancellationToken);
            }
        }).ToImmutableList();

        await Task.WhenAll(tasks);
    }
}


public abstract record MovieRating(bool IsCurrentUser, IGraphUser User, string MovieId)
{
    public abstract decimal? Rating { get; }
    public abstract bool IsRated { get; }
    public string UserId => User.NameIdentifier;
    public string UserName => User.DisplayName;
}

public record UnratedMovieRating(bool IsCurrentUser, IGraphUser User, string MovieId) : MovieRating(IsCurrentUser, User, MovieId)
{
    public override decimal? Rating => null;

    public override bool IsRated => false;
}

public record RatedMovieRating(bool IsCurrentUser, IGraphUser User, string MovieId, decimal MovieRating) : MovieRating(IsCurrentUser, User, MovieId)
{
    public override decimal? Rating => MovieRating;
    public override bool IsRated => true;
}

public static class MovieRatingExtensions
{
    public static (decimal?, string) AverageRating(this IEnumerable<MovieRating> ratings)
    {
        static (decimal?, string) ret(decimal? v) => (v, v switch
        {
            null => "Not Yet",
            _ => v.Value.ToString("N2")
        });
        
        var frozen  = ratings.ToImmutableList();
        
        if (frozen.Count <= 1) return ret(frozen.Select(f => f.Rating).FirstOrDefault());
        var rated = frozen.Where(f => f.IsRated).ToImmutableList();
        
        return rated.Count >= 2 
            ? ret(rated.Select(r => r.Rating ?? 0).Average()) 
            : ret(null);
    }
}
using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Pages.Movies.View;

public class Index : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    
    private readonly IImageUrlProvider<MovieDocument> _coverImageProvider;
    private readonly IMovieDatabase _tmdb;
    private readonly IMovieRatingsManager _ratings;
    public Index(ICosmosDocumentManager<MovieDocument> dataManager, IMovieRatingsManager ratings, IImageUrlProvider<MovieDocument> coverImageProvider, IMovieDatabase tmdb)
    {
        _dataManager = dataManager;
        _ratings = ratings;
        _coverImageProvider = coverImageProvider;
        _tmdb = tmdb;
    }

    public bool UserIsAdmin = false;
    
    public string Id = "";
    public string CoverImageHref = "";
    
    public string MovieTitle = "";
    public string Overview = "";
    
    public DateOnly? WatchedDate = null;
    public decimal? RTCriticScore = null;
    public decimal? RTUserScore = null;
    public decimal? TmdbScore = null;
    public int? RuntimeMinutes = null;
    public string? ReleaseDate = null;

    public string TmdbId = "";

    public string TmdbWatchPageHref = "";
    public ImmutableList<(Uri, string)> WatchProviders = ImmutableList<(Uri, string)>.Empty;
    
    public ImmutableList<MovieRating> Ratings = ImmutableList<MovieRating>.Empty;
    public decimal? OurRating = null;
    public string OurRatingFormatted = "Not Yet";
    
    public async Task OnGet(string id, CancellationToken cancellationToken)
    {
        UserIsAdmin = User.IsAdmin();
        Id = MovieDocument.IdFromSlugId(id);
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new BadRequestException("id cannot be empty");
        }
        
        var doc =
                (await _dataManager.GetDocumentById(Id, cancellationToken))
                .ValueOrThrow();

        Ratings = await _ratings.GetRatingsForMovie(HttpContext, doc, cancellationToken);

        (OurRating, OurRatingFormatted) = Ratings.AverageRating(); 
 
        MovieTitle = doc.Title;
        Overview = doc.Overview ?? "";
        
        WatchedDate = doc.MostRecentWatchedDate;
        RTCriticScore = doc.RottenTomatoesCriticScore;
        RTUserScore = doc.RottenTomatoesUserScore;
        RuntimeMinutes = doc.RuntimeMinutes;
        ReleaseDate = doc.ReleaseDate;
        TmdbId = doc.TmdbId ?? "";

        if (int.TryParse(TmdbId, out var tmdbId))
        {
            var watchProvidersResult = await _tmdb.GetWatchProviders(tmdbId, cancellationToken);
            if (watchProvidersResult.Providers.Count > 0)
            {
                WatchProviders = ImmutableList<(Uri, string)>.Empty.AddRange(
                    watchProvidersResult.Providers.Select(p => (p.Logo, p.Name)));
                TmdbWatchPageHref = watchProvidersResult.TmdbWatchPage?.ToString() ?? "";
            }

            var details = await _tmdb.GetDetails(tmdbId, cancellationToken);
            if ((details?.Rating ?? 0.0M) != 0.0M)
            {
                TmdbScore = Math.Round(details!.Rating, 2);
            }
        }
        CoverImageHref = _coverImageProvider.ImageUri(doc, ImageSize.Size512).ToString();
    }
}
using System.Collections.Immutable;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.VisualBasic;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages.Movies;

public record DataRow(MovieDocument Movie, ImmutableDictionary<string, string> RatingsByUser, string OurAverageRating)
{
    public string MovieId => Movie.id!;
    public string MovieTitle => Movie.Title;
    public string? MovieTmdbId => Movie.TmdbId;
    public bool MovieIsWatched => Movie.MostRecentWatchedDate.HasValue;
    public string Slug => Movie.SlugId();
    public string WatchDate => Movie.MostRecentWatchedDate?.ToString() ?? "";
    public decimal? RottenTomatoesCriticScore = Movie.RottenTomatoesCriticScore;
    public decimal? RottenTomatoesUserScore = Movie.RottenTomatoesUserScore;
    public string? TmdbId => Movie.TmdbId;
    public string? RuntimeMinutes => Movie.RuntimeMinutes.HasValue ? Movie.RuntimeMinutes.ToString() : "";
}

public class RawData : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    private readonly IMovieRatingsManager _ratingsManager;
        
    public RawData(ICosmosDocumentManager<MovieDocument> movieManager, IMovieRatingsManager ratingsManager)
    {
        _movieManager = movieManager;
        _ratingsManager = ratingsManager;
    }

    public ImmutableList<DataRow> DataRows = ImmutableList<DataRow>.Empty;
    public ImmutableList<(string DisplayName, string UserId)> Users = ImmutableList<(string DisplayName, string UserId)>.Empty;
    
    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        var moviesTask = _movieManager.GetAllDocuments(cancellationToken);
        var ratingsTask = _ratingsManager.GetAllRatings(HttpContext, cancellationToken);
        await Task.WhenAll(new Task[] { moviesTask, ratingsTask });

        var allMovies = (await moviesTask).ValueOrThrow();
        var allRatings = (await ratingsTask);

        var watchedMovies = allMovies.Where(m => m.MostRecentWatchedDate.HasValue);
        var unwatchedMovies = allMovies.Where(m => !m.MostRecentWatchedDate.HasValue);
        
        DataRows =
            watchedMovies.OrderBy(m => m.MostRecentWatchedDate).ThenBy(m => m.DateAdded).ThenBy(m => m.Title)
                .Concat(unwatchedMovies.OrderBy(m => m.DateAdded).ThenBy(m => m.Title))
                .Select(m =>
                {
                    var ratingsForMovie = allRatings.Where(r => r.MovieId == m.id!).ToImmutableList();

                    var ratingsByUser =
                        ratingsForMovie
                            .ToImmutableDictionary(r => r.UserId, r => r.Rating.HasValue ? r.Rating.Value.ToString("N2") : "");

                    var averageRating = ratingsForMovie.AverageRating().First();                    
                    return new DataRow(m, ratingsByUser, OurAverageRating: averageRating.HasValue ? averageRating.Value.ToString("N2") : "");
                })
                .ToImmutableList();

        Users =
            allRatings
                .Select(r => r.User)
                .DistinctBy(u => u.NameIdentifier)
                .Select(u => (DisplayName: u.DisplayName, UserId: u.NameIdentifier))
                .OrderBy(t => t.First())
                .ToImmutableList();

        var renderCsv = Request.Query.ContainsKey("csv");
        return renderCsv ? await RenderCsv(cancellationToken) : Page();
    }
    
    private async Task<IActionResult> RenderCsv(CancellationToken cancellationToken)
    {
        var sb = new System.Text.StringBuilder();
        await using var output = new StringWriter(sb);
        await using var csv = new CsvWriter(output, CultureInfo.InvariantCulture);


        var headerFields =
            (new string[] { "Title", "Watched Date" })
            .Concat(Users.Select(u => $"{u.First()} Rating"))
            .Concat(new string[] { "Average Rating", "Rotten Tomatoes (Critics)", "Rotten Tomatoes (Audience)", "Runtime (minutes)", "TMDB ID"});
        
        foreach(var h in headerFields)
            csv.WriteField(h);
        await csv.NextRecordAsync();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var row in DataRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            csv.WriteField(row.MovieTitle);
            csv.WriteField(row.WatchDate);

            foreach (var (_, userId) in Users)
            {
                csv.WriteField(row.RatingsByUser.TryGetValue(userId, out var rating) ? rating : "");
            }
            csv.WriteField(row.OurAverageRating);
            csv.WriteField(row.RottenTomatoesCriticScore);
            csv.WriteField(row.RottenTomatoesUserScore);
            csv.WriteField(row.RuntimeMinutes);
            csv.WriteField(row.TmdbId);
            await csv.NextRecordAsync();
        }
        await output.FlushAsync();

        return new ContentResult()
        {
            ContentType = "text/csv",
            Content = sb.ToString() 
        };

    }

    
}

using System.Collections.Immutable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using zinfandel_movie_club.Controllers.Models;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Controllers;

[Controller]
[Route("/api/v1/movies/{movieId}/watch-dates")]
[Authorize(Policy = "Member")]
[EnableRateLimiting(policyName: "api")]
public class MovieWatchDatesApiController : Controller
{
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;

    public MovieWatchDatesApiController(ICosmosDocumentManager<MovieDocument> movieManager)
    {
        _movieManager = movieManager;
    }

    [HttpGet]
    public async Task<WatchDatesResponse> GetWatchDates(string movieId, CancellationToken cancellationToken)
    {
        var movie =
            (await _movieManager.GetDocumentById(movieId, cancellationToken))
            .ValueOrThrow();
        
        return new WatchDatesResponse(
            MostRecentWatchDate: movie.MostRecentWatchedDate!.Value,
            AllWatchDates: movie.WatchedDates.ToImmutableList());
    }

    [HttpDelete]
    public async Task ClearWatchDates(string movieId, CancellationToken cancellationToken)
    {
        var movie =
            (await _movieManager.GetDocumentById(movieId, cancellationToken))
            .ValueOrThrow();

        movie.MostRecentWatchedDate = null;
        movie.WatchedDates = new List<DateOnly>();
        
        await _movieManager.UpsertDocument(movie, cancellationToken);
    }
    
    [HttpPost]
    public async Task<WatchDatesResponse> SetWatchDate(string movieId, [FromBody] SetWatchDateRequest req, CancellationToken cancellationToken)
    {
        if (req?.NewWatchDate == null)
        {
            throw new BadRequestException($"{nameof(req.NewWatchDate)} is required");
        }

        var movie =
            (await _movieManager.GetDocumentById(movieId, cancellationToken))
            .ValueOrThrow();

        var existingDates = (movie.WatchedDates ?? Enumerable.Empty<DateOnly>()).ToImmutableList();
        movie.WatchedDates =
            (existingDates.Contains(req.NewWatchDate.Value)
                ? existingDates
                : existingDates.Add(req.NewWatchDate.Value))
            .OrderByDescending(d => d)
            .ToList();

        movie.MostRecentWatchedDate = movie.WatchedDates.First();

        await _movieManager.UpsertDocument(movie, cancellationToken);

        return new WatchDatesResponse(
            MostRecentWatchDate: movie.MostRecentWatchedDate!.Value,
            AllWatchDates: movie.WatchedDates.ToImmutableList());
    }
}
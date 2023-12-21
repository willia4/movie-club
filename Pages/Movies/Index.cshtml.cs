using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages.Movies;

public class Index : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;

    public Index(ICosmosDocumentManager<MovieDocument> dataManager)
    {
        _dataManager = dataManager;
    }

    public ImmutableList<MovieDocument> UnwatchedMovies = ImmutableList<MovieDocument>.Empty;
    public ImmutableList<MovieDocument> WatchedMovies = ImmutableList<MovieDocument>.Empty;
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        var results = (await _dataManager.QueryDocuments(cancellationToken: cancellationToken))
            .ValueOrThrow();

        UnwatchedMovies = results
            .Where(m => m.WatchedDates.Count == 0)
            .OrderBy(m => m.DateAdded)
            .ThenBy(m => m.Title)
            .ToImmutableList();
        
        WatchedMovies = results
            .Where(m => m.WatchedDates.Count > 0)
            .OrderBy(m => m.DateAdded)
            .ThenBy(m => m.Title)
            .ToImmutableList();
    }
}
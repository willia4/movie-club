using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages;

public class Picker : PageModel
{
    private readonly ISeededRandom _random;
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    
    public Picker(ISeededRandom random, ICosmosDocumentManager<MovieDocument> movieManager)
    {
        _random = random;
        _movieManager = movieManager;
    }
    
    public ImmutableList<MovieDocument> Choices = ImmutableList<MovieDocument>.Empty;

    public async Task OnGet(CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
$"""
SELECT * FROM root r 
         WHERE r.DocumentType = "{MovieDocument._DocumentType}"
         AND ((NOT IS_DEFINED(r.MostRecentWatchedDate)) OR (IS_NULL(r.MostRecentWatchedDate)) OR (r.MostRecentWatchedDate = ""))
""");
        var movies = (await _movieManager
                .QueryDocuments(query, cancellationToken))
            .Match(results => results as IEnumerable<MovieDocument>, ex => Enumerable.Empty<MovieDocument>())
            .OrderBy(m => m.DateAdded)
            .ThenBy(m => m.Title)
            .ThenBy(m => m.id)
            .ToImmutableList();

        if (movies.Count == 0)
        {
            return;
        }
        else if (movies.Count == 1)
        {
            Choices = movies;
            return;
        }

        var lastIndex = -1;
        while (movies.Count > 0 && Choices.Count < 3)
        {
            var hash = HashCode.Combine(movies.Count, movies.First().id, movies.Last().id, lastIndex);

            var nextIndex = _random.NextInRange(hash, 0, (movies.Count - 1));
            Choices = Choices.Add(movies[nextIndex]);
            movies = movies.RemoveAt(nextIndex);
            lastIndex = nextIndex;
        }
    }
}
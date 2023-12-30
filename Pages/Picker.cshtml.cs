using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Pages;

public class Picker : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    private readonly IMovieRatingsManager _ratingsManager;
    
    public Picker(ICosmosDocumentManager<MovieDocument> movieManager, IMovieRatingsManager ratingsManager)
    {
        _movieManager = movieManager;
        _ratingsManager = ratingsManager;
    }
    
    public ImmutableList<MovieListMoviePartialModel> Choices = ImmutableList<MovieListMoviePartialModel>.Empty;
    public string CurrentUserId;
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        CurrentUserId = User.NameIdentifier() ?? throw new UnauthorizedException();
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
            //Choices = await GetRatingsForMovies(movies, cancellationToken);
            Choices = await MovieListMoviePartialModel.MakeModelsForMovies(_ratingsManager, HttpContext, movies, cancellationToken);
            return;
        }

        var seed = HashCode.Combine(movies.Count, movies.First().id, movies.Last().id);
        var random = new Random(seed);
        
        var pickedMovies = new List<MovieDocument>();
        while (movies.Count > 0 && pickedMovies.Count < 3)
        {
            var nextIndex = random.Next(0, (movies.Count - 1));

            pickedMovies.Add(movies[nextIndex]);
            movies = movies.RemoveAt(nextIndex);
        }

        Choices = await MovieListMoviePartialModel.MakeModelsForMovies(_ratingsManager, HttpContext, pickedMovies, cancellationToken);
    }
}
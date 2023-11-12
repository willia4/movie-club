using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Controllers.Models;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Controllers;

[Controller]
[Route("/api/v1/search")]
[Authorize(Policy = "Member")]
[EnableRateLimiting(policyName: "api")]
public class SearchApiController : Controller
{
    private readonly IMovieDatabase _movieDb;
    
    public SearchApiController(IMovieDatabase movieDb)
    {
        _movieDb = movieDb;
    }
    
    [HttpPost]
    public async IAsyncEnumerable<SearchMoviesResponse> SearchMoviesPost([FromBody] Models.SearchMoviesRequest req, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var res in _movieDb.Search("Blues Brothers", cancellationToken))
        {
            yield return new SearchMoviesResponse(Id: res.Id.ToString(), Title: res.Title);
        }
    }
}
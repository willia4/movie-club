using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zinfandel_movie_club.Controllers.Models;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Controllers;

[Controller]
[Route("/api/v1/movie")]
[Authorize(Policy = "Member")]
[EnableRateLimiting(policyName: "api")]
public class MovieDetailApiController : Controller
{
    private readonly IMovieDatabase _movieDb;
    
    public MovieDetailApiController(IMovieDatabase movieDb)
    {
        _movieDb = movieDb;
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<MovieDetailResponse> GetMovieDetails([FromRoute] string id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var intId))
        {
            throw new BadRequestParameterException("id", "id in route must be an int");
        }

        var details = await _movieDb.GetDetails(intId, cancellationToken);
        if (details == null)
        {
            throw new NotFoundException();
        }

        return new MovieDetailResponse(
            Id: details.Id.ToString(),
            Title: details.Title,
            Overview: details.Overview,
            PosterHref: details.PosterHref,
            BackdropHref: details.BackdropHref,
            ReleaseDate: details.ReleaseDate,
            RuntimeMinutes: details.RuntimeMinutes);
    }
}
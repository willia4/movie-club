using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zinfandel_movie_club.Controllers.Models;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Controllers;

[Controller]
[Route("/api/v1/movies/{movieId}")]
[Authorize(Policy = "Member")]
[EnableRateLimiting(policyName: "api")]
public class UserRatingsApiController : Controller
{
    private readonly IGraphUserManager _userManager;
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    private readonly IMovieRatingsManager _ratingsManager;
    
    public UserRatingsApiController(IGraphUserManager userManager, ICosmosDocumentManager<MovieDocument> movieManager, IMovieRatingsManager ratingsManager)
    {
        _userManager = userManager;
        _movieManager = movieManager;
        _ratingsManager = ratingsManager;
    }

    [HttpPost]
    [Route("users/{userId}")]
    public async Task<IActionResult> PostNewUserRating(string movieId, string userId, [FromBody] PostMovieUserRatingRequest req, CancellationToken cancellationToken)
    {
        if (req.NewRating < 0M || req.NewRating > 10M)
        {
            return BadRequest("Rating must be between 0.00 and 10.00");
        }

        var roundedRating = Math.Round(req.NewRating, 2);
        if (roundedRating != req.NewRating)
        {
            return BadRequest("Rating must have at most 2 decimal places");
        }

        var isAdmin = User.IsAdmin();
        var canEdit = isAdmin || User.NameIdentifier() == userId;

        if (!canEdit)
            return Unauthorized();

        var member = await _userManager.GetGraphUserAsync(userId, cancellationToken);
        if (member == null)
            return NotFound($"Could not find user with id {userId}");

        var movie = (await _movieManager.GetDocumentById(movieId, cancellationToken)).ValueOrThrow();

        var rating = await _ratingsManager.UpdateRatingForMovie(HttpContext, member, movie, req.NewRating, cancellationToken);
        var allRatings = await _ratingsManager.GetRatingsForMovie(HttpContext, movie, cancellationToken);

        var (average, formattedAverage) = allRatings.AverageRating();
        return Ok(new PostMovieUserRatingResult(
            AverageMovieRating: average,
            AverageMovieRatingFormatted: formattedAverage,
            NewRating: rating.Rating,
            NewRatingFormatted: rating.Rating.HasValue ? rating.Rating.Value.ToString("N2") : ""));
    }
}
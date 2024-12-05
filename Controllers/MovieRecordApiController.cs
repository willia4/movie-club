using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Controllers;


[Controller]
[Route("/api/v2/movie")]
[EnableRateLimiting(policyName: "api")]
public class MovieRecordApiController : Controller
{

    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    private readonly IMovieRatingsManager _ratings;
    
    public MovieRecordApiController(ICosmosDocumentManager<MovieDocument> dataManager, IMovieRatingsManager ratings)
    {
        _dataManager = dataManager;
        _ratings = ratings;
    }
    
    [HttpDelete]
    [Route("{id}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteMovie([FromRoute] string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BadRequestException("id cannot be empty");

        var doc = 
            (await _dataManager.GetDocumentById(id, cancellationToken))
            .ValueOrThrow(() => new NotFoundException());

        await _ratings.DeleteRatingsForMovie(doc, cancellationToken);
        await _dataManager.DeleteDocument(id, cancellationToken);

        return new OkResult();
    }
}
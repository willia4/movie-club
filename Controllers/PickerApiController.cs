using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Controllers;

[Controller]
[Route("/api/v1/picker/shuffle")]
[Authorize(Policy = "Member")]
[EnableRateLimiting(policyName: "api")]
public class PickerApiController : Controller
{
    private readonly IShuffleHasher _shuffleHasher;

    public PickerApiController(IShuffleHasher shuffleHasher)
    {
        _shuffleHasher = shuffleHasher;
    }

    [HttpGet]
    public async Task<int> GetShuffle(CancellationToken cancellationToken)
    {
        return await _shuffleHasher.GetCurrentShuffleValue(cancellationToken);
    }
    
    [HttpPost]
    public async Task<IActionResult> Shuffle(CancellationToken cancellationToken)
    {
        await _shuffleHasher.IncrementCurrentShuffleValue(cancellationToken);
        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> ResetShuffle(CancellationToken cancellationToken)
    {
        await _shuffleHasher.ResetShuffleValue(cancellationToken);
        return Ok();
    }
}
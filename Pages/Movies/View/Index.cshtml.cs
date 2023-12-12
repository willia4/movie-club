using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Pages.Movies.View;

public class Index : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    
    public Index(ICosmosDocumentManager<MovieDocument> dataManager)
    {
        _dataManager = dataManager;
    }
    
    public string MovieTitle = "";
    public string Id = "";
    
    public async Task OnGet(string id, CancellationToken cancellationToken)
    {
        Id = MovieDocument.IdFromSlugId(id);
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new BadRequestException("id cannot be empty");
        }

        var doc =
                (await _dataManager.GetDocumentById(Id, cancellationToken))
                .ValueOrThrow();
        
        MovieTitle = doc.Title;
    }
}
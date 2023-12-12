using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Pages.Movies.View;

public class Index : PageModel
{
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    private readonly IGraphUserManager _userManager;
    private readonly ICoverImageProvider _coverImageProvider;
    
    public Index(ICosmosDocumentManager<MovieDocument> dataManager, IGraphUserManager userManager, ICoverImageProvider coverImageProvider)
    {
        _dataManager = dataManager;
        _userManager = userManager;
        _coverImageProvider = coverImageProvider;
    }
    
    public string MovieTitle = "";
    public string Id = "";
    public string CoverImageHref = "";
    
    public IEnumerable<IGraphUser> AllMembers = Enumerable.Empty<IGraphUser>();
    
    public async Task OnGet(string id, CancellationToken cancellationToken)
    {
        Id = MovieDocument.IdFromSlugId(id);
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new BadRequestException("id cannot be empty");
        }

        AllMembers = await _userManager.GetMembersAsync(cancellationToken);
        
        var doc =
                (await _dataManager.GetDocumentById(Id, cancellationToken))
                .ValueOrThrow();
        
        MovieTitle = doc.Title;
        CoverImageHref = _coverImageProvider.CoverImageUri(doc, ImageSize.Size256).ToString();
    }
}
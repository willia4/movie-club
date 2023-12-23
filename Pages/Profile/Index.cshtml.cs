using System.Collections.Immutable;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Pages.Profile;

public class Index : PageModel
{
    private readonly IGraphUserManager _graphUserManager;
    private readonly IImageUrlProvider<IGraphUser> _profileImageProvider;
    private readonly IImageUrlProvider<MovieDocument> _movieCoverImageProvider;
    private readonly IImageManager _imageManager;
    private readonly IMovieRatingsManager _ratingsManager;
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    
    public bool IsAdmin = false;
    public bool CanEdit = false;
    public string MemberRole = "";
    public string DisplayName = "";
    public string ProfileImageHref = "";

    public string UserId = "";
    public string AADUserName = "";
    
    public ImmutableList<ProfilePageMovieRating> RatingsForUser = ImmutableList<ProfilePageMovieRating>.Empty;

    public Index(IGraphUserManager graphUserManager, IImageUrlProvider<IGraphUser> profileImageProvider, IImageUrlProvider<MovieDocument> movieCoverImageProvider, IImageManager imageManager, IMovieRatingsManager ratingsManager, ICosmosDocumentManager<MovieDocument> _movieManager)
    {
        _graphUserManager = graphUserManager;
        _profileImageProvider = profileImageProvider;
        _movieCoverImageProvider = movieCoverImageProvider.WithDefaultSize(ImageSize.Size128);
        _imageManager = imageManager;
        _ratingsManager = ratingsManager;
        this._movieManager = _movieManager;
    }
    
    private (bool isAdmin, bool canEdit) GetUserPermissions(string pageId)
    {
        var isAdmin = User.IsAdmin();
        var canEdit = isAdmin || 
                      string.Equals(User.NameIdentifier(), pageId, StringComparison.InvariantCultureIgnoreCase);

        return (isAdmin, canEdit);
    }

    private (bool wasSelf, string fixedId) FixupPageId(string id) =>
        id switch
        {
            null => throw new Exceptions.BadRequestException("Invalid route"),
            not null when string.IsNullOrWhiteSpace(id) => throw new Exceptions.BadRequestException("Invalid route"),
            "_self" => (true, User.NameIdentifier() ?? throw new InvalidOperationException("Could not find name identifier claim for logged in user")),
            not null => (false, id.Trim()) 
        };
    
    public async Task OnGet(string id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "User Profile";

        (var _, id) = FixupPageId(id);
        (IsAdmin, CanEdit) = GetUserPermissions(id);

        var user = await _graphUserManager.GetGraphUserAsync(id, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException();
        }

        var ratingsForUser = await _ratingsManager.GetRatingsForUser(HttpContext, user, cancellationToken);
        var moviesForRatings = 
            (await _movieManager.GetDocumentsByIds(ratingsForUser.Select(r => r.MovieId).Distinct(), cancellationToken))
            .ValueOrThrow()
            .ToImmutableDictionary(m => m.id!, m => m);
        
        RatingsForUser = ImmutableList<ProfilePageMovieRating>.Empty;

        RatingsForUser =
            ratingsForUser
                .OfType<RatedMovieRating>()
                .Aggregate(ImmutableList<ProfilePageMovieRating>.Empty, (acc, r) =>
                {
                    if (moviesForRatings.TryGetValue(r.MovieId, out var movie))
                    {
                        return acc.Add(new ProfilePageMovieRating(
                            Slug: movie.SlugId(),
                            Title: movie.Title,
                            CoverHref: _movieCoverImageProvider.ImageUri(movie).ToString(),
                            Rating: r.MovieRating));
                    }
                    return acc;
                })
                .OrderByDescending(r => r.Rating)
                .ToImmutableList();
        
        MemberRole = user.UserRole;
        DisplayName = user.DisplayName;
        
        ProfileImageHref = _profileImageProvider.ImageUri(user, ImageSize.Size512).ToString();

        UserId = id;
        AADUserName = user.AADUserName;
    }

    private async Task<SixLabors.ImageSharp.Image?> GetProfileImageData(CancellationToken cancellationToken)
    {
        var uploadedFile = Request?.Form?.Files?.Where(f => f.Name == "uploaded-file").FirstOrDefault();
        if (uploadedFile != null)
        {
            await using var s = uploadedFile.OpenReadStream();
            var loadImageResult = await
                ImageUtility.LoadImageFromBytes(s, cancellationToken);

            return
                loadImageResult
                    .Match(x => x.First());
        }

        return null;
    }

    public async Task<IActionResult> OnPost(string id, [FromForm(Name = "displayName")] string displayName, CancellationToken cancellationToken)
    {
        (var wasSelf, id) = FixupPageId(id);
        var (_, canEdit) = GetUserPermissions(id);
        
        if (!canEdit) return new UnauthorizedResult();

        var profileImageData = await GetProfileImageData(cancellationToken);
        string? profileImagePrefix = null; 
        IEnumerable<(ImageSize, string)>? profileImageBlobsBySize = null;
        
        if (profileImageData != null)
        {
            var newMetadata = new Dictionary<string, string>
            {
                { "userId", id }
            };

            profileImagePrefix = $"users/{id}/profile";
            profileImageBlobsBySize =
                (await _imageManager.UploadImage(profileImagePrefix, profileImageData, newMetadata, cancellationToken))
                .MapError(x => x.ToInternalServerError("Could not save image"))
                .ValueOrThrow();
        }
        displayName = displayName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(displayName)) throw new Exceptions.BadRequestParameterException(nameof(displayName), $"Display name is requried");

        if (displayName != User.DisplayName())
        {
            var user = await _graphUserManager.GetGraphUserAsync(id, cancellationToken);
            if (user == null) return new NotFoundResult();

            await _graphUserManager.SetUserDisplayName(user, displayName, cancellationToken);
        }

        if (profileImagePrefix != null)
        {
            var user = await _graphUserManager.GetGraphUserAsync(id, cancellationToken);
            if (user == null) return new NotFoundResult();

            await _graphUserManager.SetProfileImage(user, 
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                profileImagePrefix,
                (profileImageBlobsBySize ?? Enumerable.Empty<(ImageSize, string)>())
                    .ToDictionary(t => t.First().FileName, t => t.Second()),
                cancellationToken);
        }
        return new RedirectToPageResult($"Index", new { id = (wasSelf ? "" : id) });
    }
}

public record ProfilePageMovieRating(string Slug, string Title, string CoverHref, decimal Rating);
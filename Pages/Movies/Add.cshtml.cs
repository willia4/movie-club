using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NanoidDotNet;
using Tavis.UriTemplates;
using zinfandel_movie_club.Data;

namespace zinfandel_movie_club.Pages.Movies;

public class Add : PageModel
{
    private readonly IUriDownloader _imageDownloader;
    private readonly IImageManager _imageManager;
    public Add(IUriDownloader imageDownloader, IImageManager imageManager)
    {
        _imageDownloader = imageDownloader;
        _imageManager = imageManager;
    }

    [BindProperty] [Required] public string Title { get; set; } = "";
    [BindProperty] public string Overview { get; set; } = "";
    [BindProperty(Name="rt-critic")] [Range(0.0, 10.0)] public decimal? RottenTomatoesCriticScore { get; set; }
    [BindProperty(Name="rt-user")] [Range(0.0, 10.0)] public decimal? RottenTomatoesUserScore { get; set; }
    [BindProperty(Name="runtime")] public int? RuntimeMinutes { get; set; }

    [BindProperty(Name="tmdb-id")] public string TmdbId { get; set; } = "";
    [BindProperty(Name="tmdb-poster")] public string TmdbPoster { get; set; } = "";
    
    public void OnGet()
    {
        
    }

    private async Task<(Uri? originalUri, string contentType, string fileExtensions, ImmutableArray<byte> bytes)> GetCoverImageData(CancellationToken cancellationToken)
    {
        var uploadedFile = Request?.Form?.Files?.Where(f => f.Name == "uploaded-file").FirstOrDefault();
        if (uploadedFile != null)
        {
            var fileExtension = System.IO.Path.GetExtension(uploadedFile.FileName);
            if (fileExtension.Length > 1 && fileExtension[0] == '.')
                fileExtension = fileExtension.Substring(1);

            await using var s = uploadedFile.OpenReadStream();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, cancellationToken);

            return (null, uploadedFile.ContentType, fileExtension, ms.ToArray().ToImmutableArray());
        }

        if (!string.IsNullOrWhiteSpace(TmdbPoster))
        {
            var uri = new Uri(TmdbPoster);
            var downloadResult =
                (await _imageDownloader.DownloadUri(uri, cancellationToken))
                .MapError(x => new Exceptions.HttpException($"Could not download cover image from TMDB at {TmdbPoster}", HttpStatusCode.InternalServerError, x)
                                        { 
                                            InternalMessage = $"{x.Message}"
                                        });

            return downloadResult.Match(
                (v) => (uri, contentType: v.ContentType, fileExtensions: v.FileExtension, bytes: v.Data),
                (ex) => throw ex);
        }

        return (null, "", "", ImmutableArray<byte>.Empty);
    }
    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        var newId = Nanoid.Generate()!;
        cancellationToken.ThrowIfCancellationRequested();

        var (coverImageUri, coverImageContentType, coverImageFileExtension, coverImageBytes) = await GetCoverImageData(cancellationToken);

        if (coverImageBytes.Length > 0)
        {
            var newMetadata = new Dictionary<string, string>();
            if (coverImageUri != null)
            {
                newMetadata["originalUri"] = coverImageUri.ToString();
            }

            ;
            var uploadResult = await _imageManager.UploadImage(Nanoid.Generate(), coverImageContentType, coverImageFileExtension, coverImageBytes, newMetadata, cancellationToken);
            uploadResult
                .MapError(x => x.ToInternalServerError("Could not save image"))
                .ThrowIfError();
        }
        return new RedirectToPageResult("Add");
        
    }
}
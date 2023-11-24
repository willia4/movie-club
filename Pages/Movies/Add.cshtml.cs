using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NanoidDotNet;
using Tavis.UriTemplates;

namespace zinfandel_movie_club.Pages.Movies;

public class Add : PageModel
{
    private readonly IUriDownloader _imageDownloader;
    public Add(IUriDownloader imageDownloader)
    {
        _imageDownloader = imageDownloader;
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

    private async Task<(string contentType, string fileExtensions, ImmutableArray<byte> bytes)> GetCoverImageData(CancellationToken cancellationToken)
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

            return (uploadedFile.ContentType, fileExtension, ms.ToArray().ToImmutableArray());
        }

        if (!string.IsNullOrWhiteSpace(TmdbPoster))
        {
            var downloadResult =
                (await _imageDownloader.DownloadUri(new Uri(TmdbPoster), cancellationToken))
                .MapError(original =>
                {
                    var x = new Exceptions.HttpException($"Could not download cover image from TMDB at {TmdbPoster}", HttpStatusCode.InternalServerError, original)
                    {
                        InternalMessage = $"{original.Message}"
                    };
                    return x;
                });

            return downloadResult.Match(
                (v) => (contentType: v.ContentType, fileExtensions: v.FileExtension, bytes: v.Data),
                (ex) => throw ex);
        }

        return ("", "", ImmutableArray<byte>.Empty);
    }
    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        var newId = Nanoid.Generate()!;
        cancellationToken.ThrowIfCancellationRequested();

        var (coverImageContentType, coverImageFileExtension, coverImageBytes) = await GetCoverImageData(cancellationToken);
        
        return new RedirectToPageResult("Add");
        
    }
}
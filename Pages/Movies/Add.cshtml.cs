using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NanoidDotNet;
using Tavis.UriTemplates;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Pages.Movies;

public class Add : PageModel
{
    private readonly IUriDownloader _imageDownloader;
    private readonly IImageManager _imageManager;
    private readonly MovieIdGenerator _movieId;
    private readonly ICosmosDocumentManager<MovieDocument> _dataManager;
    public Add(IUriDownloader imageDownloader, IImageManager imageManager, MovieIdGenerator movieId, ICosmosDocumentManager<MovieDocument> dataManager)
    {
        _imageDownloader = imageDownloader;
        _imageManager = imageManager;
        _movieId = movieId;
        _dataManager = dataManager;
    }

    [BindProperty] [Required] public string Title { get; set; } = "";
    [BindProperty] public string Overview { get; set; } = "";
    [BindProperty(Name="rt-critic")] [Range(0.0, 10.0)] public decimal? RottenTomatoesCriticScore { get; set; }
    [BindProperty(Name="rt-user")] [Range(0.0, 10.0)] public decimal? RottenTomatoesUserScore { get; set; }
    [BindProperty(Name = "runtime")] public int? RuntimeMinutes { get; set; }
    [BindProperty(Name = "release-date")] public string ReleaseDate { get; set; } = "";
    
    [BindProperty(Name="tmdb-id")] public string TmdbId { get; set; } = "";
    [BindProperty(Name="tmdb-poster")] public string TmdbPoster { get; set; } = "";
    
    public void OnGet()
    {
        
    }

    private async Task<(Uri? originalUri, SixLabors.ImageSharp.Image? image)> GetCoverImageData(CancellationToken cancellationToken)
    {
        var uploadedFile = Request?.Form?.Files?.Where(f => f.Name == "uploaded-file").FirstOrDefault();
        if (uploadedFile != null)
        {
            await using var s = uploadedFile.OpenReadStream();
            var loadImageResult = await
                ImageUtility.LoadImageFromBytes(s, cancellationToken);

            return
                loadImageResult
                    .Match(x => (originalUri: (Uri?) null, image: x.First()));
        }

        if (!string.IsNullOrWhiteSpace(TmdbPoster))
        {
            var uri = new Uri(TmdbPoster);
            var downloadResult = await
                (await _imageDownloader.DownloadUri(uri, cancellationToken))
                .MapError(x => new Exceptions.HttpException($"Could not download cover image from TMDB at {TmdbPoster}", HttpStatusCode.InternalServerError, x)
                {
                    InternalMessage = $"{x.Message}"
                })
                .AsExceptionErrorType()
                .BindAsync(x => ImageUtility.LoadImageFromBytes(x.Data, cancellationToken));
                
            return
                downloadResult
                    .Match(x => (originalUri: uri,  image: x.First()));
        }

        return (null, null);
    }
    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new BadRequestException("Title is required");
        }

        var newId = _movieId.NewId();
        cancellationToken.ThrowIfCancellationRequested();

        var (coverImageUri, coverImage) = await GetCoverImageData(cancellationToken);

        string? coverImagePrefix = null;
        IEnumerable<(ImageSize, string)>? coverImageBlobsBySize = null;
        if (coverImage != null)
        {
            var newMetadata = new Dictionary<string, string>
            {
                { "movieId", newId },
                { "movieTitle", Title }
            };
            
            if (!string.IsNullOrWhiteSpace(TmdbId))
            {
                newMetadata["tmdb"] = TmdbId;
            }
            
            if (coverImageUri != null)
            {
                newMetadata["originalUri"] = coverImageUri.ToString();
            }

            coverImagePrefix = $"movies/{newId}/cover";

            coverImageBlobsBySize =
                (await _imageManager.UploadImage(coverImagePrefix, coverImage, newMetadata, cancellationToken))
                .MapError(x => x.ToInternalServerError("Could not save image"))
                .ValueOrThrow();
        }

        static string? s(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
        
        var newMovie = new Data.Models.MovieDocument
        {
            id = newId,
            DateAdded = DateTimeOffset.Now,
            Title = Title,
            Overview = s(Overview),
            RottenTomatoesCriticScore = RottenTomatoesCriticScore,
            RottenTomatoesUserScore = RottenTomatoesUserScore,
            RuntimeMinutes = RuntimeMinutes,
            ReleaseDate = s(ReleaseDate),
            TmdbId = s(TmdbId),
            CoverImageTimeStamp = string.IsNullOrWhiteSpace(coverImagePrefix) ? null : DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), 
            CoverImageBlobPrefix = s(coverImagePrefix),
            CoverImagesBySize = (coverImageBlobsBySize ?? Enumerable.Empty<(ImageSize, string)>())
                .ToDictionary(t => t.First().FileName, t => t.Second())
        };

        var redirectId =
            (await _dataManager.UpsertDocument(newMovie, cancellationToken))
            .Match(x => x.SlugId());
        
        return new RedirectToPageResult("View/Index", new { id = redirectId});
    }
}
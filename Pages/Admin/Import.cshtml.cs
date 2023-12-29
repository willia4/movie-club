using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Pages.Admin;

public class Import : PageModel
{
    private readonly IUriDownloader _imageDownloader;
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    private readonly ICosmosDocumentManager<UserRatingDocument> _ratingDocumentManager;
    private readonly IMovieDatabase _tmdb;
    private readonly IImageManager _imageManager;
    private readonly MovieIdGenerator _movieIdGenerator;
    private readonly UserRatingIdGenerator _ratingIdGenerator;
    
    public Import(ICosmosDocumentManager<MovieDocument> movieManager, ICosmosDocumentManager<UserRatingDocument> ratingDocumentManager, IMovieDatabase tmdb,
        IUriDownloader imageDownloader, IImageManager imageManager, MovieIdGenerator movieIdGenerator,
        UserRatingIdGenerator ratingIdGenerator)
    {
        _movieManager = movieManager;
        _ratingDocumentManager = ratingDocumentManager;
        _tmdb = tmdb;
        _imageDownloader = imageDownloader;
        _imageManager = imageManager;
        _movieIdGenerator = movieIdGenerator;
        _ratingIdGenerator = ratingIdGenerator;
    }
    
    
    
    
    private async Task<(Uri? originalUri, SixLabors.ImageSharp.Image? image)> GetCoverImageData(CancellationToken cancellationToken)
    {
        // var uploadedFile = Request?.Form?.Files?.Where(f => f.Name == "uploaded-file").FirstOrDefault();
        // if (uploadedFile != null)
        // {
        //     await using var s = uploadedFile.OpenReadStream();
        //     var loadImageResult = await
        //         ImageUtility.LoadImageFromBytes(s, cancellationToken);
        //
        //     return
        //         loadImageResult
        //             .Match(x => (originalUri: (Uri?) null, image: x.First()));
        // }
        //
        // if (!string.IsNullOrWhiteSpace(TmdbPoster))
        // {
        //     var uri = new Uri(TmdbPoster);
        //     var downloadResult = await
        //         (await _imageDownloader.DownloadUri(uri, cancellationToken))
        //         .MapError(x => new Exceptions.HttpException($"Could not download cover image from TMDB at {TmdbPoster}", HttpStatusCode.InternalServerError, x)
        //         {
        //             InternalMessage = $"{x.Message}"
        //         })
        //         .AsExceptionErrorType()
        //         .BindAsync(x => ImageUtility.LoadImageFromBytes(x.Data, cancellationToken));
        //         
        //     return
        //         downloadResult
        //             .Match(x => (originalUri: uri,  image: x.First()));
        // }
        //
        return (null, null);
    }

    public async Task<IActionResult> OnGet()
    {
        return new ImportResult(_movieManager, _ratingDocumentManager, _tmdb, 
            _imageDownloader, _imageManager, _movieIdGenerator, _ratingIdGenerator);
    }
}

public class ImportResult : IActionResult
{
    private readonly ICosmosDocumentManager<MovieDocument> _movieManager;
    private readonly ICosmosDocumentManager<UserRatingDocument> _ratingDocumentManager;
    private readonly IMovieDatabase _tmdb;
    private readonly IUriDownloader _imageDownloader;
    private readonly IImageManager _imageManager;
    private readonly MovieIdGenerator _movieIdGenerator;
    private readonly UserRatingIdGenerator _ratingIdGenerator;
    public ImportResult(ICosmosDocumentManager<MovieDocument> movieManager, ICosmosDocumentManager<UserRatingDocument> ratingDocumentManager, IMovieDatabase tmdb,
        IUriDownloader imageDownloader, IImageManager imageManager, MovieIdGenerator movieIdGenerator,
        UserRatingIdGenerator ratingIdGenerator)
    {
        _movieManager = movieManager;
        _ratingDocumentManager = ratingDocumentManager;
        _tmdb = tmdb;
        _imageDownloader = imageDownloader;
        _imageManager = imageManager;
        _movieIdGenerator = movieIdGenerator;
        _ratingDocumentManager = ratingDocumentManager;
        _ratingIdGenerator = ratingIdGenerator;
    }

    private async Task ClearExistingMovies(HttpResponse res)
    {
        var allMovies = (await _movieManager.GetAllDocuments(default)).ValueOrThrow();
        await res.WriteAsync($"Deleting {allMovies.Count} movies\n");
        foreach (var m in allMovies)
        {
            var deletionResult = await _movieManager.DeleteDocument(m.id!, default);
            var msg = deletionResult.Match(
                status => $"Deleted {m.Title} with result: {status:G}",
                ex => $"Could not delete {m.Title}: {ex.Message}");
            await res.WriteAsync(msg + "\n");
            await res.Body.FlushAsync();
        }

        await res.WriteAsync("Finished deleting movies\n");
        await res.Body.FlushAsync();
    }
    
    private async Task ClearExistingRatings(HttpResponse res)
    {
        var allRatings = (await _ratingDocumentManager.GetAllDocuments(default)).ValueOrThrow();
        await res.WriteAsync($"Deleting {allRatings.Count} ratings\n");
        foreach (var r in allRatings)
        {
            var deletionResult = await _movieManager.DeleteDocument(r.id!, default);
            var msg = deletionResult.Match(
                status => $"Deleted rating with result: {status:G}",
                ex => $"Could not delete rating: {ex.Message}");
            await res.WriteAsync(msg + "\n");
            await res.Body.FlushAsync();
        }

        await res.WriteAsync("Finished deleting ratings\n");
        await res.Body.FlushAsync();
    }
    
    private async Task<ImmutableList<DataRow>> GetData()
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var s = assembly.GetManifestResourceStream("zinfandel_movie_club.import_data.csv");
        if (s == null) throw new InvalidOperationException("Data was null");
        using var reader = new StreamReader(s);
        using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
        var records = await csv.GetRecordsAsync<DataRow>().ToImmutableList(default);
        return records;
    }

    private async Task<(string, Dictionary<string, string>)> HandleCoverImage(HttpResponse res, MovieDetailResult data, string newId)
    {
        await res.WriteAsync($"Handling cover image for {data.Title}\n");
        await res.WriteAsync($"Downloading image {data.PosterHref}\n");
        var coverImageData = (await
            (await _imageDownloader
                .DownloadUri(new Uri(data.PosterHref), default))
            .AsExceptionErrorType()
            .BindAsync(async x => await ImageUtility.LoadImageFromBytes(x.Data, default)))
            .ValueOrThrow()
            .First();
    
        var newMetadata = new Dictionary<string, string>
        {
            { "movieId", newId },
            { "movieTitle", data.Title },
            { "tmdb", data.Id.ToString()},
            { "originalUri", data.PosterHref }
        };
        
        var coverImagePrefix = $"movies/{newId}/cover";

        await res.WriteAsync($"Uploading images for prefix {coverImagePrefix}\n");
        
        var coverImageBlobsBySize =
            (await _imageManager.UploadImage(coverImagePrefix, coverImageData, newMetadata, default))
            .MapError(x => x.ToInternalServerError("Could not save image"))
            .ValueOrThrow()
            .ToDictionary(t => t.First().FileName, t => t.Second());

        await res.WriteAsync($"Done handling cover image for {data.PosterHref}\n");
        
        return (coverImagePrefix, coverImageBlobsBySize);
    }
    private async Task ImportDataRow(HttpResponse res, DataRow row)
    {
        static string? s(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
        
        var newId = _movieIdGenerator.NewId();
        await res.WriteAsync($"Importing {row.Title} as {newId}\n");
        var data = await _tmdb.GetDetails(row.TMDBID, default);
        data = data ?? throw new InvalidOperationException("Data is null");
        var (coverImagePrefix, coverImageBlobs) = await HandleCoverImage(res, data, newId);

        var newMovie = new MovieDocument
        {
            id = newId,
            DateAdded = DateTimeOffset.Now,
            Title = data.Title,
            Overview = s(data.Overview),
            RottenTomatoesCriticScore = row.RottenTomatoesCritics,
            RottenTomatoesUserScore = row.RottenTomatoesAudience,
            RuntimeMinutes = row.Runtime,
            TmdbId = row.TMDBID.ToString(),
            CoverImageTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            CoverImageBlobPrefix = coverImagePrefix,
            CoverImagesBySize = coverImageBlobs
        };

        newMovie = (await _movieManager.UpsertDocument(newMovie, default)).ValueOrThrow();
        await res.WriteAsync($"Wrote movie document {newMovie.id}\n");

        if (row.PandaRating.HasValue)
        {
            (await _ratingDocumentManager.UpsertDocument(new UserRatingDocument()
            {
                id = _ratingIdGenerator.NewId(),
                MovieId = newId,
                Rating = row.PandaRating.Value,
                UserId = "67cbae96-cc4f-4461-8677-f957c3097667"
            }, default)).ThrowIfError();

            await res.WriteAsync($"Wrote rating for Panda: {row.PandaRating}\n");
        }

        if (row.TimRating.HasValue)
        {
            (await _ratingDocumentManager.UpsertDocument(new UserRatingDocument()
            {
                id = _ratingIdGenerator.NewId(),
                MovieId = newId,
                Rating = row.TimRating.Value,
                UserId = "f309236c-fb29-4d23-8a15-84c47fc02fef"
            }, default)).ThrowIfError();
            
            await res.WriteAsync($"Wrote rating for Tim: {row.TimRating}\n");
        }

        if (row.BranRating.HasValue)
        {
            (await _ratingDocumentManager.UpsertDocument(new UserRatingDocument()
            {
                id = _ratingIdGenerator.NewId(),
                MovieId = newId,
                Rating = row.BranRating.Value,
                UserId = "80885b5e-ab65-4355-a93a-98d8c4ea187b"
            }, default)).ThrowIfError();
            
            await res.WriteAsync($"Wrote rating for Bran: {row.BranRating}\n");
        }

        if (row.JamesRating.HasValue)
        {
            (await _ratingDocumentManager.UpsertDocument(new UserRatingDocument()
            {
                id = _ratingIdGenerator.NewId(),
                MovieId = newId,
                Rating = row.JamesRating.Value,
                UserId = "47f2fa98-a9a4-4f79-8692-7c52b7d42cd9"
            }, default)).ThrowIfError();
            
            await res.WriteAsync($"Wrote rating for James: {row.JamesRating}\n");
        }

        await res.WriteAsync($"Done importing {row.Title}\n");
    }
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var res = context.HttpContext.Response;
        res.Headers.ContentType = "text/plain";

        await ClearExistingMovies(res);
        await ClearExistingRatings(res);

        var data = await GetData();
        await res.WriteAsync($"Found {data.Count} records to import\n");

        foreach (var row in data)
        {
            try
            {
                await ImportDataRow(res, row);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                await res.WriteAsync("Uncaught exception: " + ex.Message + "\n");
            }
        }

        await res.WriteAsync("\n\nDone!\n");
    }
    
    private class DataRow
    {
        public string Title { get; set; } = "";
        public string WatchDate { get; set; } = "";
        public decimal? PandaRating { get; set; }
        public decimal? TimRating { get; set; }
        public decimal? BranRating { get; set; }
        public decimal? JamesRating { get; set; }
        public decimal? RottenTomatoesCritics { get; set; }
        public decimal? RottenTomatoesAudience { get; set; }
        public int Runtime { get; set; }
        public int TMDBID { get; set; }
    }
}
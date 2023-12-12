using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface ICoverImageProvider
{
    public Uri CoverImageUri(MovieDocument movieDocument, ImageSize size);
    public Uri CoverImageUri(MovieDocument movieDocument) => CoverImageUri(movieDocument, ImageSize.SizeOriginal);

    public Uri CoverImageUri(MovieDocument movieDocument, int width)
    {
        var size =
            ImageSize.AllImageSizes
                .Where(x => x.MaxWidth.HasValue)
                .OrderBy(x => x.MaxWidth!.Value)
                .FirstOrDefault(x => x.MaxWidth >= width);
        return CoverImageUri(movieDocument, size ?? ImageSize.SizeOriginal);
    }
}

public class CoverImageProvider : ICoverImageProvider
{
    private readonly string _storageAccountUrlPrefix;
    
    public CoverImageProvider(IOptions<DatabaseConfig> databaseConfig)
    {
        var config = databaseConfig.Value.StorageAccount;
        var connectionString = config.ConnectionString;
        var parsed = 
            connectionString
                .Split(";")
                .Select(p => (p, p.Split("=")))
                .ToDictionary(
                    p => p.Second().Length == 2 ? p.Second()[0] : p.First(),
                    p => p.Second().Length == 2 ? p.Second()[1] : p.First());
        if (parsed.TryGetValue("AccountName", out var accountName) &&
            parsed.TryGetValue("EndpointSuffix", out var suffix))
        {
            _storageAccountUrlPrefix = $"https://{accountName}.blob.{suffix}/{config.ImagesContainer}/";
        }
        else
        {
            throw "Could not parse connection string".ToException();
        }

    }
    public Uri CoverImageUri(MovieDocument movieDocument, ImageSize size)
    {
        if (movieDocument.CoverImagesBySize.TryGetValue(size.FileName, out var suffix))
        {
            return new Uri($"{_storageAccountUrlPrefix}{suffix}?v={movieDocument.CoverImageTimeStamp}");
        }

        return new Uri($"/img/default_movie_cover_{size.FileName}.png", UriKind.Relative);
    }
}
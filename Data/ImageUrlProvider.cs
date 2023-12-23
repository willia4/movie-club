using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface IImageUrlProvider<T>
{
    public Uri ImageUri(T item, ImageSize size);
    public Uri ImageUri(T item) => ImageUri(item, ImageSize.SizeOriginal);

    public Uri ImageUri(T item, int width)
    {
        var size =
            ImageSize.AllImageSizes
                .Where(x => x.MaxWidth.HasValue)
                .OrderBy(x => x.MaxWidth!.Value)
                .FirstOrDefault(x => x.MaxWidth >= width);
        return size != null ? ImageUri(item, size) : ImageUri(item);
    }

    public IImageUrlProvider<T> WithDefaultSize(ImageSize newDefault) => new DefaultSizeCoverImageProvider<T>(this, newDefault);
}

public abstract class BasicStorageAccountImageProvider<T> : IImageUrlProvider<T>
{
    protected readonly string _storageAccountName;
    protected readonly string _storageAccountDomainSuffix;
    protected readonly string _storageAccountImagesContainer;
    
    protected BasicStorageAccountImageProvider(IOptions<DatabaseConfig> databaseConfig)
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
            _storageAccountName = accountName;
            _storageAccountDomainSuffix = suffix;
            _storageAccountImagesContainer = config.ImagesContainer;
        }
        else
        {
            throw "Could not parse connection string".ToException();
        }
    }

    protected virtual string StorageAccountUrlPrefix => $"https://{_storageAccountName}.blob.{_storageAccountDomainSuffix}/{_storageAccountImagesContainer}/";
    public virtual Uri ImageUri(T item, ImageSize size)
    {
        if (ImagesBySize(item)?.TryGetValue(size.FileName, out var suffix) ?? false)
        {
            return new Uri($"{StorageAccountUrlPrefix}{suffix}?v={CacheBuster(item)}");
        }

        return DefaultImage(size);
    }

    protected abstract Uri DefaultImage(ImageSize size);

    protected abstract IDictionary<string, string>? ImagesBySize(T item);
    protected abstract string CacheBuster(T item);
}

public class ProfileImageProvider : BasicStorageAccountImageProvider<IGraphUser>
{
    public ProfileImageProvider(IOptions<DatabaseConfig> databaseConfig) : base(databaseConfig)
    {
    }

    protected override Uri DefaultImage(ImageSize size) => new Uri($"/img/default_profile_picture_{size.FileName}.png", UriKind.Relative);
    protected override IDictionary<string, string>? ImagesBySize(IGraphUser item) => item.ProfileImagesBySize;
    protected override string CacheBuster(IGraphUser item) => item.ProfileImageTimeStamp ?? "";
}

public class CoverImageProvider : BasicStorageAccountImageProvider<MovieDocument>
{
    public CoverImageProvider(IOptions<DatabaseConfig> databaseConfig) : base(databaseConfig)
    {
    }

    protected override Uri DefaultImage(ImageSize size) => new Uri($"/img/default_movie_cover_{size.FileName}.png", UriKind.Relative);
    protected override IDictionary<string, string>? ImagesBySize(MovieDocument item) => item.CoverImagesBySize;
    protected override string CacheBuster(MovieDocument item) => item.CoverImageTimeStamp ?? "";
}

public class DefaultSizeCoverImageProvider<T> : IImageUrlProvider<T>
{
    private readonly IImageUrlProvider<T> _wrapped;
    private readonly ImageSize _defaultSize;
    public DefaultSizeCoverImageProvider(IImageUrlProvider<T> wrapped, ImageSize defaultSize)
    {
        _wrapped = wrapped;
        _defaultSize = defaultSize;
    }

    public Uri ImageUri(T item, ImageSize size) => _wrapped.ImageUri(item, size);
    public Uri ImageUri(T item) => ImageUri(item, _defaultSize);
}


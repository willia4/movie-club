using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;

namespace zinfandel_movie_club.Data;

public interface IProfileImageProvider
{
    public Uri ProfileImageUri(IGraphUser user, ImageSize size);
    public Uri ProfileImageUri(IGraphUser user) => ProfileImageUri(user, ImageSize.SizeOriginal);

    public Uri ProfileImageUri(IGraphUser user, int width)
    {
        var size =
            ImageSize.AllImageSizes
                .Where(x => x.MaxWidth.HasValue)
                .OrderBy(x => x.MaxWidth!.Value)
                .FirstOrDefault(x => x.MaxWidth >= width);
        return ProfileImageUri(user, size ?? ImageSize.SizeOriginal);
    }
}

public class ProfileImageProvider : IProfileImageProvider
{
    private readonly string _storageAccountUrlPrefix;
    
    public ProfileImageProvider(IOptions<DatabaseConfig> databaseConfig)
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
    
    public Uri ProfileImageUri(IGraphUser user, ImageSize size)
    {
        if (user.ProfileImagesBySize?.TryGetValue(size.FileName, out var suffix) ?? false)
        {
            return new Uri($"{_storageAccountUrlPrefix}{suffix}?v={user.ProfileImageTimeStamp}");
        }
        
        return new Uri($"/img/default_profile_picture_{size.FileName}.png", UriKind.Relative);
    }
}
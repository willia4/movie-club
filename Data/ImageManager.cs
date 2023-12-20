using System.Collections.Immutable;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using Tavis.UriTemplates;
using zinfandel_movie_club.Config;

namespace zinfandel_movie_club.Data;

public interface IImageManager
{
    public Task<Result<ImmutableList<(ImageSize, string)>,Exception>> UploadImage(string id, SixLabors.ImageSharp.Image image, IDictionary<string, string> metadata, CancellationToken cancellationToken);
}

public record ImageBlob(string StorageAccountName, string BlobId);


public class ImageManager : IImageManager
{
    private readonly BlobContainerClient _client;
    
    public ImageManager(IOptions<DatabaseConfig> databaseConfig)
    {
        _client = new BlobContainerClient(
            connectionString: databaseConfig.Value.StorageAccount.ConnectionString,
            blobContainerName: databaseConfig.Value.StorageAccount.ImagesContainer);
    }

    private async Task<Result<BlobClient, Exception>> UploadBlob(string blobName, string contentType, IDictionary<string, string> metadata, ImmutableArray<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            var blob = _client.GetBlobClient(blobName);
            await blob.UploadAsync(content: data.AsBinaryData(), overwrite: true, cancellationToken: cancellationToken);
            
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!properties.HasValue)
            {
                return Result<BlobClient, Exception>.Error($"Could not get properties from blob after uploading {blobName}".ToException());
            }
            
            var newHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,

                CacheControl = $"max-age={TimeSpan.FromHours(5).TotalSeconds}",
                ContentDisposition = properties.Value.ContentDisposition,
                ContentEncoding = properties.Value.ContentEncoding,
                ContentHash = properties.Value.ContentHash
            };

            try
            {
                await blob.SetHttpHeadersAsync(newHeaders, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<BlobClient, Exception>.Error($"Could not set headers for blob after uploading {blobName}: {ex.Message}".ToException(ex));
            }
            
            try
            {
                await blob.SetMetadataAsync(metadata, cancellationToken: cancellationToken);                
            }
            catch (Exception ex)
            {
                return Result<BlobClient, Exception>.Error($"Could not set custom metadata for blob after uploading {blobName}: {ex.Message}".ToException(ex));
            }

            return Result<BlobClient, Exception>.Ok(blob);
        }
        catch (Exception outer)
        {
            return Result<BlobClient, Exception>.Error(outer);
        }
    }
    
    public async Task<Result<ImmutableList<(ImageSize, string)>, Exception>> UploadImage(string blobPrefix, SixLabors.ImageSharp.Image image, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        var sizedImages = ImageSize.AllImageSizes.Select(s => (s, s.SizedImage(image))).ToImmutableList();
 
        var results = ImmutableList<(ImageSize, string)>.Empty;

        var tasks = sizedImages
            .Select(async t =>
            {
                var (size, img) = t;
                var blobName = $"{blobPrefix}/{size.FileName}.jpg";
                var blobBytes = img.ToJpegImmutableByteArray();

                var metadata_ =
                    metadata.ToImmutableDictionary()
                        .SetItem("size", size.FileName)
                        .SetItem("height", img.Height.ToString())
                        .SetItem("width", img.Width.ToString());

                var uploadResult = await UploadBlob(
                    blobName: blobName,
                    contentType: "image/jpeg",
                    metadata: metadata_,
                    data: blobBytes,
                    cancellationToken: cancellationToken);

                if (uploadResult.IsError)
                {
                    return uploadResult.ChangeResultTypeIfError<(ImageSize, string)>();
                }

                return Result<(ImageSize, string), Exception>.Ok((size, blobName));
            })
            .ToArray();

        var results_ = await Task.WhenAll(tasks);
        var errors = results_.Where(r => r.IsError);
        if (errors.Any())
        {
            return errors.First().ChangeResultTypeIfError<ImmutableList<(ImageSize, string)>>();
        }
        
        // we know there weren't errors because we'd have already returned
        var collectedResults = results_.Select(r => r.Unwrap());
        return Result<ImmutableList<(ImageSize, string)>, Exception>.Ok(collectedResults.ToImmutableList());
    }
}
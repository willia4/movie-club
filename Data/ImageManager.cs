using System.Collections.Immutable;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Tavis.UriTemplates;
using zinfandel_movie_club.Config;

namespace zinfandel_movie_club.Data;

public interface IImageManager
{
    public Task<Result<ImageBlob,Exception>> UploadImage(string id, string contentType, string fileExtension, ImmutableArray<byte> data, IDictionary<string, string> metadata, CancellationToken cancellationToken);
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

    public async Task<Result<ImageBlob, Exception>> UploadImage(string id, string contentType, string fileExtension, ImmutableArray<byte> data, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        try
        {
            await _client.UploadBlobAsync(blobName: id, content: data.AsBinaryData(), cancellationToken: cancellationToken);
            
            var blob = _client.GetBlobClient(id);
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!properties.HasValue)
            {
                return Result<ImageBlob, Exception>.Error($"Could not get properties from blob after uploading {id}".ToException());
            }
            
            var newHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,

                CacheControl = properties.Value.CacheControl,
                ContentDisposition = properties.Value.ContentDisposition,
                ContentEncoding = properties.Value.ContentEncoding,
                ContentHash = properties.Value.ContentHash
            };

            var newMetadata =
                (metadata ?? ImmutableDictionary<string, string>.Empty)
                .ToImmutableDictionary()
                .SetItem("originalFileExtension", fileExtension);

            try
            {
                await blob.SetHttpHeadersAsync(newHeaders, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<ImageBlob, Exception>.Error($"Could not set headers for blob after uploading {id}: {ex.Message}".ToException(ex));
            }
            
            try
            {
                await blob.SetMetadataAsync(newMetadata, cancellationToken: cancellationToken);                
            }
            catch (Exception ex)
            {
                return Result<ImageBlob, Exception>.Error($"Could not set custom metadata for blob after uploading {id}: {ex.Message}".ToException(ex));
            }

            return Result<ImageBlob, Exception>.Ok(new ImageBlob(StorageAccountName: _client.AccountName, BlobId: blob.Name));
        }
        catch (Exception outer)
        {
            return Result<ImageBlob, Exception>.Error(outer);
        }
        
    }
}
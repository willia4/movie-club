using System.Collections.Immutable;
using System.Net;
using Tavis.UriTemplates;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club;

public record DownloadedData(Uri SourceUri, string ContentType, string FileExtension, ImmutableArray<byte> Data);

public interface IUriDownloader
{
    Task<Result<DownloadedData, UriDownloaderException>> DownloadUri(Uri uri, CancellationToken cancellationToken);
}

public class UriDownloader : IUriDownloader
{
    private static readonly Lazy<ImmutableDictionary<string, string>> ExtensionToMimeTypeMappings = new Lazy<ImmutableDictionary<string, string>>(() =>
    {
        var fileExtensionProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        return fileExtensionProvider.Mappings.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    });
    
    private static readonly Lazy<ImmutableDictionary<string, string>> MimeTypeToExtensionMappings = new Lazy<ImmutableDictionary<string, string>>(() =>
    {
        var fileExtensionProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        return fileExtensionProvider.Mappings.ToDictionary(kvp => kvp.Value, kvp => kvp.Key).ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    });

    private readonly IHttpClientFactory _clientFactory;

    public UriDownloader(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<Result<DownloadedData, UriDownloaderException>> DownloadUri(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _clientFactory.CreateClient();
            var fileExtension = uri.FileExtension();

            var msg = await client.GetAsync(uri, cancellationToken);

            if (!msg.IsSuccessStatusCode)
            {
                var errorBody = await msg.Content.ReadAsStringAsync(cancellationToken);
                return Result<DownloadedData, UriDownloaderException>.Error(new UriDownloaderStatusCodeException(uri, msg.StatusCode, errorBody));
            }

            var data = await msg.Content.ReadAsByteArrayAsync(cancellationToken);

            var contentType = "";
            if (msg.Content.Headers.ContentType?.MediaType is { } mediaType)
            {
                contentType = mediaType;
            }
            else if (!string.IsNullOrWhiteSpace(fileExtension) && ExtensionToMimeTypeMappings.Value.TryGetKey($".{fileExtension}", out var mappedMimeType))
            {
                contentType = mappedMimeType ?? "";
            }

            if (string.IsNullOrWhiteSpace(fileExtension) && !string.IsNullOrWhiteSpace(contentType))
            {
                if (MimeTypeToExtensionMappings.Value.TryGetValue(contentType, out var foundFileExtension))
                {
                    fileExtension = foundFileExtension.Substring(1);
                }
            }

            return Result<DownloadedData, UriDownloaderException>.Ok(new DownloadedData(SourceUri: uri, ContentType: contentType, FileExtension: fileExtension, Data: data.ToImmutableArray()));
        }
        catch (UriDownloaderException outer)
        {
            return Result<DownloadedData, UriDownloaderException>.Error(outer);
        }
        catch (Exception outer)
        {
            return Result<DownloadedData, UriDownloaderException>.Error(new UriDownloaderException(uri, outer));
        }
    }
}

public class UriDownloaderException : Exception
{
    public Uri Uri { get; init; }

    public UriDownloaderException(Uri uri) : base($"Could not download from uri {uri}")
    {
        Uri = uri;
    }

    public UriDownloaderException(Uri uri, string message) : base($"Could not download from uri {uri}: {message}")
    {
        Uri = uri;
    }
    
    public UriDownloaderException(Uri uri, Exception inner) : base($"Could not download from uri {uri}: {inner.Message}", inner)
    {
        Uri = uri;
    }
    
    public UriDownloaderException(Uri uri, string message, Exception inner) : base($"Could not download from uri {uri}: {message}", inner)
    {
        Uri = uri;
    }
}

public class UriDownloaderStatusCodeException : UriDownloaderException
{
    public int StatusCode { get; init; }
    public string ErrorBody { get; init; }

    public UriDownloaderStatusCodeException(Uri uri, int statusCode, string errorBody) : base(uri, $"Could not download from uri {uri}. Result was {statusCode}: {errorBody}")
    {
        StatusCode = statusCode;
        ErrorBody = errorBody;
    }

    public UriDownloaderStatusCodeException(Uri uri, HttpStatusCode statusCode, string errorBody) : this(uri, (int)statusCode, errorBody)
    {
        
    }
}
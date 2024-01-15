using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface IMovieDatabase
{
    public IAsyncEnumerable<Models.MovieSearchResult> Search(string searchText, CancellationToken cancellationToken);
    public Task<MovieDetailResult?> GetDetails(int id, CancellationToken cancellationToken);
    public Task<MovieStreamingProviderResult> GetWatchProviders(int movieId, CancellationToken cancellationToken);
}

public enum BackdropSize
{
    W300,
    W780,
    W1280,
    Original
}

public enum PosterSize
{
    W92,
    W154,
    W185,
    W342,
    W500,
    W780,
    Original
}

public class TheMovieDatabase : IMovieDatabase
{
    private readonly Config.TMDBConfig _tmdbConfig;
    private readonly IHttpClientFactory _clientFactory;
    
    public TheMovieDatabase(IOptions<Config.TMDBConfig> tmdConfig, IHttpClientFactory clientFactory)
    {
        _tmdbConfig = tmdConfig.Value;
        _clientFactory = clientFactory;
    }

    private HttpRequestMessage CreateRequest(string path, IDictionary<string, string>? queryParams = default)
    {
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        var urlBuilder = new UriBuilder("https://api.themoviedb.org")
        {
            Path = path
        };

        var queryString =
            (queryParams?.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, string>>())
            .Aggregate(new System.Text.StringBuilder(), (acc, next) =>
            {
                acc.Append(acc.Length == 0 ? "?" : "&");
                acc.Append(HttpUtility.UrlEncode(next.Key));
                acc.Append('=');
                acc.Append(HttpUtility.UrlEncode(next.Value));

                return acc;
            })
            .ToString();

        if (queryString.Length > 0)
        {
            urlBuilder.Query = queryString;
        }

        var req = new HttpRequestMessage
        {
            RequestUri = urlBuilder.Uri,
            Method = HttpMethod.Get
        };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tmdbConfig.ApiToken);
        return req;
    }

    public async Task<MovieDetailResult?> GetDetails(int id, CancellationToken cancellationToken)
    {
        var req = CreateRequest($"/3/movie/{id}");
        using var client = _clientFactory.CreateClient();
        var res = await client.SendAsync(req, cancellationToken);

        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (res.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not load details for movie {id}: {body}");
        }

        var details = System.Text.Json.JsonSerializer.Deserialize<TmdbDetailResponse>(body)!;
        
        var posterHref = string.IsNullOrEmpty(details.PosterPath)
            ? ""
            : $"https://image.tmdb.org/t/p/w780/{details.PosterPath}";
        
        var backdropHref = string.IsNullOrEmpty(details.BackdropPath)
            ? ""
            : $"https://image.tmdb.org/t/p/w1280/{details.BackdropPath}";

        return new MovieDetailResult(Id: details.Id, Title: details.Title, Overview: details.Overview,
            PosterHref: posterHref, BackdropHref: backdropHref, ReleaseDate: details.ReleaseDate, RuntimeMinutes: details.RuntimeMinutes,
            Rating: details.Rating);
    }

    public async IAsyncEnumerable<Models.MovieSearchResult> Search(string searchText, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var page = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(searchText)) throw new ArgumentException($"{nameof(searchText)} cannot be empty", nameof(searchText));
            const string path = "/3/search/movie";
            var query = ImmutableDictionary<string, string>.Empty.SetItem("query", searchText)
                .SetItem("include_adult", "false")
                .SetItem("language", "en-US")
                .SetItem("page", $"{page}");

            var req = CreateRequest(path, query);
            using var client = _clientFactory.CreateClient();
            var res = await client.SendAsync(req, cancellationToken);

            var body = await res.Content.ReadAsStringAsync(cancellationToken);

            if (!res.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Error status from TMDB when searching: {res.StatusCode}, {body}");
            }

            var results = System.Text.Json.JsonSerializer.Deserialize<TmdbSearchResponse>(body);
            if (results == null)
            {
                throw new InvalidOperationException($"Could not parse response from TMDB when searching");
            }

            foreach (var movieResult in results.Results)
            {
                var movieHref = string.IsNullOrEmpty(movieResult.PosterPath)
                    ? ""
                    : $"https://image.tmdb.org/t/p/w185/{movieResult.PosterPath}";
                
                yield return new MovieSearchResult(Id: movieResult.Id, Title: movieResult.Title, Overview: movieResult.Overview, PosterHref: movieHref, ReleaseDate: movieResult.ReleaseDate );
            }

            if (results.CurrentPage < results.TotalPages)
            {
                page += 1;
                continue;
            }

            break;
        }
    }

    public async Task<MovieStreamingProviderResult> GetWatchProviders(int movieId, CancellationToken cancellationToken)
    {
        // https://developer.themoviedb.org/reference/movie-watch-providers
        var allowedProviderIds = new int[]
        {
            8, // Netflix
            9, // Amazon Prime
            337, // Disney Plus
            9, // Apple TV Plus
            2, // Apple TV
            15, // Hulu
            384, // HBO Max
            1899, // Max
            386, // Peacock
            387, // Peacock Premium
            87, // Acorn
            151 // BritBox
        };
        
        var req = CreateRequest($"/3/movie/{movieId}/watch/providers");
        using var client = _clientFactory.CreateClient();
        var res = await client.SendAsync(req, cancellationToken);

        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        var results = System.Text.Json.JsonSerializer.Deserialize<TmdbWatchProvidersResponse>(body)!;

        if (res.IsSuccessStatusCode && results?.Results?.UnitedStates is { } us)
        {
            static Uri MakeLogoUri(string logoPath)
            {
                return new Uri($"https://image.tmdb.org/t/p/original{logoPath}");
            }
            
            var providers =
                us.StreamingProviders
                    .Where(p => allowedProviderIds.Contains(p.Id))
                    .OrderBy(p => p.DisplayPriority)
                    .Select(p => new MovieStreamingProvider(p.Name, MakeLogoUri(p.LogoPath)))
                    .ToImmutableList();

            return new MovieStreamingProviderResult(results.Id, new Uri(us.Link), providers);
        }
        return new MovieStreamingProviderResult(movieId, null, ImmutableList<MovieStreamingProvider>.Empty);
    }
    
    private class TmdbSearchResponseMovie
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("original_title")] public string OriginalTitle { get; set; } = "";
        [JsonPropertyName("overview")] public string Overview { get; set; } = "";
        [JsonPropertyName("poster_path")] public string PosterPath { get; set; } = "";
        [JsonPropertyName("backdrop_path")] public string BackdropPath { get; set; } = "";
        [JsonPropertyName("genre_ids")] public ImmutableList<int> GenreIds { get; set; } = ImmutableList<int>.Empty;
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        
        [JsonPropertyName("title")] public string Title { get; set; } = "";
    }
    
    private class TmdbSearchResponse
    {
        [JsonPropertyName("page")] public int CurrentPage { get; set; }
        [JsonPropertyName("results")] public ImmutableList<TmdbSearchResponseMovie> Results { get; set; } = ImmutableList<TmdbSearchResponseMovie>.Empty;
        [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
        [JsonPropertyName("total_results")] public int TotalResults { get; set; }
    }

    private class TmdbDetailResponseGenre
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }
    
    private class TmdbDetailResponse
    {
        [JsonPropertyName("genres")] public ImmutableList<TmdbDetailResponseGenre> Genres { get; set; } = ImmutableList<TmdbDetailResponseGenre>.Empty;
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("imdb_id")] public string ImdbId {get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("overview")] public string Overview { get; set; } = "";
        [JsonPropertyName("poster_path")] public string PosterPath { get; set; } = "";
        [JsonPropertyName("backdrop_path")] public string BackdropPath { get; set; } = "";
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        [JsonPropertyName("runtime")] public int RuntimeMinutes { get; set; }
        [JsonPropertyName("vote_average")] public decimal Rating { get; set; } = 0.0M;
    }

    private class TmdbWatchProvidersResponse
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("results")] public TmdbWatchProvidersResponseResults Results { get; set; } = new();
    }

    private class TmdbWatchProvidersResponseResults
    {
        [JsonPropertyName("US")] public TmdbWatchProviderRegion? UnitedStates { get; set; }
    }

    private class TmdbWatchProviderRegion
    {
        [JsonPropertyName("link")] public string Link { get; set; } = "";
        // ReSharper disable once CollectionNeverUpdated.Local
        [JsonPropertyName("flatrate")] public List<TmdbWatchProvider> StreamingProviders { get; set; } = new();
    }

    private class TmdbWatchProvider
    {
        [JsonPropertyName("provider_id")] public int Id { get; set; }
        [JsonPropertyName("logo_path")] public string LogoPath { get; set; } = "";
        [JsonPropertyName("display_priority")] public int DisplayPriority { get; set; }
        [JsonPropertyName("provider_name")] public string Name { get; set; } = "";
    }
}
using System.Collections.Immutable;
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
            var client = _clientFactory.CreateClient();
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
}
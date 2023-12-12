using System.Collections.Immutable;

namespace zinfandel_movie_club.Data.Models;

public record MovieSearchResult(int Id, string Title, string Overview, string PosterHref, string ReleaseDate);

public record MovieDetailResult(int Id, string Title, string Overview, string PosterHref, string BackdropHref, string ReleaseDate, int RuntimeMinutes, decimal Rating);

public record MovieStreamingProviderResult(int MovieId, Uri? TmdbWatchPage, ImmutableList<MovieStreamingProvider> Providers);
public record MovieStreamingProvider(string Name, Uri Logo);
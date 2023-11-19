namespace zinfandel_movie_club.Data.Models;

public record MovieSearchResult(int Id, string Title, string Overview, string PosterHref, string ReleaseDate);

public record MovieDetailResult(int Id, string Title, string Overview, string PosterHref, string BackdropHref, string ReleaseDate, int RuntimeMinutes);
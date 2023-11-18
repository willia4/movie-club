namespace zinfandel_movie_club.Controllers.Models;

public record SearchMoviesRequest(string TitleSearch, int MaxResults = 25);

public record SearchMoviesResponse(string Id, string Title, string Overview, string PosterHref, string ReleaseDate);
namespace zinfandel_movie_club.Controllers.Models;

public record MovieDetailResponse(string Id, string Title, string Overview, string PosterHref, string BackdropHref, string ReleaseDate, int RuntimeMinutes);
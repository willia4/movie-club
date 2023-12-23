namespace zinfandel_movie_club.Controllers.Models;

public record PostMovieUserRatingRequest(decimal? NewRating);

public record PostMovieUserRatingResult(decimal? AverageMovieRating, string AverageMovieRatingFormatted, decimal? NewRating, string NewRatingFormatted);
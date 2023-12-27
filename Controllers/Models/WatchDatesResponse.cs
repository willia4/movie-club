using System.Collections.Immutable;

namespace zinfandel_movie_club.Controllers.Models;

public record SetWatchDateRequest(DateOnly? NewWatchDate);
public record WatchDatesResponse(DateOnly MostRecentWatchDate, ImmutableList<DateOnly> AllWatchDates);

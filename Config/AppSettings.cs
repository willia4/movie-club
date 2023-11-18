using System.Collections.Immutable;

namespace zinfandel_movie_club.Config;

public class AppSettings
{
    public List<string> SuperUserIds { get; set; } = new();
}
using System.Collections.Immutable;

namespace zinfandel_movie_club.Config;

public class AppSettings
{
    //public string EnvironmentId { get; set; } = "";
    public List<string> SuperUserIds { get; set; } = new();
}
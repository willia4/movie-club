using System.Runtime.InteropServices.JavaScript;

namespace zinfandel_movie_club.Config;

public sealed class Branding
{
    public Branding(IConfiguration config)
    {
        var section = config.GetSection("Branding");
        SiteName = section.GetValue<string>("SiteName") ?? "Config Error: Site Name";
    }
    
    public string SiteName { get; }
}

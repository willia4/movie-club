namespace zinfandel_movie_club.Config;

public class DatabaseConfig
{
    public CosmosConfig Cosmos { get; set; } = new();
}

public class CosmosConfig
{
    public string ConnectionString { get; set; } = "";
    public string Database { get; set; } = "";
    public string Container { get; set; } = "";
}
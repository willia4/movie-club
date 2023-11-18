namespace zinfandel_movie_club.Config;

public class DatabaseConfig
{
    public CosmosConfig Cosmos { get; init; } = new();
    public StorageAccountConfig StorageAccount { get; init; } = new();
}

public class CosmosConfig
{
    public string ConnectionString { get; init; } = "";
    public string Database { get; init; } = "";
    public string Container { get; init; } = "";
}

public class StorageAccountConfig
{
    public string ConnectionString { get; init; } = "";
}
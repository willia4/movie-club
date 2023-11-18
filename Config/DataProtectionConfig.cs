namespace zinfandel_movie_club.Config;

public class DataProtectionConfig
{
    public string StorageAccountContainer { get; init; } = "";
    public string StorageAccountBlob { get; init; } = "";
    public string KeyVaultKeyUri { get; init; } = "";
}
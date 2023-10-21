using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface IUserProfileDatabase
{
    public IAsyncEnumerable<UserProfile> GetUserProfilesAsync(CancellationToken cancellationToken);
    public Task<UserProfile?> GetUserProfileByIdAsync(string id, CancellationToken cancellationToken);
    public Task DeleteUserProfile(string id, CancellationToken cancellationToken);
    public Task<UserProfile> UpsertUserProfileForGraphUser(IGraphUser user, string role, CancellationToken cancellationToken);
    public Task<UserProfile> UpsertUserProfileForPrototype(UserProfile prototype, CancellationToken cancellationToken);
    public Task<UserProfile> UpsertUserProfile(string id, string displayName, string role, CancellationToken cancellationToken);
}

public sealed class UserProfileDatabase : IUserProfileDatabase, IDisposable
{
    private readonly CosmosConfig _config;
    private readonly CosmosClient _client;
    private readonly AppSettings _appSettings;

    private string EnvironmentId => _appSettings.EnvironmentId;
    public static string DocumentType => Models.UserProfile._DocumentType;

    public PartitionKey MakePartitionKey(string userId) => new PartitionKeyBuilder().Add(userId).Build();
    public PartitionKey MakePartitionKey(IUserProfile profile) => MakePartitionKey(profile.Id);
    
    public UserProfileDatabase(IOptions<AppSettings> appSettings, CosmosConfig config)
    {
        _config = config;
        _client = new CosmosClient(connectionString: _config.ConnectionString, clientOptions: new CosmosClientOptions()
        {
            SerializerOptions = new CosmosSerializationOptions()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });
        _appSettings = appSettings.Value;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_client.GetContainer(_config.Database, _config.Container));
    }

    public async IAsyncEnumerable<UserProfile> GetUserProfilesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        var query =
            container
                .GetItemLinqQueryable<UserProfile>()
                .Where(p => p.DocumentType == DocumentType && p.EnvironmentId == EnvironmentId)
                .Select(p => p);

        await foreach (var item in query.ToAsyncEnumerable(cancellationToken))
        {
            yield return item;
        }
    }

    public async Task<UserProfile?> GetUserProfileByIdAsync(string id, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        var query =
            container
                .GetItemLinqQueryable<UserProfile>(requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 1
                })
                .Where(p => p.DocumentType == DocumentType && p.EnvironmentId == EnvironmentId && p.Id == id)
                .ToAsyncEnumerable(cancellationToken);

        return await query.FirstOrDefault(cancellationToken);
    }

    public async Task DeleteUserProfile(string id, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        await container.DeleteItemAsync<UserProfile>(id, MakePartitionKey(id), cancellationToken: cancellationToken);
    }

    public Task<UserProfile> UpsertUserProfileForGraphUser(IGraphUser user, string role, CancellationToken cancellationToken)
    {
        return UpsertUserProfile(id: user.NameIdentifier, displayName: user.DisplayName, role: role, cancellationToken);
    }

    public Task<UserProfile> UpsertUserProfileForPrototype(UserProfile prototype, CancellationToken cancellationToken)
    {
        return UpsertUserProfile(id: prototype.Id, displayName: prototype.DisplayName, role: prototype.Role, cancellationToken);
    }

    public async Task<UserProfile> UpsertUserProfile(string id, string displayName, string role, CancellationToken cancellationToken)
    {
        var newRecord = new UserProfile(environmentId: EnvironmentId, documentType: DocumentType, id: id, displayName: displayName, role: role);

        var json = System.Text.Json.JsonSerializer.Serialize(newRecord);
        
        var container = await GetContainerAsync(cancellationToken);
        await container.UpsertItemAsync(newRecord, MakePartitionKey(newRecord), cancellationToken: cancellationToken);
        return newRecord;
    }
}
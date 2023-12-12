using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using zinfandel_movie_club.Authentication;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;
using AzureUser = Microsoft.Graph.Models.User;

public interface IGraphUser
{
    public string NameIdentifier { get; }
    public string DisplayName { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string UserRole { get; }
}

public record GraphUser(string NameIdentifier, string DisplayName, string FirstName, string LastName, string UserRole) : IGraphUser;

public interface IGraphUserManager
{
    IAsyncEnumerable<IGraphUser> GetGraphUsersAsync(CancellationToken cancellationToken);
    Task<IGraphUser?> GetGraphUserAsync(string id, CancellationToken cancellationToken);
    Task SetUserRole(IGraphUser user, string? newRole, CancellationToken cancellationToken);
    Task SetUserDisplayName(IGraphUser user, string? newDisplayName, CancellationToken cancellationToken);
    Task<string?> GetUserRole(string id, CancellationToken cancellationToken);
    Task<IEnumerable<IGraphUser>> GetMembersAsync(CancellationToken cancellationToken);
}

public class GraphUserManager : IGraphUserManager
{
    private readonly Config.GraphApi _graphApiConfig;
    private readonly Config.AppSettings _appSettings;
    private readonly TokenCredential _graphCredential;
    private readonly GraphServiceClient _client;
    private readonly IUserProfileKeyValueStore _profileDataStore;
    private readonly IUserRoleDecorator _userRoleDecorator;
    
    private readonly IMemoryCache _cache;
    private const string MemberListCacheKey = $"{nameof(GraphUserManager)}-members";
    
    public GraphUserManager(IUserProfileKeyValueStore profileDataStore, IOptions<Config.AppSettings> appSettings, IOptions<Config.GraphApi> graphApiConfig, IMemoryCache cache, IUserRoleDecorator userRoleDecorator)
    {
        _profileDataStore = profileDataStore;
        _graphApiConfig = graphApiConfig.Value;
        _appSettings = appSettings.Value;
        _graphCredential = new ClientSecretCredential(tenantId: _graphApiConfig.TenantId, clientId: _graphApiConfig.ClientId, clientSecret: _graphApiConfig.ClientSecret);
        _client = new GraphServiceClient(_graphCredential, scopes: new string[] { "https://graph.microsoft.com/.default" });
        _cache = cache;
        _userRoleDecorator = userRoleDecorator;
    }

    
    private GraphUser GraphUserFromAzureUser(AzureUser azureUser, IUserProfile profileData)
    {
        var userRole = (profileData.Role ?? "").Trim();

        var customDisplayName = (profileData.DisplayName ?? "").Trim();
        var displayName = string.IsNullOrWhiteSpace(customDisplayName) ? azureUser.DisplayName ?? "" : customDisplayName.Trim();

        return new GraphUser(
            NameIdentifier: azureUser.Id ?? "",
            DisplayName: displayName,
            FirstName: azureUser.GivenName ?? "",
            LastName: azureUser.Surname ?? "",
            UserRole: userRole);   
    }
    
    private string[] UserSelectList => new string[] { "displayName", "id", "givenName", "surname" };
    
    private Task<AzureUser?> GetAzureUserAsync(string id, CancellationToken cancellationToken) => _client.Users[id].GetAsync(req => {  req.QueryParameters.Select = UserSelectList; }, cancellationToken: cancellationToken);
    private UserProfileData CustomDefaultProfileDataDocumentForAzureUser(AzureUser azureUser) => new UserProfileData {id = azureUser.Id, DisplayName = azureUser.DisplayName, Role = ""};
    
    public async IAsyncEnumerable<IGraphUser> GetGraphUsersAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queryResponse = await _client.Users.GetAsync(req =>
            {
                req.QueryParameters.Select = UserSelectList;
            },
            cancellationToken: cancellationToken);

        if (queryResponse == null)
        {
            yield break;
        }

        GraphUser? next = null;
        var pageIterator = PageIterator<AzureUser, UserCollectionResponse>.CreatePageIterator(_client, queryResponse, async azureUser =>
        {
            var profileData = await _profileDataStore.GetDocument(azureUser.Id ?? "", () => CustomDefaultProfileDataDocumentForAzureUser(azureUser), cancellationToken);
            next = GraphUserFromAzureUser(azureUser, profileData);
            return false;
        });

        await pageIterator.IterateAsync(cancellationToken);
        while (next != null)
        {
            yield return next;
            next = null;
            
            if (pageIterator.State != PagingState.Complete)
            {
                await pageIterator.ResumeAsync(cancellationToken);
            }
        }
    }

    
    public async Task<IGraphUser?> GetGraphUserAsync(string id, CancellationToken cancellationToken)
    {
        if (await GetAzureUserAsync(id, cancellationToken) is AzureUser azureUser)
        {
            var profileData = await _profileDataStore.GetDocument(azureUser.Id ?? "", () => CustomDefaultProfileDataDocumentForAzureUser(azureUser), cancellationToken);
            return GraphUserFromAzureUser(azureUser, profileData);
        }

        return null;
    }

    public async Task SetUserRole(IGraphUser user, string? newRole, CancellationToken cancellationToken)
    {
        if (!(await GetAzureUserAsync(user.NameIdentifier, cancellationToken) is AzureUser azureUser))
        {
            throw new InvalidOperationException($"Could not find Azure user with id {user.NameIdentifier}");
        }

        var profileData = await _profileDataStore.GetDocument(azureUser.Id ?? "", () => CustomDefaultProfileDataDocumentForAzureUser(azureUser), cancellationToken);
        profileData.Role = (newRole ?? "").Trim();

        _cache.Remove(MemberListCacheKey);
        await _profileDataStore.UpsertDocument(user.NameIdentifier, profileData, cancellationToken);
    }

    public async Task SetUserDisplayName(IGraphUser user, string? newDisplayName, CancellationToken cancellationToken)
    {
        if (!(await GetAzureUserAsync(user.NameIdentifier, cancellationToken) is AzureUser azureUser))
        {
            throw new InvalidOperationException($"Could not find Azure user with id {user.NameIdentifier}");
        }

        var profileData = await _profileDataStore.GetDocument(azureUser.Id ?? "", () => CustomDefaultProfileDataDocumentForAzureUser(azureUser), cancellationToken);
        profileData.DisplayName = (newDisplayName ?? "").Trim();
        
        _cache.Remove(MemberListCacheKey);
        await _profileDataStore.UpsertDocument(user.NameIdentifier, profileData, cancellationToken);
    }

    public async Task<string?> GetUserRole(string id, CancellationToken cancellationToken) =>
        (await GetGraphUserAsync(id, cancellationToken))?.UserRole;

    public async Task<IEnumerable<IGraphUser>> GetMembersAsync(CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(MemberListCacheKey, async (entry) =>
        {
            var members = await
                GetGraphUsersAsync(cancellationToken)
                    .Where(_userRoleDecorator.IsMember, cancellationToken)
                    .ToImmutableList(cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return members!;
        }))!;
    }
}
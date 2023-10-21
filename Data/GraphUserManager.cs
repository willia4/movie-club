using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace zinfandel_movie_club.Data;
using AzureUser = Microsoft.Graph.Models.User;

public interface IGraphUser
{
    public string NameIdentifier { get; }
    public string DisplayName { get; }
    public string FirstName { get; }
    public string LastName { get; }
}

public record GraphUser(string NameIdentifier, string DisplayName, string FirstName, string LastName) : IGraphUser
{
    public static GraphUser FromAzureUser(AzureUser azureUser) => new GraphUser(
        NameIdentifier: azureUser.Id ?? "",
        DisplayName: azureUser.DisplayName ?? "",
        FirstName: azureUser.GivenName ?? "",
        LastName: azureUser.Surname ?? "");
}

public interface IGraphUserManager
{
    IAsyncEnumerable<IGraphUser> GetGraphUsersAsync(CancellationToken cancellationToken);
    Task<IGraphUser?> GetGraphUserAsync(string id, CancellationToken cancellationToken);
}

public class GraphUserManager : IGraphUserManager
{
    private readonly Config.GraphApi _graphApiConfig;
    private readonly TokenCredential _graphCredential;
    private readonly GraphServiceClient _client;
    
    public GraphUserManager(IOptions<Config.GraphApi> graphApiConfig)
    {
        _graphApiConfig = graphApiConfig.Value;
        _graphCredential = new ClientSecretCredential(tenantId: _graphApiConfig.TenantId, clientId: _graphApiConfig.ClientId, clientSecret: _graphApiConfig.ClientSecret);
        _client = new GraphServiceClient(_graphCredential, scopes: new string[] { "https://graph.microsoft.com/.default" });
    }
    
    private string[] UserSelectList => new string[] { "displayName", "id", "givenName", "surname" };
    
    private Task<AzureUser?> GetAzureUserAsync(string id, CancellationToken cancellationToken) => _client.Users[id].GetAsync(req => {  req.QueryParameters.Select = UserSelectList; }, cancellationToken: cancellationToken);
    
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
        var pageIterator = PageIterator<AzureUser, UserCollectionResponse>.CreatePageIterator(_client, queryResponse, azureUser =>
        {
            next = GraphUser.FromAzureUser(azureUser);
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
            return GraphUser.FromAzureUser(azureUser);
        }

        return null;
    }
}
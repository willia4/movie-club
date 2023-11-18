using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface DefaultingKeyVaultStore<KeyT, ValueT>
{
    public ValueT DefaultDocument(KeyT id);
    public Task<ValueT> GetDocument(KeyT id, CancellationToken cancellationToken);
    public Task<ValueT> GetDocument(KeyT id, Func<ValueT> defaultDocumentCreator, CancellationToken cancellationToken);
    public IAsyncEnumerable<(KeyT?, ValueT)> GetDocumentsAndKeys(CancellationToken cancellationToken);
    public IAsyncEnumerable<ValueT> GetDocuments(CancellationToken cancellationToken);
    public Task<ValueT> UpsertDocument(KeyT id, ValueT newDocument, CancellationToken cancellationToken);
    public Task DeleteDocument(KeyT id, CancellationToken cancellationToken);
}

public interface IUserProfileKeyValueStore : DefaultingKeyVaultStore<string, UserProfileData> {}
public sealed class UserProfileKeyValueStore : DefaultingCosmosKeyValueStore<UserProfileData>, IUserProfileKeyValueStore
{
    public UserProfileKeyValueStore(IOptions<AppSettings> appSettings, CosmosConfig cosmosConfig) 
        : base(
            connectionString: cosmosConfig.ConnectionString,
            database: cosmosConfig.Database,
            container: cosmosConfig.Container,
            documentType: UserProfileData._DocumentType,
            partitionKeyMaker: id => new PartitionKey(id),
            defaultDocumentGenerator: id => new UserProfileData { id = id, DisplayName = "", Role = "" })
    {
        
    }
}

public class DefaultingCosmosKeyValueStore<DocumentT> : DefaultingKeyVaultStore<string, DocumentT> where DocumentT : CosmosDocument
{
    private readonly string _connectionString;
    private readonly string _database;
    private readonly string _container;
    private readonly string _documentType;
    
    private readonly Func<string, PartitionKey> _partitionKeyMaker;
    private readonly Func<string, DocumentT> _defaultDocumentGenerator;
    private readonly IPurgeableMemoryCache<string, DocumentT> _cache;

    private readonly CosmosClient _client;

    public bool WriteDefaultDocumentsBack { get; init; } = true;
    
    public DefaultingCosmosKeyValueStore(string connectionString, string database, string container, string documentType, Func<string, PartitionKey> partitionKeyMaker, Func<string, DocumentT> defaultDocumentGenerator)
    {
        _connectionString = connectionString;
        _database = database;
        _container = container;

        _documentType = documentType;
        
        _defaultDocumentGenerator = defaultDocumentGenerator;
        _partitionKeyMaker = partitionKeyMaker;
        
        _cache = new PurgeableMemoryCache<string, DocumentT>();
        
        _client = new CosmosClient(connectionString: _connectionString, clientOptions: new CosmosClientOptions() { Serializer = new CosmosSystemTextJsonSerializer() });
    }

    private string IdPrefix => $"{_documentType}-";
    private string PrefixId(string id) => id.StartsWith(IdPrefix) ? id : $"{IdPrefix}{id}";
    private string CleanId(string prefixedId) => prefixedId.StartsWith(IdPrefix) ? prefixedId.Substring(IdPrefix.Length) : prefixedId;

    private DocumentT? PrefixId(DocumentT? doc)
    {
        if (doc == null) return doc;
        doc.id = PrefixId(doc.id ?? "");
        return doc;
    }

    private DocumentT? CleanId(DocumentT? doc)
    {
        if (doc == null) return doc;
        doc.id = CleanId(doc.id ?? "");
        return doc;
    }
    
    public DocumentT DefaultDocument(string id) => _defaultDocumentGenerator(id);

    private Database? _cachedDatabase = null;
    private async Task<Database> GetDatabase(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cachedDatabase == null)
        {
            var res = await _client.GetDatabase(_database).ReadAsync(cancellationToken: cancellationToken);
            _cachedDatabase = res?.Database ?? throw new InvalidOperationException($"Could not open Cosmos database {_database}");
        }

        return _cachedDatabase;
    }

    private Container? _cachedContainer = null;

    private async Task<Container> GetContainer(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cachedContainer == null)
        {
            var db = await GetDatabase(cancellationToken);
            var res = await db.GetContainer(_container).ReadContainerAsync(cancellationToken: cancellationToken);
            _cachedContainer = res?.Container ?? throw new InvalidOperationException($"Could not open Cosmos collection {_container} on database {db.Id}");
        }

        return _cachedContainer;
    }
    
    private async Task<DocumentT?> GetDocumentFromCosmosIfExists(string id, CancellationToken cancellationToken)
    {
        var container = await GetContainer(cancellationToken);
        var query = container.GetItemLinqQueryable<DocumentT>()
            .Where(doc => doc.id == PrefixId(id) && doc.DocumentType == _documentType)
            .ToQueryDefinition();

        var iterator = container.GetItemQueryIterator<DocumentT>(query, requestOptions: new QueryRequestOptions() { MaxItemCount = 1});
        DocumentT? item = default;
        
        while (iterator.HasMoreResults)
        {
            var results = await iterator.ReadNextAsync(cancellationToken);
            item = results.FirstOrDefault();
        }

        return CleanId(item);
    }
    
    public async Task<DocumentT> GetDocument(string id, CancellationToken cancellationToken)
    {
        DocumentT DefaultDocumentCreator() => _defaultDocumentGenerator(id);
        return await GetDocument(id, DefaultDocumentCreator, cancellationToken);
    }

    public async Task<DocumentT> GetDocument(string id, Func<DocumentT> defaultDocumentCreator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _cache.GetOrCreateAsync(id, async (id_, cancellationToken) =>
        {
            var doc = (await GetDocumentFromCosmosIfExists(id, cancellationToken)) switch
            {
                DocumentT existing => existing,
                null when WriteDefaultDocumentsBack => await UpsertDocument(id, defaultDocumentCreator(), cancellationToken),
                null => _defaultDocumentGenerator(id)
            };
            return (null, doc);
        }, cancellationToken);
    }

    public async IAsyncEnumerable<(string?, DocumentT)> GetDocumentsAndKeys([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var doc in GetDocuments(cancellationToken))
        {
            CleanId(doc);
            yield return (doc.id, doc);
        }
    }

    public async IAsyncEnumerable<DocumentT> GetDocuments([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seenIds = new HashSet<string>();
        var container = await GetContainer(cancellationToken);

        var q = container.GetItemLinqQueryable<DocumentT>();
        var iterator = q.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                CleanId(item);
                
                if (item != null)
                {
                    seenIds.Add(item.id ?? "");
                    yield return item;
                }
            }
        }

        foreach (var item in _cache.Where(kvp => !seenIds.Contains(kvp.Key)))
        {
            yield return item.Value;
        }
    }

    public async Task<DocumentT> UpsertDocument(string id, DocumentT newDocument, CancellationToken cancellationToken)
    {
        var container = await GetContainer(cancellationToken);
        newDocument.id = PrefixId(id);
        var res = await container.UpsertItemAsync(newDocument, _partitionKeyMaker(newDocument.id), cancellationToken: cancellationToken);
        if (res?.Resource is DocumentT updated)
        {
            return _cache.Set(id, null, res!.Resource!);            
        }

        return newDocument;
    }

    public async Task DeleteDocument(string id, CancellationToken cancellationToken)
    {
        var container = await GetContainer(cancellationToken);
        var _ = await container.DeleteItemAsync<DocumentT>(PrefixId(id), _partitionKeyMaker(id), cancellationToken: cancellationToken);
        _cache.Remove(id);
    }
}
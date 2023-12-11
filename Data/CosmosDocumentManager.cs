using Microsoft.Azure.Cosmos;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface ICosmosDocumentManager<DocumentT>
{
    public Task<Result<DocumentT, Exception>> UpsertDocument(DocumentT doc, CancellationToken cancellationToken);
}

public class CosmosDocumentManager<DocumentT> : ICosmosDocumentManager<DocumentT> where DocumentT : CosmosDocument
{
    private readonly string _connectionString;
    private readonly string _database;
    private readonly string _container;
    private readonly string _documentType;
    private readonly Func<string, PartitionKey> _partitionKeyMaker;
    
    private readonly CosmosClient _client;
    
    public CosmosDocumentManager(string connectionString, string database, string container, string documentType, Func<string, PartitionKey> partitionKeyMaker)
    {
        _connectionString = connectionString;
        _database = database;
        _container = container;
        _documentType = documentType;
        _partitionKeyMaker = partitionKeyMaker;

        _client = new CosmosClient(connectionString: _connectionString, clientOptions: new CosmosClientOptions() { Serializer = new CosmosSystemTextJsonSerializer() });
    }
    
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
    
    public async Task<Result<DocumentT, Exception>> UpsertDocument(DocumentT doc, CancellationToken cancellationToken)
    {
        if (doc.id is null || string.IsNullOrWhiteSpace(doc.id))
        {
            throw new System.ArgumentException($"{nameof(doc)} cannot be null or empty", nameof(doc));
        }

        try
        {
            var container = await GetContainer(cancellationToken);
            var res = await container.UpsertItemAsync(doc, _partitionKeyMaker(doc.id), cancellationToken: cancellationToken);
            if (res?.Resource is { } updated)
            {
                return Result<DocumentT, System.Exception>.Ok(updated);
            }

            return Result<DocumentT, Exception>.Error($"Did not get a valid response from Cosmos: {res?.StatusCode}".ToException());
        }
        catch (Exception outer)
        {
            return Result<DocumentT, Exception>.Error(outer);
        }
    }
}
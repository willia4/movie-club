using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data.Models;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club.Data;

public interface ICosmosDocumentManager<DocumentT>
{
    public Task<Container> InitializeContainer(CancellationToken cancellationToken);
    public Task<Result<DocumentT, Exception>> UpsertDocument(DocumentT doc, CancellationToken cancellationToken);
    public Task<Result<DocumentT, Exception>> GetDocumentById(string id, CancellationToken cancellationToken);
    public Task<QueryDefinition> MakeQuery(Func<IQueryable<DocumentT>, IQueryable<DocumentT>> f, CancellationToken cancellationToken);

    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(CancellationToken cancellationToken);
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(Func<IQueryable<DocumentT>, IQueryable<DocumentT>> queryFactory, CancellationToken cancellationToken);
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(QueryDefinition query, CancellationToken cancellationToken);
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(int maxItems, CancellationToken cancellationToken);
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(int maxItems, Func<IQueryable<DocumentT>, IQueryable<DocumentT>> queryFactory, CancellationToken cancellationToken);
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(int maxItems, QueryDefinition query, CancellationToken cancellationToken);
    public Task<Result<ImmutableList<DocumentT>, Exception>> GetAllDocuments(CancellationToken cancellationToken);
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
    private Container? _cachedContainer = null;
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
    private async Task<Container> GetContainer(CancellationToken cancellationToken)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedContainer == null)
            {
                if (_cachedDatabase == null)
                {
                    var databaseRes = await _client.GetDatabase(_database).ReadAsync(cancellationToken: cancellationToken);
                    _cachedDatabase = databaseRes?.Database ?? throw new InvalidOperationException($"Could not open Cosmos database {_database}");
                }

                var containerRes = await _cachedDatabase.GetContainer(_container).ReadContainerAsync(
                    requestOptions: new ContainerRequestOptions()
                    {
                        PopulateQuotaInfo = false
                    },
                    cancellationToken: cancellationToken);
                _cachedContainer = containerRes?.Container ?? throw new InvalidOperationException($"Could not open Cosmos collection {_container} on database {_cachedDatabase.Id}");
            }
            
            return _cachedContainer;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public Task<Container> InitializeContainer(CancellationToken cancellationToken) => GetContainer(cancellationToken);
    
    public async Task<Result<DocumentT, Exception>> UpsertDocument(DocumentT doc, CancellationToken cancellationToken)
    {
        if (doc.id is null || string.IsNullOrWhiteSpace(doc.id))
        {
            throw new System.ArgumentException($"{nameof(doc)}.id cannot be null or empty", nameof(doc));
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

    public async Task<Result<DocumentT, Exception>> GetDocumentById(string id, CancellationToken cancellationToken)
    {
        if (id is null || string.IsNullOrWhiteSpace(id))
        {
            throw new System.ArgumentException($"{nameof(id)} cannot be null or empty", nameof(id));
        }

        try
        {
            var container = await GetContainer(cancellationToken);
            var r = await container.ReadItemAsync<DocumentT>(id, _partitionKeyMaker(id), cancellationToken: cancellationToken);

            return r?.Resource switch
            {
                null => Result<DocumentT, Exception>.Error(new NotFoundException()),
                var value => Result<DocumentT, Exception>.Ok(value)
            };
        }
        catch (CosmosException notFound) when (notFound.StatusCode == HttpStatusCode.NotFound)
        {
            return Result<DocumentT, Exception>.Error(new NotFoundException($"Could not find document with id \"{id}\"", notFound));
        }
        catch (Exception outer)
        {
            return Result<DocumentT, Exception>.Error(outer);
        }
    }

    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(
            maxItems: -1,
            queryDefinitionOrFactory: null,
            cancellationToken: cancellationToken);
    }

    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(Func<IQueryable<DocumentT>, IQueryable<DocumentT>> queryFactory, CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(
            maxItems: -1,
            queryDefinitionOrFactory: Either<QueryDefinition, Func<IQueryable<DocumentT>, IQueryable<DocumentT>>>.OfRight(queryFactory),
            cancellationToken: cancellationToken);
    }
    
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(QueryDefinition query, CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(
            maxItems: -1,
            queryDefinitionOrFactory: Either<QueryDefinition, Func<IQueryable<DocumentT>, IQueryable<DocumentT>>>.OfLeft(query),
            cancellationToken: cancellationToken);
    }
    
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(int maxItems, CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(
            maxItems: maxItems,
            queryDefinitionOrFactory: null,
            cancellationToken: cancellationToken);
    }

    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(int maxItems, Func<IQueryable<DocumentT>, IQueryable<DocumentT>> queryFactory, CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(
            maxItems: maxItems,
            queryDefinitionOrFactory: Either<QueryDefinition, Func<IQueryable<DocumentT>, IQueryable<DocumentT>>>.OfRight(queryFactory),
            cancellationToken: cancellationToken);
    }
    
    public Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocuments(int maxItems, QueryDefinition query, CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(
            maxItems: maxItems,
            queryDefinitionOrFactory: Either<QueryDefinition, Func<IQueryable<DocumentT>, IQueryable<DocumentT>>>.OfLeft(query),
            cancellationToken: cancellationToken);
    }

    private async Task<Result<ImmutableList<DocumentT>, Exception>> QueryDocumentsInternal(int maxItems, 
        Either<QueryDefinition, Func<IQueryable<DocumentT>, IQueryable<DocumentT>>>? queryDefinitionOrFactory, CancellationToken cancellationToken)
    {
        try
        {
            var container = await GetContainer(cancellationToken);
            var query = 
                queryDefinitionOrFactory?.Match(
                    (qd) => qd,
                    f => f(container.GetItemLinqQueryable<DocumentT>().Where(d => d.DocumentType == _documentType)).ToQueryDefinition())
                ?? container.GetItemLinqQueryable<DocumentT>().Where(d => d.DocumentType == _documentType).ToQueryDefinition();

            var iterator = container.GetItemQueryIterator<DocumentT>(query, requestOptions: new QueryRequestOptions()
            {
                MaxItemCount = maxItems
            });

            ImmutableList<DocumentT> results = ImmutableList<DocumentT>.Empty;
            while (iterator.HasMoreResults)
            {
                var res = await iterator.ReadNextAsync(cancellationToken);
                results = results.AddRange(res);
            }

            return Result<ImmutableList<DocumentT>, Exception>.Ok(results);
        }
        catch (Exception outer)
        {
            return Result<ImmutableList<DocumentT>, Exception>.Error(outer);
        }
    }

    public async Task<QueryDefinition> MakeQuery(Func<IQueryable<DocumentT>, IQueryable<DocumentT>> f, CancellationToken cancellationToken)
    {
        var container = await GetContainer(cancellationToken);
        var query = container.GetItemLinqQueryable<DocumentT>();
        return f(query.Where(d => d.DocumentType == _documentType)).ToQueryDefinition();
    }
    
    public Task<Result<ImmutableList<DocumentT>, Exception>> GetAllDocuments(CancellationToken cancellationToken)
    {
        return QueryDocumentsInternal(-1, null, cancellationToken);
    }
}

public static class CosmosDocumentManagerExtensions
{
    private static bool TryGetDocumentType<T>(out string documentType) where T : CosmosDocument
    {
        try
        {
            documentType = (string)typeof(T)!.GetProperty("_DocumentType", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            return true;
        }
        catch
        {
            documentType = default!;
            return false;
        }
    }

    public static object AddCosmosDocumentManager<T>(this IServiceCollection services) where T : CosmosDocument
    {
        return services.AddCosmosDocumentManager<T>(id => new PartitionKey(id));
    }

    public static object AddCosmosDocumentManager<T>(this IServiceCollection services, Func<string, PartitionKey> partitionKeyMaker) where T : CosmosDocument
    {
        return services.AddSingleton<ICosmosDocumentManager<T>>(sp =>
        {
            var config = sp.GetRequiredService<CosmosConfig>();
            if (!TryGetDocumentType<T>(out var documentType))
            {
                throw new InvalidOperationException($"Could not get document type for type {typeof(T).Name}");
            }

            return new CosmosDocumentManager<T>(
                connectionString: config.ConnectionString,
                database: config.Database,
                container: config.Container,
                documentType: documentType,
                partitionKeyMaker: partitionKeyMaker);
        });
    }
}
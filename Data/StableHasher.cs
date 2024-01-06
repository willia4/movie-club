using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using zinfandel_movie_club.Config;

namespace zinfandel_movie_club.Data;

public interface IShuffleHasher
{
    public Task<int> HashValues(CancellationToken cancellationToken, params dynamic[] vs);
    public Task<int> GetCurrentShuffleValue(CancellationToken cancellationToken);
    public Task<int> IncrementCurrentShuffleValue(CancellationToken cancellationToken);
    public Task ReloadShuffleValue(CancellationToken cancellationToken);
    public Task ResetShuffleValue(CancellationToken cancellationToken);
}

//based on ideas from https://stackoverflow.com/a/263416

public class StableHasher
{
    private readonly int _hash;
    public StableHasher()
    {
        unchecked
        {
            _hash = (int)2166136261;
        }
    }

    private StableHasher(int newHash)
    {
        _hash = newHash;
    }
    
    public StableHasher MixIn(int v)
    {
        unchecked
        {
            return new StableHasher((_hash * 16777619) ^ v);
        }
    }

    public StableHasher MixIn(char c) => MixIn((int)c);

    public StableHasher MixIn(string s)
    {
        var r = this.MixIn(s.Length);
        foreach (var c in s)
        {
            r = MixIn(c);
        }

        return r;
    }
    
    public StableHasher MixIn(dynamic v)
    {
        return v switch
        {
            string s => MixIn(s),
            int i => MixIn(i),
            char c => MixIn(c),
            dynamic[] vs => vs.Aggregate(MixIn(vs.Length), (acc, v_) => acc.MixIn(v_)),
            _ => throw new InvalidOperationException($"Could not hash dynamic object {v.GetType()}")
        };
    }

    public StableHasher MixIn(params dynamic[] vs)
    {
        var r = MixIn(vs.Length);
        foreach (var v in vs)
        {
            r = r.MixIn(v);
        }
        return r;
    }
    
    public int Hash => _hash;

    public static explicit operator int(StableHasher h) => h._hash;
}


public class ShuffleHasher : IShuffleHasher
{
    private readonly BlobContainerClient _client;
    
    private readonly SemaphoreSlim _shuffleValueLock = new SemaphoreSlim(1, 1);
    private int? _shuffleValue = null;
    
    public ShuffleHasher(IOptions<DatabaseConfig> databaseConfig)
    {
        _client = new BlobContainerClient(
            connectionString: databaseConfig.Value.StorageAccount.ConnectionString,
            blobContainerName: databaseConfig.Value.StorageAccount.SettingsContainer);
    }

    public async Task<int> HashValues(CancellationToken cancellationToken, params dynamic[] vs)
    {
        var shuffleValue = await GetCurrentShuffleValue(cancellationToken);
        return (int)new StableHasher().MixIn(shuffleValue).MixIn(vs);
    }

    private BlobClient BlobClient() => _client.GetBlobClient("picker_shuffle.txt");
    public async Task<int> GetCurrentShuffleValue(CancellationToken cancellationToken)
    {
        await _shuffleValueLock.WaitAsync(cancellationToken);
        try
        {
            if (_shuffleValue.HasValue)
            {
                return _shuffleValue.Value;
            }

            var blobClient = BlobClient();
            try
            {
                var r = await blobClient.DownloadContentAsync(cancellationToken);
                if (r.HasValue)
                {
                    var stringValue = r.Value.Content.ToString();
                    if (int.TryParse(stringValue, out var v))
                    {
                        _shuffleValue = v;
                        return v;
                    }
                }

                _shuffleValue = 0;
                return 0;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _shuffleValue = 0;
                return 0;
            }
        }
        finally
        {
            _shuffleValueLock.Release();
        }
    }

    public async Task<int> IncrementCurrentShuffleValue(CancellationToken cancellationToken)
    {
        var currentValue = await GetCurrentShuffleValue(cancellationToken);
        currentValue = currentValue >= Int16.MaxValue ? 0 : currentValue + 1;

        await _shuffleValueLock.WaitAsync(cancellationToken);
        try
        {
            var blobClient = BlobClient();
            var newContent = new BinaryData($"{currentValue}");
            await blobClient.UploadAsync(newContent, overwrite:true, cancellationToken: cancellationToken);
            
            _shuffleValue = currentValue;
            return currentValue;
        }
        finally
        {
            _shuffleValueLock.Release();
        }
    }

    public async Task ReloadShuffleValue(CancellationToken cancellationToken)
    {
        await _shuffleValueLock.WaitAsync(cancellationToken);
        try
        {
            _shuffleValue = null;
        }
        finally
        {
            _shuffleValueLock.Release();
        }

        await GetCurrentShuffleValue(cancellationToken);
    }

    public async Task ResetShuffleValue(CancellationToken cancellationToken)
    {
        await _shuffleValueLock.WaitAsync(cancellationToken);
        try
        {
            _shuffleValue = null;

            var blobClient = BlobClient();
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _shuffleValueLock.Release();
        }
    }
}
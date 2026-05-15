using Microsoft.Azure.Cosmos;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.CloudSync.Storage;

/// <summary>
/// Azure Cosmos DB-backed <see cref="IPaycheckRepository"/>.
/// Partition key is the sync token (a per-device UUID); lazy-initializes
/// the Cosmos client on first use.
/// </summary>
public sealed class CosmosPaycheckRepository : IPaycheckRepository, IAsyncDisposable
{
    private readonly CloudSyncOptions _options;
    private readonly ISyncTokenProvider _tokenProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CosmosClient? _client;
    private Container? _container;

    public CosmosPaycheckRepository(CloudSyncOptions options, ISyncTokenProvider tokenProvider)
    {
        _options = options;
        _tokenProvider = tokenProvider;
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        var (container, token) = await EnsureReadyAsync();
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.syncToken = @token")
            .WithParameter("@token", token);

        var results = new List<SavedPaycheck>();
        using var feed = container.GetItemQueryIterator<CosmosDocument<SavedPaycheck>>(query);
        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            foreach (var doc in page)
                results.Add(doc.Payload);
        }
        return results.AsReadOnly();
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        var (container, token) = await EnsureReadyAsync();
        try
        {
            var response = await container.ReadItemAsync<CosmosDocument<SavedPaycheck>>(
                id.ToString(), new PartitionKey(token));
            return response.Resource.Payload;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        var (container, token) = await EnsureReadyAsync();
        var doc = new CosmosDocument<SavedPaycheck>
        {
            Id = paycheck.Id.ToString(),
            SyncToken = token,
            Payload = paycheck
        };
        await container.UpsertItemAsync(doc, new PartitionKey(token));
    }

    public async Task DeleteAsync(Guid id)
    {
        var (container, token) = await EnsureReadyAsync();
        try
        {
            await container.DeleteItemAsync<CosmosDocument<SavedPaycheck>>(
                id.ToString(), new PartitionKey(token));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — treat as success.
        }
    }

    private async Task<(Container container, string token)> EnsureReadyAsync()
    {
        if (_container is not null)
        {
            var tok = await _tokenProvider.GetOrCreateTokenAsync();
            return (_container, tok);
        }

        await _lock.WaitAsync();
        try
        {
            if (_container is null)
            {
                _client = new CosmosClient(
                    _options.ConnectionString,
                    new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        }
                    });
                var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(_options.DatabaseId);
                var containerResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties(_options.ContainerId, partitionKeyPath: "/syncToken"));
                _container = containerResponse.Container;
            }
        }
        finally
        {
            _lock.Release();
        }

        var token = await _tokenProvider.GetOrCreateTokenAsync();
        return (_container!, token);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _lock.Dispose();
        await ValueTask.CompletedTask;
    }
}

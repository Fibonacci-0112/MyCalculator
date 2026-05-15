using System.Text.Json.Serialization;

namespace PaycheckCalc.CloudSync.Storage;

/// <summary>
/// Cosmos DB envelope. The "id" and "syncToken" fields are infrastructure;
/// the domain object lives in <see cref="Payload"/> so Core models are not
/// polluted with Cosmos-specific properties.
/// </summary>
internal sealed class CosmosDocument<T>
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("syncToken")]
    public string SyncToken { get; set; } = "";

    [JsonPropertyName("payload")]
    public T Payload { get; set; } = default!;
}

namespace Fixon.Infrastructure.Persistence;

/// <summary>
/// Stores idempotent responses for write endpoints (per-tenant).
/// </summary>
public sealed class IdempotencyRequest
{
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Endpoint { get; private set; } = null!;

    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyRequest() { } // EF

    public IdempotencyRequest(
        Guid tenantId,
        string key,
        string endpoint,
        int responseStatusCode,
        string responseBody,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(responseBody)) throw new ArgumentException("ResponseBody is required.", nameof(responseBody));

        TenantId = tenantId;
        Key = key;
        Endpoint = endpoint;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        CreatedAt = createdAt;
    }
}


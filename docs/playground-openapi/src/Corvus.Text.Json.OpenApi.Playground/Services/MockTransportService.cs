namespace Corvus.Text.Json.OpenApi.Playground.Services;

/// <summary>
/// Provides a mock HTTP transport for running generated OpenAPI clients
/// in the browser without a real backend.
/// Returns example responses or 200 OK with empty bodies.
/// </summary>
public class MockTransportService
{
    private readonly List<HttpExchange> exchanges = [];

    public IReadOnlyList<HttpExchange> Exchanges => this.exchanges;

    public void Clear() => this.exchanges.Clear();

    public void RecordExchange(string method, string path, int statusCode, string? responseBody)
    {
        this.exchanges.Add(new HttpExchange(method, path, statusCode, responseBody));
    }
}

public record HttpExchange(string Method, string Path, int StatusCode, string? ResponseBody);
using System.Text.Json;
using Corvus.Json;

namespace ComplianceGenerator;

// Mirrors the C# test harness's FakeWebDocumentResolver (Common/tests/TestUtilities): serves
// http://localhost:1234/<path> from a local remotes directory so the JSON-Schema-Test-Suite's remote
// $ref/$dynamicRef cases resolve offline. Self-contained copy (identical to the TS compliance harness's).
internal sealed class FakeWebDocumentResolver : IDocumentResolver
{
    private readonly string baseDirectory;
    private readonly Dictionary<string, JsonDocument> documents = [];
    private bool disposed;

    public FakeWebDocumentResolver(string baseDirectory) => this.baseDirectory = baseDirectory;

    public bool AddDocument(string uri, JsonDocument document) => this.documents.TryAdd(uri, document);

    public void Reset() => this.DisposeDocuments();

    public void Dispose()
    {
        this.DisposeDocuments();
        this.disposed = true;
    }

    public async ValueTask<JsonElement?> TryResolve(JsonReference reference)
    {
        if (this.disposed || !IsMatchForFakeUri(reference))
        {
            return null;
        }

        string path = this.GetPath(reference);
        if (this.documents.TryGetValue(path, out JsonDocument? cached))
        {
            return JsonPointerUtilities.TryResolvePointer(cached, reference.Fragment, out JsonElement? element) ? element : default;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            JsonDocument result = await JsonDocument.ParseAsync(stream);
            this.documents[path] = result;
            return JsonPointerUtilities.TryResolvePointer(result, reference.Fragment, out JsonElement? element) ? element : default;
        }
        catch (Exception)
        {
            return default;
        }

        static bool IsMatchForFakeUri(JsonReference reference)
        {
            JsonReferenceBuilder builder = reference.AsBuilder();
            return builder.Host.SequenceEqual("localhost".AsSpan()) && builder.Port.SequenceEqual("1234".AsSpan());
        }
    }

    private string GetPath(JsonReference reference)
    {
        JsonReferenceBuilder builder = reference.AsBuilder();
        string rel = builder.Path.ToString();
        return Path.Combine(this.baseDirectory, rel.Length > 0 && rel[0] == '/' ? rel[1..] : rel);
    }

    private void DisposeDocuments()
    {
        foreach (KeyValuePair<string, JsonDocument> d in this.documents)
        {
            d.Value.Dispose();
        }

        this.documents.Clear();
    }
}
using System.Text.Json.Nodes;
using Corvus.Json;
using Corvus.Json.UriTemplates;

string uriTemplate = "http://example.org/location{?value*}";
JsonAny jsonValues = JsonAny.FromProperties(("foo", "bar"), ("bar", "baz"), ("baz", "bob")).AsJsonElementBackedValue();
ReadOnlySpan<char> span = uriTemplate.AsSpan();

for (int i = 0; i < 10000; i++)
{
    UriTemplateResolver.TryResolveResult(span, false, jsonValues, HandleResult);
}

static void HandleResult(ReadOnlySpan<char> resolvedTemplate)
{
    // NOP
}
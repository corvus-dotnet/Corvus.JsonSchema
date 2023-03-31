using Corvus.Json;
using Corvus.Json.Benchmarking.Models;
using Corvus.Json.UriTemplates;
using NodaTime;

string uriTemplate = "http://example.org/location{?value*}";
JsonAny jsonValues = JsonAny.FromProperties(("foo", "bar"), ("bar", "baz"), ("baz", "bob")).AsJsonElementBackedValue();
ReadOnlySpan<char> span = uriTemplate.AsSpan();

for (int i = 0; i < 10000; i++)
{
    object? nullState = default;
    JsonUriTemplateResolver.TryResolveResult(span, false, jsonValues, HandleResult, ref nullState);
}

static void HandleResult(ReadOnlySpan<char> resolvedTemplate, ref object? state)
{
    // NOP
}

var p =
    Person.Create(
        name: PersonName.Create("Adams", "Matthew"),
        dateOfBirth: new LocalDate(1973, 02, 14));

JsonNumber first = new(3);
JsonInteger second = new(long.MaxValue);
Console.WriteLine((JsonInteger)first < second);
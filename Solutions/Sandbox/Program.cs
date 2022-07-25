using System.Text.Json;
using Corvus.Json;
using Corvus.Json.JsonSchema.Draft7;

using var documentResolver =
    new CompoundDocumentResolver(
        new FileSystemDocumentResolver(),
        new HttpClientDocumentResolver(new HttpClient()));

JsonElement? element = await documentResolver.TryResolve(new JsonReference("https://json-schema.org/draft-07/schema")).ConfigureAwait(false);
if (element is JsonElement schemaElement)
{
    var schema = Schema.FromJson(schemaElement);
}
else
{
    Console.Error.WriteLine("Unable to find the Draft7 metaschema.");
}
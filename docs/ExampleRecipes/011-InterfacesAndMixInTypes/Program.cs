using Corvus.Text.Json;
using InterfacesAndMixInTypes.Models;

// Parse a composite type from JSON
string compositeJson =
    """
    {
      "budget": 123.7,
      "count": 4,
      "title": "Greeting",
      "description": "Hello world"
    }
    """;

using var parsedComposite = ParsedJsonDocument<CompositeType>.Parse(compositeJson);
CompositeType composite = parsedComposite.RootElement;

Console.WriteLine("Composite type:");
Console.WriteLine(composite);
Console.WriteLine();

// Implicit conversion to each of the composed types (allOf)
// This is similar to implementing multiple interfaces
Documentation documentation = composite;
Countable countable = composite;

Console.WriteLine($"Title: {documentation.Title}");
// Description is an optional property on Documentation - work with the generated type
if (!documentation.Description.IsUndefined())
{
    Console.WriteLine($"Description: {documentation.Description}");
}

Console.WriteLine($"Count: {countable.Count}");
Console.WriteLine($"Budget: {composite.Budget}");
Console.WriteLine();

// Build a composite by applying its constituent parts
// This is useful when you have Countable and Documentation instances
// from different sources and want to compose them into a CompositeType
using var parsedCountable = ParsedJsonDocument<Countable>.Parse("""{"count": 10}""");
using var parsedDocumentation = ParsedJsonDocument<Documentation>.Parse(
    """{"title": "Composed", "description": "Built from parts"}""");

using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = CompositeType.CreateBuilder(workspace);
CompositeType.Mutable mutableComposite = builder.RootElement;

// Apply each constituent - merges its properties into the composite
mutableComposite.Apply(parsedCountable.RootElement);
mutableComposite.Apply(parsedDocumentation.RootElement);

// Set the composite's own property
mutableComposite.SetBudget(456.78m);

Console.WriteLine("Composed from parts:");
Console.WriteLine(mutableComposite);
// Output: {"count":10,"title":"Composed","description":"Built from parts","budget":456.78}
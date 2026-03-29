using Corvus.Text.Json;
using MappingInputAndOutputValues.Models;

// Parse source data
string sourceJson = """
    {
      "id": 123,
      "name": "John Doe"
    }
    """;

using var parsedSource = ParsedJsonDocument<SourceType>.Parse(sourceJson);
SourceType source = parsedSource.RootElement;
Console.WriteLine("Source data:");
Console.WriteLine($"  id: {source.Id}");
Console.WriteLine($"  name: {source.Name}");
Console.WriteLine();

// Transform source to target using builder
Console.WriteLine("Transforming source to target...");
using JsonWorkspace workspace = JsonWorkspace.Create();

// Create target using CreateBuilder convenience overload with named parameters
// Property entities are compatible - pass values directly (zero-allocation view)
using var targetBuilder = TargetType.CreateBuilder(
    workspace,
    fullName: source.Name,
    identifier: source.Id);

TargetType target = targetBuilder.RootElement;
Console.WriteLine("Target data:");
Console.WriteLine($"  identifier: {target.Identifier}");
Console.WriteLine($"  fullName: {target.FullName}");
Console.WriteLine();
Console.WriteLine("Target JSON:");
Console.WriteLine(target);
Console.WriteLine();

// Demonstrate reverse transformation (target -> source)
Console.WriteLine("Reverse transformation (target -> source)...");

// Property entities are compatible in both directions
using var sourceBuilder = SourceType.CreateBuilder(
    workspace,
    id: target.Identifier,
    name: target.FullName);

SourceType reversedSource = sourceBuilder.RootElement;
Console.WriteLine("Reversed source JSON:");
Console.WriteLine(reversedSource);
Console.WriteLine();

// Demonstrate mapping to CRM type (constrained properties require From())
Console.WriteLine("Mapping source to CRM type (using From())...");

// CRM properties have additional constraints (minimum, minLength, maxLength),
// so the entity types are NOT reduced to JsonInteger/JsonString.
// We must use From() to convert between compatible but distinct entity types.
using var crmBuilder = CrmType.CreateBuilder(
    workspace,
    customerId: CrmType.CustomerIdEntity.From(source.Id),
    displayName: CrmType.DisplayNameEntity.From(source.Name));

CrmType crm = crmBuilder.RootElement;
Console.WriteLine("CRM data:");
Console.WriteLine($"  customerId: {crm.CustomerId}");
Console.WriteLine($"  displayName: {crm.DisplayName}");
Console.WriteLine();
Console.WriteLine("CRM JSON:");
Console.WriteLine(crm);
Console.WriteLine();

// Demonstrate transformation WITH value modification
Console.WriteLine("Transformation with modification...");

// Only extract to primitives when you need to transform the values
if (source.Id.TryGetValue(out long idValue) && source.Name.TryGetValue(out string? nameValue) && nameValue is not null)
{
    using var modifiedBuilder = TargetType.CreateBuilder(
        workspace,
        fullName: nameValue.ToUpperInvariant(),
        identifier: idValue + 1000);

    TargetType modified = modifiedBuilder.RootElement;
    Console.WriteLine("Modified target JSON:");
    Console.WriteLine(modified);
}
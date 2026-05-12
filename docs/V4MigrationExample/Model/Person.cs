using Corvus.Json;

namespace V4MigrationExample.Model;

/// <summary>
/// A person, generated from person.json schema using the V4 source generator.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/person.json")]
public readonly partial struct Person
{
}

using Corvus.Text.Json;

namespace DataObjectValidation.Models;

/// <summary>
/// A person entity with validation constraints.
/// Generated from person-constraints.json.
/// </summary>
[JsonSchemaTypeGenerator("person-constraints.json")]
public readonly partial struct PersonConstraints;
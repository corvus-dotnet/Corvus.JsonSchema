using Corvus.Text.Json;

namespace ConstrainingABaseType.Models;

/// <summary>
/// A closed person type (base type).
/// </summary>
[JsonSchemaTypeGenerator("person-closed.json")]
public readonly partial struct PersonClosed;

/// <summary>
/// A tall person — adds a tighter minimum height constraint via $ref.
/// </summary>
[JsonSchemaTypeGenerator("person-tall.json")]
public readonly partial struct PersonTall;
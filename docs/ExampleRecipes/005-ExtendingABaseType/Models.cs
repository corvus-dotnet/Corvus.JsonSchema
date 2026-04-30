using Corvus.Text.Json;

namespace ExtendingABaseType.Models;

/// <summary>
/// An open person type (base type).
/// </summary>
[JsonSchemaTypeGenerator("person-open.json")]
public readonly partial struct PersonOpen;

/// <summary>
/// A wealthy person extending the base PersonOpen via $ref.
/// </summary>
[JsonSchemaTypeGenerator("person-wealthy.json")]
public readonly partial struct PersonWealthy;
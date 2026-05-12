using Corvus.Text.Json;

namespace OpenVersusClosedTypes.Models;

/// <summary>
/// An open person (allows additional properties).
/// </summary>
[JsonSchemaTypeGenerator("person-open.json")]
public readonly partial struct PersonOpen;

/// <summary>
/// A closed person (unevaluatedProperties: false).
/// </summary>
[JsonSchemaTypeGenerator("person-closed.json")]
public readonly partial struct PersonClosed;
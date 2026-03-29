using Corvus.Text.Json;

namespace CreatingAStronglyTypedArray.Models;

/// <summary>
/// A closed person type (used as the array item type).
/// </summary>
[JsonSchemaTypeGenerator("person-closed.json")]
public readonly partial struct PersonClosed;

/// <summary>
/// A fixed-length array of 30 PersonClosed instances.
/// </summary>
[JsonSchemaTypeGenerator("person-1d-array.json")]
public readonly partial struct Person1dArray;
using Corvus.Text.Json;

namespace ReusingCommonTypes.Models;

/// <summary>
/// A person entity with reusable constrained string types via $ref/$defs.
/// Generated from person-common-schema.json.
/// </summary>
[JsonSchemaTypeGenerator("person-common-schema.json")]
public readonly partial struct PersonCommonSchema;
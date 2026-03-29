using Corvus.Text.Json;

namespace PatternMatching.Models;

[JsonSchemaTypeGenerator("discriminated-union-by-type.json")]
public readonly partial struct DiscriminatedUnionByType;

[JsonSchemaTypeGenerator("person-open.json")]
public readonly partial struct PersonOpen;
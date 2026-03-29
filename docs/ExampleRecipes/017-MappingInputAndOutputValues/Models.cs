using Corvus.Text.Json;

namespace MappingInputAndOutputValues.Models;

[JsonSchemaTypeGenerator("source.json")]
public readonly partial struct SourceType;

[JsonSchemaTypeGenerator("target.json")]
public readonly partial struct TargetType;

[JsonSchemaTypeGenerator("crm.json")]
public readonly partial struct CrmType;
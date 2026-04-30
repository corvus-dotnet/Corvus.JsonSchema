using Corvus.Text.Json;

namespace InterfacesAndMixInTypes.Models;

[JsonSchemaTypeGenerator("composite-type.json")]
public readonly partial struct CompositeType;

[JsonSchemaTypeGenerator("countable.json")]
public readonly partial struct Countable;

[JsonSchemaTypeGenerator("documentation.json")]
public readonly partial struct Documentation;
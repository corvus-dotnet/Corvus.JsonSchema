using Corvus.Text.Json;

namespace PolymorphismWithDiscriminators.Models;

[JsonSchemaTypeGenerator("shape.json")]
public readonly partial struct Shape;

[JsonSchemaTypeGenerator("drawing.json")]
public readonly partial struct Drawing;
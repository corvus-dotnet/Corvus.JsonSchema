using Corvus.Text.Json;

namespace Corvus.MicroModels;

[JsonSchemaTypeGenerator("Schemas/object.json", EmitEvaluator = true)]
public readonly partial struct MicroObject;

[JsonSchemaTypeGenerator("Schemas/array.json", EmitEvaluator = true)]
public readonly partial struct MicroArray;

[JsonSchemaTypeGenerator("Schemas/string.json", EmitEvaluator = true)]
public readonly partial struct MicroString;

[JsonSchemaTypeGenerator("Schemas/unevaluated.json", EmitEvaluator = true)]
public readonly partial struct MicroUnevaluated;

[JsonSchemaTypeGenerator("Schemas/dynamic.json", EmitEvaluator = true)]
public readonly partial struct MicroDynamic;

[JsonSchemaTypeGenerator("Schemas/oneof.json", EmitEvaluator = true)]
public readonly partial struct MicroOneOf;

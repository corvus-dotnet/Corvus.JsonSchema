namespace Corvus.Text.Json.Tests.GeneratedModels.Draft202012;

// Regression model for the inline evaluated-bits buffer overflow: an object whose properties are
// tracked for evaluation (additionalProperties + unevaluatedProperties) so that validating an
// instance with 232..255 properties marks property index 231 — which used to collide with the
// inline/rented flag bit and fault the buffer accessor. See InlineEvaluatedBufferOverflowTests.
[JsonSchemaTypeGenerator("./inline-evaluated-buffer-2020-12.json")]
public readonly partial struct InlineEvaluatedBuffer
{
}

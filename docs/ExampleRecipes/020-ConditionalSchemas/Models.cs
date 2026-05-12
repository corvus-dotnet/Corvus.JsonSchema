using Corvus.Text.Json;

namespace ConditionalSchemas.Models;

/// <summary>
/// A payment that requires different fields based on the payment method.
/// Generated from payment.json.
/// </summary>
[JsonSchemaTypeGenerator("payment.json")]
public readonly partial struct Payment;
using Corvus.Text.Json;

namespace JSONata.Models;

/// <summary>
/// A simple order with customer name and line items.
/// Generated from order.json.
/// </summary>
[JsonSchemaTypeGenerator("order.json")]
public readonly partial struct Order;

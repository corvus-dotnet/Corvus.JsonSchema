using Corvus.Text.Json.Jsonata;

namespace JSONata.Expressions;

/// <summary>
/// A JSONata expression compiled at build time from total-price.jsonata.
/// Calculates the total price of all items in an order.
/// </summary>
[JsonataExpression("total-price.jsonata")]
public static partial class TotalPrice;

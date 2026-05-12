using Corvus.Text.Json.JMESPath;

namespace JMESPath.Expressions;

/// <summary>
/// A JMESPath expression compiled at build time from total-price.jmespath.
/// Calculates the sum of all item prices in an order.
/// </summary>
[JMESPathExpression("total-price.jmespath")]
public static partial class TotalPrice;

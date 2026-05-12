using Corvus.Text.Json.JsonLogic;

namespace JsonLogic.Rules;

/// <summary>
/// A discount rule compiled at build time from discount-rule.json.
/// Applies a 10% discount when the order total is >= 100.
/// </summary>
[JsonLogicRule("discount-rule.json")]
public static partial class DiscountRule;

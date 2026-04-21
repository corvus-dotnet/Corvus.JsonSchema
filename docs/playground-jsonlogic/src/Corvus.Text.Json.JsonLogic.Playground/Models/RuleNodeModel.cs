using Stj = System.Text.Json;
using StjElement = System.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Playground.Models;

/// <summary>
/// Base class for all nodes in a visual rule tree.
/// </summary>
public abstract class RuleNodeModel
{
    /// <summary>
    /// Unique identifier for Blazor rendering (@key).
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Serializes this node to a JSON Logic element.
    /// </summary>
    public abstract void WriteTo(Stj.Utf8JsonWriter writer);

    /// <summary>
    /// Serializes the full tree to a JSON string.
    /// </summary>
    public string ToJson(bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Stj.Utf8JsonWriter(stream, new Stj.JsonWriterOptions { Indented = indented }))
        {
            WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Parses a JSON Logic rule into a rule tree.
    /// Returns null if the JSON is empty/whitespace.
    /// </summary>
    /// <exception cref="Stj.JsonException">Thrown for malformed JSON.</exception>
    public static RuleNodeModel? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = Stj.JsonDocument.Parse(json);
        return FromElement(doc.RootElement);
    }

    /// <summary>
    /// Parses a <see cref="StjElement"/> into a rule tree node.
    /// </summary>
    public static RuleNodeModel FromElement(StjElement element)
    {
        switch (element.ValueKind)
        {
            case Stj.JsonValueKind.Object:
                // JSON Logic: a single-key object is an operator call
                var enumerator = element.EnumerateObject();
                if (!enumerator.MoveNext())
                {
                    // Empty object — treat as a value (empty object literal)
                    return new ValueNodeModel { Kind = ValueKind.Null, RawValue = "" };
                }

                var first = enumerator.Current;
                if (enumerator.MoveNext())
                {
                    // Multi-key object — not a valid JsonLogic operator.
                    // We can't represent this visually; return a raw JSON value.
                    return new RawJsonNodeModel { Json = element.GetRawText() };
                }

                string op = first.Name;
                var operands = new List<RuleNodeModel>();

                if (first.Value.ValueKind == Stj.JsonValueKind.Array)
                {
                    foreach (var arg in first.Value.EnumerateArray())
                    {
                        operands.Add(FromElement(arg));
                    }
                }
                else
                {
                    // Single non-array value: e.g. {"var": "x"} — wrap as single operand
                    operands.Add(FromElement(first.Value));
                }

                return new OperatorNodeModel { Operator = op, Operands = operands };

            case Stj.JsonValueKind.Array:
                var items = new List<RuleNodeModel>();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(FromElement(item));
                }

                return new ArrayNodeModel { Items = items };

            case Stj.JsonValueKind.String:
                return new ValueNodeModel
                {
                    Kind = ValueKind.String,
                    RawValue = element.GetString() ?? "",
                };

            case Stj.JsonValueKind.Number:
                return new ValueNodeModel
                {
                    Kind = ValueKind.Number,
                    RawValue = element.GetRawText(),
                };

            case Stj.JsonValueKind.True:
                return new ValueNodeModel { Kind = ValueKind.Boolean, RawValue = "true" };

            case Stj.JsonValueKind.False:
                return new ValueNodeModel { Kind = ValueKind.Boolean, RawValue = "false" };

            case Stj.JsonValueKind.Null:
                return new ValueNodeModel { Kind = ValueKind.Null, RawValue = "" };

            default:
                return new ValueNodeModel { Kind = ValueKind.Null, RawValue = "" };
        }
    }

    /// <summary>
    /// Creates a deep clone of this node.
    /// </summary>
    public abstract RuleNodeModel Clone();
}

/// <summary>
/// An operator invocation: {"op": [arg1, arg2, ...]}.
/// </summary>
public sealed class OperatorNodeModel : RuleNodeModel
{
    public string Operator { get; set; } = "==";

    public List<RuleNodeModel> Operands { get; set; } = [];

    public override void WriteTo(Stj.Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(Operator);
        writer.WriteStartArray();
        foreach (var operand in Operands)
        {
            operand.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public override RuleNodeModel Clone() => new OperatorNodeModel
    {
        Operator = Operator,
        Operands = Operands.Select(o => o.Clone()).ToList(),
    };
}

/// <summary>
/// A literal value: string, number, boolean, or null.
/// </summary>
public sealed class ValueNodeModel : RuleNodeModel
{
    public ValueKind Kind { get; set; } = ValueKind.String;

    public string RawValue { get; set; } = "";

    public override void WriteTo(Stj.Utf8JsonWriter writer)
    {
        switch (Kind)
        {
            case ValueKind.String:
                writer.WriteStringValue(RawValue);
                break;
            case ValueKind.Number:
                if (double.TryParse(RawValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                {
                    // Preserve integer form when possible
                    if (d == Math.Floor(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
                    {
                        writer.WriteNumberValue((long)d);
                    }
                    else
                    {
                        writer.WriteNumberValue(d);
                    }
                }
                else
                {
                    writer.WriteNumberValue(0);
                }

                break;
            case ValueKind.Boolean:
                writer.WriteBooleanValue(
                    string.Equals(RawValue, "true", StringComparison.OrdinalIgnoreCase));
                break;
            case ValueKind.Null:
                writer.WriteNullValue();
                break;
        }
    }

    public override RuleNodeModel Clone() => new ValueNodeModel
    {
        Kind = Kind,
        RawValue = RawValue,
    };
}

/// <summary>
/// A JSON array literal: [item1, item2, ...].
/// </summary>
public sealed class ArrayNodeModel : RuleNodeModel
{
    public List<RuleNodeModel> Items { get; set; } = [];

    public override void WriteTo(Stj.Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in Items)
        {
            item.WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    public override RuleNodeModel Clone() => new ArrayNodeModel
    {
        Items = Items.Select(i => i.Clone()).ToList(),
    };
}

/// <summary>
/// A raw JSON fragment that cannot be represented visually.
/// </summary>
public sealed class RawJsonNodeModel : RuleNodeModel
{
    public string Json { get; set; } = "null";

    public override void WriteTo(Stj.Utf8JsonWriter writer)
    {
        using var doc = Stj.JsonDocument.Parse(Json);
        doc.RootElement.WriteTo(writer);
    }

    public override RuleNodeModel Clone() => new RawJsonNodeModel { Json = Json };
}

public enum ValueKind
{
    String,
    Number,
    Boolean,
    Null,
}

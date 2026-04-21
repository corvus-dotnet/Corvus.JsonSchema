namespace Corvus.Text.Json.JsonLogic.Playground.Models;

/// <summary>
/// Categorizes JsonLogic operators for visual styling and grouping.
/// </summary>
public enum OperatorCategory
{
    Data,
    Logic,
    Comparison,
    Arithmetic,
    Array,
    String,
    Misc,
}

/// <summary>
/// Metadata about a JsonLogic operator.
/// </summary>
public sealed record OperatorInfo(
    string Key,
    string DisplayName,
    OperatorCategory Category,
    int MinArity,
    int? MaxArity,
    string[]? ArgLabels = null)
{
    /// <summary>
    /// CSS class name for this category.
    /// </summary>
    public string CategoryClass => Category switch
    {
        OperatorCategory.Data => "cat-data",
        OperatorCategory.Logic => "cat-logic",
        OperatorCategory.Comparison => "cat-comparison",
        OperatorCategory.Arithmetic => "cat-arithmetic",
        OperatorCategory.Array => "cat-array",
        OperatorCategory.String => "cat-string",
        _ => "cat-misc",
    };
}

/// <summary>
/// Registry of all supported JsonLogic operators.
/// </summary>
public static class OperatorRegistry
{
    private static readonly Dictionary<string, OperatorInfo> ByKey = new(StringComparer.Ordinal);

    public static IReadOnlyList<OperatorInfo> All { get; }

    public static IReadOnlyList<(string GroupName, IReadOnlyList<OperatorInfo> Operators)> Grouped { get; }

    static OperatorRegistry()
    {
        var all = new List<OperatorInfo>
        {
            // Data access
            new("var", "var", OperatorCategory.Data, 0, 2, ["Path", "Default"]),
            new("missing", "missing", OperatorCategory.Data, 1, null),
            new("missing_some", "missing_some", OperatorCategory.Data, 2, 2, ["Min required", "Keys"]),

            // Logic
            new("if", "if / then / else", OperatorCategory.Logic, 3, null, ["Condition", "Then", "Else"]),
            new("!", "not (!)", OperatorCategory.Logic, 1, 1, ["Value"]),
            new("!!", "truthy (!!)", OperatorCategory.Logic, 1, 1, ["Value"]),
            new("or", "or", OperatorCategory.Logic, 2, null),
            new("and", "and", OperatorCategory.Logic, 2, null),

            // Comparison
            new("==", "== (loose)", OperatorCategory.Comparison, 2, 2, ["Left", "Right"]),
            new("===", "=== (strict)", OperatorCategory.Comparison, 2, 2, ["Left", "Right"]),
            new("!=", "!= (loose)", OperatorCategory.Comparison, 2, 2, ["Left", "Right"]),
            new("!==", "!== (strict)", OperatorCategory.Comparison, 2, 2, ["Left", "Right"]),
            new("<", "< (less than)", OperatorCategory.Comparison, 2, 3, ["Left", "Middle", "Right"]),
            new("<=", "<= (less or equal)", OperatorCategory.Comparison, 2, 3, ["Left", "Middle", "Right"]),
            new(">", "> (greater than)", OperatorCategory.Comparison, 2, 3, ["Left", "Middle", "Right"]),
            new(">=", ">= (greater or equal)", OperatorCategory.Comparison, 2, 3, ["Left", "Middle", "Right"]),

            // Arithmetic
            new("+", "add (+)", OperatorCategory.Arithmetic, 1, null),
            new("-", "subtract (−)", OperatorCategory.Arithmetic, 1, 2),
            new("*", "multiply (×)", OperatorCategory.Arithmetic, 2, null),
            new("/", "divide (÷)", OperatorCategory.Arithmetic, 2, 2, ["Dividend", "Divisor"]),
            new("%", "modulo (%)", OperatorCategory.Arithmetic, 2, 2, ["Dividend", "Divisor"]),
            new("min", "min", OperatorCategory.Arithmetic, 1, null),
            new("max", "max", OperatorCategory.Arithmetic, 1, null),

            // Array operations
            new("map", "map", OperatorCategory.Array, 2, 2, ["Array", "Transform"]),
            new("filter", "filter", OperatorCategory.Array, 2, 2, ["Array", "Condition"]),
            new("reduce", "reduce", OperatorCategory.Array, 3, 3, ["Array", "Reducer", "Initial"]),
            new("all", "all", OperatorCategory.Array, 2, 2, ["Array", "Condition"]),
            new("some", "some", OperatorCategory.Array, 2, 2, ["Array", "Condition"]),
            new("none", "none", OperatorCategory.Array, 2, 2, ["Array", "Condition"]),
            new("merge", "merge", OperatorCategory.Array, 1, null),

            // String operations
            new("cat", "cat (concatenate)", OperatorCategory.String, 1, null),
            new("substr", "substr", OperatorCategory.String, 2, 3, ["String", "Start", "Length"]),
            new("in", "in (contains)", OperatorCategory.String, 2, 2, ["Needle", "Haystack"]),

            // Misc
            new("log", "log", OperatorCategory.Misc, 1, 1, ["Value"]),
        };

        All = all;

        foreach (var info in all)
        {
            ByKey[info.Key] = info;
        }

        Grouped = all
            .GroupBy(o => o.Category)
            .Select(g => (
                GroupName: g.Key.ToString(),
                Operators: (IReadOnlyList<OperatorInfo>)g.ToList()))
            .ToList();
    }

    /// <summary>
    /// Gets operator info by key, or null for unknown operators.
    /// </summary>
    public static OperatorInfo? Get(string key) =>
        ByKey.TryGetValue(key, out var info) ? info : null;

    /// <summary>
    /// Gets operator info by key, returning a fallback for unknown operators.
    /// </summary>
    public static OperatorInfo GetOrDefault(string key) =>
        ByKey.TryGetValue(key, out var info)
            ? info
            : new OperatorInfo(key, key, OperatorCategory.Misc, 0, null);

    /// <summary>
    /// Creates default operands for a newly-selected operator.
    /// </summary>
    public static List<RuleNodeModel> CreateDefaultOperands(string op)
    {
        var info = GetOrDefault(op);
        var operands = new List<RuleNodeModel>();

        switch (op)
        {
            case "var":
                operands.Add(new ValueNodeModel { Kind = ValueKind.String, RawValue = "" });
                break;

            case "if":
                operands.Add(new OperatorNodeModel { Operator = "==", Operands = CreateDefaultOperands("==") });
                operands.Add(new ValueNodeModel { Kind = ValueKind.String, RawValue = "yes" });
                operands.Add(new ValueNodeModel { Kind = ValueKind.String, RawValue = "no" });
                break;

            case "missing":
            case "missing_some":
                if (op == "missing_some")
                {
                    operands.Add(new ValueNodeModel { Kind = ValueKind.Number, RawValue = "1" });
                }

                operands.Add(new ArrayNodeModel
                {
                    Items = [new ValueNodeModel { Kind = ValueKind.String, RawValue = "field" }],
                });
                break;

            case "map":
            case "filter":
            case "all":
            case "some":
            case "none":
                operands.Add(new OperatorNodeModel
                {
                    Operator = "var",
                    Operands = [new ValueNodeModel { Kind = ValueKind.String, RawValue = "items" }],
                });
                operands.Add(new OperatorNodeModel { Operator = "==", Operands = CreateDefaultOperands("==") });
                break;

            case "reduce":
                operands.Add(new OperatorNodeModel
                {
                    Operator = "var",
                    Operands = [new ValueNodeModel { Kind = ValueKind.String, RawValue = "items" }],
                });
                operands.Add(new OperatorNodeModel
                {
                    Operator = "+",
                    Operands =
                    [
                        new OperatorNodeModel
                        {
                            Operator = "var",
                            Operands = [new ValueNodeModel { Kind = ValueKind.String, RawValue = "current" }],
                        },
                        new OperatorNodeModel
                        {
                            Operator = "var",
                            Operands = [new ValueNodeModel { Kind = ValueKind.String, RawValue = "accumulator" }],
                        },
                    ],
                });
                operands.Add(new ValueNodeModel { Kind = ValueKind.Number, RawValue = "0" });
                break;

            default:
                for (int i = 0; i < info.MinArity; i++)
                {
                    operands.Add(new ValueNodeModel { Kind = ValueKind.Number, RawValue = "0" });
                }

                break;
        }

        return operands;
    }
}

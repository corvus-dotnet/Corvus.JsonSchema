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
/// The visual shape of a block (matches Scratch block taxonomy).
/// </summary>
public enum BlockShape
{
    /// <summary>Rounded reporter block — returns a value.</summary>
    Reporter,

    /// <summary>Hexagonal boolean block — returns true/false.</summary>
    Boolean,

    /// <summary>C-shaped control block — wraps body expressions.</summary>
    CBlock,
}

/// <summary>
/// The type of expression a slot accepts.
/// </summary>
public enum SlotType
{
    /// <summary>Accepts any expression (reporter or boolean). Default for JSON Logic's loose typing.</summary>
    Any,

    /// <summary>Primarily boolean (shows boolean blocks first, but accepts reporters for truthy coercion).</summary>
    Boolean,

    /// <summary>Value-returning expression (reporters, var, arithmetic, etc.).</summary>
    Reporter,
}

/// <summary>
/// How to render an operator block visually.
/// </summary>
public enum RenderStyle
{
    /// <summary>Infix symbol: [A] op [B].</summary>
    Infix,

    /// <summary>Infix keyword: A and B.</summary>
    InfixKeyword,

    /// <summary>Prefix: op [A].</summary>
    Prefix,

    /// <summary>Control flow: if/then/else C-block.</summary>
    Control,

    /// <summary>Data access pill: var.</summary>
    Data,

    /// <summary>Function call: name(A, B).</summary>
    Function,
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
    /// How this operator should be rendered in the visual builder.
    /// </summary>
    public RenderStyle Style { get; init; } = RenderStyle.Function;

    /// <summary>
    /// The visual shape of this block.
    /// </summary>
    public BlockShape Shape { get; init; } = BlockShape.Reporter;

    /// <summary>
    /// Slot types for each operand position. Empty means all slots are <see cref="SlotType.Any"/>.
    /// For variadic operators, the last entry repeats.
    /// </summary>
    public SlotType[] SlotTypes { get; init; } = [];

    /// <summary>
    /// Gets the slot type for a given operand index.
    /// </summary>
    public SlotType GetSlotType(int index)
    {
        if (SlotTypes.Length == 0)
        {
            return SlotType.Any;
        }

        return index < SlotTypes.Length ? SlotTypes[index] : SlotTypes[^1];
    }

    /// <summary>
    /// CSS class suffix for this category.
    /// </summary>
    public string CategoryCss => Category switch
    {
        OperatorCategory.Data => "data",
        OperatorCategory.Logic => "logic",
        OperatorCategory.Comparison => "comparison",
        OperatorCategory.Arithmetic => "arithmetic",
        OperatorCategory.Array => "array",
        OperatorCategory.String => "string",
        _ => "misc",
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
            // Data access — reporter shape (returns values)
            new("var", "var", OperatorCategory.Data, 0, 2, ["Path", "Default"])
                { Style = RenderStyle.Data, Shape = BlockShape.Reporter },
            new("missing", "missing", OperatorCategory.Data, 1, null)
                { Shape = BlockShape.Reporter },
            new("missing_some", "missing_some", OperatorCategory.Data, 2, 2, ["Min required", "Keys"])
                { Shape = BlockShape.Reporter },

            // Logic — and/or return values in JSON Logic (not strictly boolean)
            new("if", "if / then / else", OperatorCategory.Logic, 3, null, ["Condition", "Then", "Else"])
                { Style = RenderStyle.Control, Shape = BlockShape.CBlock, SlotTypes = [SlotType.Boolean, SlotType.Any, SlotType.Any] },
            new("!", "not", OperatorCategory.Logic, 1, 1, ["Value"])
                { Style = RenderStyle.Prefix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },
            new("!!", "truthy", OperatorCategory.Logic, 1, 1, ["Value"])
                { Style = RenderStyle.Prefix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },
            // Logic — and/or are binary boolean operators (hexagonal) in Scratch style
            // Chains are nested: (a AND b AND c) → a AND (b AND c)
            new("or", "or", OperatorCategory.Logic, 2, 2)
                { Style = RenderStyle.InfixKeyword, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Boolean] },
            new("and", "and", OperatorCategory.Logic, 2, 2)
                { Style = RenderStyle.InfixKeyword, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Boolean] },

            // Comparison — boolean shape (returns true/false)
            new("==", "=", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any, SlotType.Any] },
            new("===", "≡", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any, SlotType.Any] },
            new("!=", "≠", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any, SlotType.Any] },
            new("!==", "≢", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any, SlotType.Any] },
            new("<", "<", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },
            new("<=", "≤", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },
            new(">", ">", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },
            new(">=", "≥", OperatorCategory.Comparison, 2, 2, ["Left", "Right"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },

            // Between — visual wrappers for 3-argument < and <=
            new("between<", "< _ <", OperatorCategory.Comparison, 3, 3, ["Low", "Value", "High"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },
            new("between<=", "≤ _ ≤", OperatorCategory.Comparison, 3, 3, ["Low", "Value", "High"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any] },

            // Arithmetic — reporter shape (returns numbers)
            new("+", "+", OperatorCategory.Arithmetic, 1, null)
                { Style = RenderStyle.Infix, Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("-", "−", OperatorCategory.Arithmetic, 1, 2)
                { Style = RenderStyle.Infix, Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("*", "×", OperatorCategory.Arithmetic, 2, null)
                { Style = RenderStyle.Infix, Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("/", "÷", OperatorCategory.Arithmetic, 2, 2, ["Dividend", "Divisor"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("%", "mod", OperatorCategory.Arithmetic, 2, 2, ["Dividend", "Divisor"])
                { Style = RenderStyle.Infix, Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("min", "min", OperatorCategory.Arithmetic, 1, null)
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("max", "max", OperatorCategory.Arithmetic, 1, null)
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },

            // Array operations
            new("map", "map", OperatorCategory.Array, 2, 2, ["Array", "Transform"])
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Reporter, SlotType.Any] },
            new("filter", "filter", OperatorCategory.Array, 2, 2, ["Array", "Condition"])
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Reporter, SlotType.Boolean] },
            new("reduce", "reduce", OperatorCategory.Array, 3, 3, ["Array", "Reducer", "Initial"])
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Reporter, SlotType.Any, SlotType.Any] },
            new("all", "all", OperatorCategory.Array, 2, 2, ["Array", "Condition"])
                { Shape = BlockShape.Boolean, SlotTypes = [SlotType.Reporter, SlotType.Boolean] },
            new("some", "some", OperatorCategory.Array, 2, 2, ["Array", "Condition"])
                { Shape = BlockShape.Boolean, SlotTypes = [SlotType.Reporter, SlotType.Boolean] },
            new("none", "none", OperatorCategory.Array, 2, 2, ["Array", "Condition"])
                { Shape = BlockShape.Boolean, SlotTypes = [SlotType.Reporter, SlotType.Boolean] },
            new("merge", "merge", OperatorCategory.Array, 1, null)
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },

            // String operations
            new("cat", "cat", OperatorCategory.String, 1, null)
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("substr", "substr", OperatorCategory.String, 2, 3, ["String", "Start", "Length"])
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
            new("in", "in", OperatorCategory.String, 2, 2, ["Needle", "Haystack"])
                { Style = RenderStyle.InfixKeyword, Shape = BlockShape.Boolean, SlotTypes = [SlotType.Any, SlotType.Any] },

            // Misc
            new("log", "log", OperatorCategory.Misc, 1, 1, ["Value"])
                { Shape = BlockShape.Reporter, SlotTypes = [SlotType.Any] },
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

        // Boolean-only: only boolean-shaped operators + var (for truthiness checks)
        var boolOps = all.Where(o => o.Shape == BlockShape.Boolean).ToList();
        var varOp = all.Where(o => o.Key == "var").ToList();
        BooleanOnlyGrouped = new List<(string, IReadOnlyList<OperatorInfo>)>
        {
            ("Conditions", boolOps),
            ("Data", varOp),
        };

        // Reporter-first: reporters + data at top, boolean ops available too
        var reporterOps = all.Where(o => o.Shape == BlockShape.Reporter || o.Shape == BlockShape.CBlock).ToList();
        ReporterFirstGrouped = new List<(string, IReadOnlyList<OperatorInfo>)>
        {
            ("Blocks", reporterOps),
        };
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
    /// Gets operators appropriate for a given slot type.
    /// Boolean slots: boolean-shaped blocks first, then reporters.
    /// Reporter slots: reporter-shaped blocks, then boolean.
    /// Any: all operators.
    /// </summary>
    public static IReadOnlyList<(string GroupName, IReadOnlyList<OperatorInfo> Operators)> GetFilteredGroups(SlotType slotType)
    {
        return slotType switch
        {
            SlotType.Boolean => BooleanOnlyGrouped,
            SlotType.Reporter => ReporterFirstGrouped,
            _ => Grouped,
        };
    }

    private static IReadOnlyList<(string GroupName, IReadOnlyList<OperatorInfo> Operators)> BooleanOnlyGrouped { get; }
    private static IReadOnlyList<(string GroupName, IReadOnlyList<OperatorInfo> Operators)> ReporterFirstGrouped { get; }

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
                operands.Add(new EmptySlotModel()); // condition
                operands.Add(new EmptySlotModel()); // then
                break;

            case "missing":
                operands.Add(new ArrayNodeModel
                {
                    Items = [new EmptySlotModel()],
                });
                break;

            case "missing_some":
                operands.Add(new EmptySlotModel()); // min required
                operands.Add(new ArrayNodeModel
                {
                    Items = [new EmptySlotModel()],
                });
                break;

            default:
                for (int i = 0; i < info.MinArity; i++)
                {
                    operands.Add(new EmptySlotModel());
                }

                break;
        }

        return operands;
    }
}

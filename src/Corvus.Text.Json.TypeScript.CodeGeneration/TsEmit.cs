// <copyright file="TsEmit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal static class TsEmit
{
    public static string Str(string name) => JsonSerializer.Serialize(name);

    // The results collector (§15). The THREE locations: `il` = instanceLocation (threaded); `kl` =
    // keywordLocation = the PATH TAKEN (threaded, accumulates each child's path modifier INCLUDING `$ref`
    // steps); absoluteKeywordLocation = the type's RESOLVED location `td.LocatedSchema.Location` (a constant).
    // FailRecord appends `/keyword` to kl (runtime) and to the resolved location (constant); FailShape wraps
    // it so the boolean hot path short-circuits (r === null) with no string building, else records and sets
    // ok = false to fall through and gather every failure.
    public static string FailRecord(TypeDeclaration td, string? keyword)
    {
        string seg = keyword is null ? string.Empty : "/" + keyword;
        string klExpr = seg.Length == 0 ? "kl" : "kl + " + Str(seg);
        return "r.fail(" + klExpr + ", il, " + Str(AbsoluteKeywordLocation(td, keyword)) + ");";
    }

    // absoluteKeywordLocation is base-URI + '#' + JSON-pointer fragment. A document-root schema's resolved
    // location carries no '#' fragment, so it must be supplied before the keyword segment; subschema
    // locations (properties / $defs / $ref targets) already include '#'.
    public static string AbsoluteKeywordLocation(TypeDeclaration td, string? keyword)
    {
        string loc = td.LocatedSchema.Location.ToString();
        if (!loc.Contains('#'))
        {
            loc += "#";
        }

        return keyword is null ? loc : loc + "/" + keyword;
    }

    public static string FailShape(TypeDeclaration td, string? keyword)
        => "if (r === null) return false; " + FailRecord(td, keyword) + " ok = false;";

    // Child-recursion arguments when collecting (gated on `r` so the boolean hot path builds no string): the
    // instance pointer `il`, then the keyword path `kl` with the child's path modifier appended, then `r`. The
    // collector is a threaded parameter, not a field on `ev` (children get a fresh/NOEV tracker, so ev.r is
    // lost on descent). `pm` is the path modifier from GetPathModifier/KeywordPathModifier with its leading
    // `#` stripped (it already includes the `$ref` step, so kl diverges from absoluteKeywordLocation there).
    private static string StripHash(string pm) => pm.Length > 0 && pm[0] == '#' ? pm[1..] : pm;

    public static string ChildKey(string pm)
        => ", (r === null ? il : il + \"/\" + __ptr(k)), (r === null ? kl : kl + " + Str(StripHash(pm)) + "), r";

    public static string ChildIdx(string idx, string pm)
        => ", (r === null ? il : il + \"/\" + " + idx + "), (r === null ? kl : kl + " + Str(StripHash(pm)) + "), r";

    // A subschema-on-`value` applicator (allOf/then/else/dependentSchemas): instance pointer unchanged, kl
    // gets the path modifier, then `r`.
    public static string ChildValue(string pm)
        => ", il, (r === null ? kl : kl + " + Str(StripHash(pm)) + "), r";

    // A child applicator reported its own failure; the parent only propagates the verdict (no re-record):
    // short-circuit on the boolean path, else mark ok = false and fall through to gather the rest.
    public const string Propagate = "if (r === null) return false; ok = false;";

    public static string? EvalName(TypeDeclaration t)
        => t.TryGetMetadata<string>("Ts_FinalName", out string? n) && !string.IsNullOrEmpty(n) ? "evaluate" + n : null;

    // Does this type need an evaluation tracker threaded (it or an in-place applicator uses unevaluated*)?
    public static bool Tracks(TypeDeclaration t) => t.RequiresPropertyEvaluationTracking() || t.RequiresItemsEvaluationTracking();

    // The tracker argument for a SUB-INSTANCE recursion (a property value / array item is its own scope):
    // a fresh tracker when the child needs one, else the shared no-op tracker.
    public static string SubEv(TypeDeclaration child) => Tracks(child) ? "fresh()" : "NOEV";

    public static string KindExpr(CoreTypes t) => t switch
    {
        CoreTypes.Object => "__isObj(value)",
        CoreTypes.Array => "Array.isArray(value)",
        CoreTypes.String => "typeof value === \"string\"",
        CoreTypes.Number => "__isNum(value)",
        CoreTypes.Integer => "(__isNum(value) && __isInt(String(value)))",
        CoreTypes.Boolean => "typeof value === \"boolean\"",
        CoreTypes.Null => "value === null",
        _ => "true",
    };

    // The TS fail-condition for a length/count operator (small safe-integer comparison; plain JS).
    public static string? FailCondition(Operator op, string left, string constText) => op switch
    {
        Operator.GreaterThan => $"{left} <= {constText}",
        Operator.GreaterThanOrEquals => $"{left} < {constText}",
        Operator.LessThan => $"{left} >= {constText}",
        Operator.LessThanOrEquals => $"{left} > {constText}",
        _ => null,
    };

    // The fail-condition for a NUMERIC value operator, evaluated exactly on the number's text (§4.1):
    // bounds via __cmp(String(value), <literal>) <op> 0; multipleOf via !__multipleOf(...).
    public static string? NumericFailCondition(Operator op, string constText) => op switch
    {
        Operator.GreaterThan => $"__cmp(String(value), {constText}) <= 0",
        Operator.GreaterThanOrEquals => $"__cmp(String(value), {constText}) < 0",
        Operator.LessThan => $"__cmp(String(value), {constText}) >= 0",
        Operator.LessThanOrEquals => $"__cmp(String(value), {constText}) > 0",
        Operator.MultipleOf => $"!__multipleOf(String(value), {constText})",
        _ => null,
    };
}
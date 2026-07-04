// <copyright file="PyEmit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

/// <summary>
/// The per-module emission accumulator threaded through the keyword emitters: the module body plus the
/// imports it turned out to need (typing names, sibling modules, and runtime names).
/// </summary>
/// <remarks>
/// Unlike the TypeScript engine (single-file by default, so child validators are referenced unqualified),
/// the Python engine is module-per-type, so a cross-type validator call is qualified
/// (<c>module.evaluate_typed</c>) and the referenced sibling module is recorded here for import.
/// </remarks>
internal sealed class PyModule(string runtimeModule)
{
    /// <summary>Gets the import specifier for the shared model runtime (e.g. <c>corvus_json_runtime</c>).</summary>
    public string RuntimeModule { get; } = runtimeModule;

    /// <summary>Gets the accumulating module body (all type surfaces + validators for the single generated file).</summary>
    public StringBuilder Body { get; } = new();

    /// <summary>Gets module-level statements to emit after all validators (e.g. a property dispatch table).</summary>
    public StringBuilder ModuleLevel { get; } = new();

    /// <summary>Gets the union type-guard function names already emitted, for dedup across the single file.</summary>
    public HashSet<string> EmittedGuards { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the names imported from <c>typing</c>.</summary>
    public SortedSet<string> Typing { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the names imported from the shared model runtime.</summary>
    public SortedSet<string> Runtime { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets a value indicating whether the module imports <c>json</c>.</summary>
    public bool NeedsJson { get; set; }

    /// <summary>Gets or sets a value indicating whether the module imports <c>Sequence</c>.</summary>
    public bool NeedsSequence { get; set; }

    /// <summary>Gets or sets a value indicating whether the module imports <c>Mapping</c>.</summary>
    public bool NeedsMapping { get; set; }

    /// <summary>Gets or sets a value indicating whether the module imports <c>Callable</c>.</summary>
    public bool NeedsCallable { get; set; }

    /// <summary>Records a runtime name as used and returns it (for inline composition).</summary>
    /// <param name="name">The runtime name to import.</param>
    /// <returns>The name.</returns>
    public string Rt(string name)
    {
        this.Runtime.Add(name);
        return name;
    }
}

/// <summary>
/// Shared emit helpers for the Python keyword emitters (the Python peer of <c>TsEmit</c>).
/// </summary>
internal static class PyEmit
{
    /// <summary>A Python string literal (JSON escaping is a compatible subset).</summary>
    /// <param name="value">The string.</param>
    /// <returns>The double-quoted literal.</returns>
    public static string Str(string value) => JsonSerializer.Serialize(value);

    /// <summary>The absolute keyword location (base URI + '#' fragment + keyword) for spec output.</summary>
    /// <param name="td">The type declaration.</param>
    /// <param name="keyword">The keyword, or null for the type itself.</param>
    /// <returns>The absolute keyword location.</returns>
    public static string AbsoluteKeywordLocation(TypeDeclaration td, string? keyword)
    {
        string loc = td.LocatedSchema.Location.ToString();
        if (!loc.Contains('#'))
        {
            loc += "#";
        }

        return keyword is null ? loc : loc + "/" + keyword;
    }

    /// <summary>Emit <c>if {condition}:</c> then the fail block (fast-path return, else record + ok = False).</summary>
    /// <param name="mod">The module context.</param>
    /// <param name="td">The type declaration.</param>
    /// <param name="keyword">The failing keyword (for the recorded location).</param>
    /// <param name="condition">The Python fail condition.</param>
    /// <param name="indent">The indent (spaces) of the <c>if</c>.</param>
    public static void Check(PyModule mod, TypeDeclaration td, string? keyword, string condition, int indent)
    {
        mod.Body.Append(new string(' ', indent)).Append("if ").Append(condition).Append(":\n");
        FailBody(mod, td, keyword, indent + 4);
    }

    /// <summary>Emit the fail block (fast-path return, else record + ok = False) at the given indent.</summary>
    /// <param name="mod">The module context.</param>
    /// <param name="td">The type declaration.</param>
    /// <param name="keyword">The failing keyword (for the recorded location).</param>
    /// <param name="indent">The indent (spaces).</param>
    public static void FailBody(PyModule mod, TypeDeclaration td, string? keyword, int indent)
    {
        string pad = new(' ', indent);
        string pad4 = new(' ', indent + 4);
        string seg = keyword is null ? string.Empty : "/" + keyword;
        mod.Body.Append(pad).Append("if r is None:\n").Append(pad4).Append("return False\n");
        mod.Body.Append(pad).Append("r.fail(").Append(Str(seg)).Append(", ").Append(Str(AbsoluteKeywordLocation(td, keyword))).Append(")\n");
        mod.Body.Append(pad).Append("ok = False\n");
    }

    /// <summary>The Python expression for the instance-location segment of the current property key <c>_k</c>.</summary>
    public const string PropInstSeg = "\"/\" + _k.replace(\"~\", \"~0\").replace(\"/\", \"~1\")";

    /// <summary>
    /// Emit a lazy-location descent into a child validator: <c>enter</c> the child's location (only when a
    /// collector is present, so the fast path builds no location), call the child capturing its boolean result
    /// into <c>_c</c>, then <c>leave</c>. The caller inspects <c>_c</c> (propagate / count / merge).
    /// </summary>
    /// <param name="mod">The module context.</param>
    /// <param name="evalRef">The child validator function name.</param>
    /// <param name="valueExpr">The Python expression for the child value.</param>
    /// <param name="subEv">The tracker argument expression.</param>
    /// <param name="instSegExpr">The instance-location segment Python expression (e.g. <see cref="PropInstSeg"/>, or <c>""</c> for a same-instance applicator).</param>
    /// <param name="kwSegExpr">The keyword-location segment as a Python expression (a <see cref="Str"/> literal, or a runtime expression like <c>_p[2]</c>).</param>
    /// <param name="indent">The indent (spaces).</param>
    /// <param name="collector">The collector variable to enter/leave and thread (default <c>r</c>; a composition sub-collector like <c>_anyrb</c> otherwise).</param>
    public static void EmitDescentCall(PyModule mod, string evalRef, string valueExpr, string subEv, string instSegExpr, string kwSegExpr, int indent, string collector = "r")
    {
        string pad = new(' ', indent);
        mod.Body.Append(pad).Append("if ").Append(collector).Append(" is not None: ").Append(collector).Append(".enter(").Append(instSegExpr).Append(", ").Append(kwSegExpr).Append(")\n");
        mod.Body.Append(pad).Append("_c = ").Append(evalRef).Append("(").Append(valueExpr).Append(", ").Append(subEv).Append(", ").Append(collector).Append(")\n");
        mod.Body.Append(pad).Append("if ").Append(collector).Append(" is not None: ").Append(collector).Append(".leave()\n");
    }

    /// <summary>Emit the propagate block: a child applicator already recorded, so fast-path return, else ok = False.</summary>
    /// <param name="mod">The module context.</param>
    /// <param name="indent">The indent (spaces).</param>
    public static void Propagate(PyModule mod, int indent)
    {
        string pad = new(' ', indent);
        string pad4 = new(' ', indent + 4);
        mod.Body.Append(pad).Append("if r is None:\n").Append(pad4).Append("return False\n");
        mod.Body.Append(pad).Append("ok = False\n");
    }

    /// <summary>
    /// The "value is NOT one of the allowed core types" fail expression for a type, over an arbitrary value
    /// expression (the single source of truth for the core-type check, shared by the type handler and inlining).
    /// </summary>
    /// <param name="mod">The module context.</param>
    /// <param name="td">The type declaration.</param>
    /// <param name="valueExpr">The Python expression for the value under test.</param>
    /// <returns>The fail condition, or null when there is no definite core-type constraint.</returns>
    public static string? CoreTypeCheckExpr(PyModule mod, TypeDeclaration td, string valueExpr)
    {
        CoreTypes ct = td.AllowedCoreTypes();
        if (ct == CoreTypes.None || ct == CoreTypes.Any)
        {
            return null;
        }

        var kinds = new List<string>();
        if (ct.HasFlag(CoreTypes.Object)) { kinds.Add(mod.Rt("is_obj") + "(" + valueExpr + ")"); }
        if (ct.HasFlag(CoreTypes.Array)) { kinds.Add(mod.Rt("is_arr") + "(" + valueExpr + ")"); }
        if (ct.HasFlag(CoreTypes.String)) { kinds.Add("isinstance(" + valueExpr + ", str)"); }
        if (ct.HasFlag(CoreTypes.Number)) { kinds.Add(mod.Rt("is_num") + "(" + valueExpr + ")"); }
        else if (ct.HasFlag(CoreTypes.Integer)) { kinds.Add(mod.Rt("is_int") + "(" + valueExpr + ")"); }
        if (ct.HasFlag(CoreTypes.Boolean)) { kinds.Add("isinstance(" + valueExpr + ", bool)"); }
        if (ct.HasFlag(CoreTypes.Null)) { kinds.Add(valueExpr + " is None"); }

        return kinds.Count == 0 ? null : "not (" + string.Join(" or ", kinds) + ")";
    }

    /// <summary>
    /// True when a type's entire validation is a FLAT SCALAR: a scalar core-type check plus only value-level
    /// constraints (const/enum, numeric bounds/multipleOf, string length, pattern) — no structure, composition,
    /// content, format, or sub-schema (so no possible recursion). Such a type can be inlined at a call site,
    /// eliminating the Python function call. Annotations do not block inlining and are not reproduced (validation
    /// results are identical). The exhaustive keyword blocklist is guarded by the full compliance suite.
    /// </summary>
    /// <param name="td">The (already reduced) type declaration.</param>
    /// <returns>Whether the type is a flat scalar.</returns>
    public static bool IsFlatScalar(TypeDeclaration td)
    {
        CoreTypes ct = td.AllowedCoreTypes();
        if (ct.HasFlag(CoreTypes.Object) || ct.HasFlag(CoreTypes.Array))
        {
            return false;
        }

        foreach (IKeyword kw in td.Keywords())
        {
            if (kw is IObjectRequiredPropertyValidationKeyword
                or IArrayLengthConstantValidationKeyword or IPropertyCountConstantValidationKeyword
                or IContentMediaTypeValidationKeyword or IContentEncodingValidationKeyword
                or IFormatProviderKeyword
                or IObjectPropertyValidationKeyword or IObjectPatternPropertyValidationKeyword
                or ILocalEvaluatedPropertyValidationKeyword or ILocalAndAppliedEvaluatedPropertyValidationKeyword
                or IObjectPropertyNameSubschemaValidationKeyword or IObjectDependentRequiredValidationKeyword
                or IObjectPropertyDependentSchemasValidationKeyword
                or ITupleTypeProviderKeyword or IArrayItemsTypeProviderKeyword
                or IUnevaluatedArrayItemsTypeProviderKeyword or IArrayContainsValidationKeyword
                or IArrayContainsCountConstantValidationKeyword or IUniqueItemsArrayValidationKeyword
                or IAllOfSubschemaValidationKeyword or IAnyOfSubschemaValidationKeyword
                or IOneOfSubschemaValidationKeyword or INotValidationKeyword or ITernaryIfValidationKeyword)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Inline a FLAT SCALAR child's checks against <paramref name="valueExpr"/> (enter its location, emit each
    /// value-level check, leave), eliminating a validator call. Returns false and emits nothing when the child is
    /// not flat-scalar-inlinable — the caller falls back to a normal descent. Post-descent marking is the caller's.
    /// </summary>
    /// <param name="mod">The module context.</param>
    /// <param name="childTd">The child type.</param>
    /// <param name="valueExpr">The Python expression for the child value (e.g. <c>value[_k]</c>).</param>
    /// <param name="instSegExpr">The instance-location segment Python expression.</param>
    /// <param name="kwSegExpr">The keyword-location segment Python expression (a <see cref="Str"/> literal or runtime expr).</param>
    /// <param name="indent">The indent (spaces).</param>
    /// <returns>Whether the child was inlined.</returns>
    public static bool TryEmitFlatScalarInline(PyModule mod, TypeDeclaration childTd, string valueExpr, string instSegExpr, string kwSegExpr, int indent)
    {
        TypeDeclaration reduced = childTd.ReducedTypeDeclaration().ReducedType;
        if (!IsGenerated(reduced) || !IsFlatScalar(reduced))
        {
            return false;
        }

        string pad = new(' ', indent);
        mod.Body.Append(pad).Append("if r is not None: r.enter(").Append(instSegExpr).Append(", ").Append(kwSegExpr).Append(")\n");

        // Bind the child value to a local NAME so `is_num(_v)` / `isinstance(_v, str)` narrow it (a TypeGuard
        // narrows names, not index expressions like `value[_k]`), keeping the inlined bound/length checks
        // strict-typed. Also evaluates the subscript once.
        mod.Body.Append(pad).Append("_v = ").Append(valueExpr).Append("\n");
        valueExpr = "_v";

        bool firstCoreType = true;
        foreach (IKeyword kw in reduced.Keywords())
        {
            if (kw is ICoreTypeValidationKeyword)
            {
                if (!firstCoreType)
                {
                    continue;
                }

                firstCoreType = false;
                if (CoreTypeCheckExpr(mod, reduced, valueExpr) is string typeCond)
                {
                    EmitInlineCheck(mod, typeCond, "/" + kw.Keyword, AbsoluteKeywordLocation(reduced, kw.Keyword), indent);
                }
            }
            else if ((kw is IAnyOfConstantValidationKeyword or ISingleConstantValidationKeyword)
                     && kw is IValidationConstantProviderKeyword cp && cp.TryGetValidationConstants(reduced, out JsonElement[]? consts))
            {
                string lits = string.Join(", ", consts.Select(Literal));
                EmitInlineCheck(mod, "not any(" + mod.Rt("eq") + "(" + valueExpr + ", _a) for _a in [" + lits + "])", "/" + kw.Keyword, AbsoluteKeywordLocation(reduced, kw.Keyword), indent);
            }
            else if (kw is INumberConstantValidationKeyword num
                     && num.TryGetOperator(reduced, out Operator nop) && num.TryGetValidationConstants(reduced, out JsonElement[]? nc) && nc.Length > 0
                     && NumericFailCondition(nop, nc[0].GetRawText(), mod, valueExpr) is string ncond)
            {
                EmitInlineCheck(mod, mod.Rt("is_num") + "(" + valueExpr + ") and " + ncond, "/" + kw.Keyword, AbsoluteKeywordLocation(reduced, kw.Keyword), indent);
            }
            else if (kw is IStringLengthConstantValidationKeyword
                     && ((IIntegerConstantValidationKeyword)kw).TryGetOperator(reduced, out Operator lop)
                     && ((IIntegerConstantValidationKeyword)kw).TryGetValidationConstants(reduced, out JsonElement[]? lc) && lc.Length > 0
                     && LengthFailCondition(lop, "len(" + valueExpr + ")", lc[0].GetRawText()) is string lcond)
            {
                EmitInlineCheck(mod, "isinstance(" + valueExpr + ", str) and " + lcond, "/" + kw.Keyword, AbsoluteKeywordLocation(reduced, kw.Keyword), indent);
            }
            else if (kw is IStringRegexValidationProviderKeyword && kw is IValidationRegexProviderKeyword rp
                     && rp.TryGetValidationRegularExpressions(reduced, out IReadOnlyList<string>? regexes))
            {
                foreach (string regex in regexes)
                {
                    string py = EcmaRegexPythonTranslator.Translate(regex);
                    EmitInlineCheck(mod, "isinstance(" + valueExpr + ", str) and not " + mod.Rt("re_test") + "(" + Str(py) + ", " + valueExpr + ")", "/" + kw.Keyword, AbsoluteKeywordLocation(reduced, kw.Keyword), indent);
                }
            }
        }

        mod.Body.Append(pad).Append("if r is not None: r.leave()\n");
        return true;
    }

    // `if {cond}:` then a fast-path-return-else-record-suffix inline fail, at the given indent.
    private static void EmitInlineCheck(PyModule mod, string cond, string kwSuffix, string absLoc, int indent)
    {
        mod.Body.Append(new string(' ', indent)).Append("if ").Append(cond).Append(":\n");
        InlineFail(mod, indent + 4, kwSuffix, absLoc);
    }

    /// <summary>Emit an inline fail block (fast-path return, else record at the current location + a suffix).</summary>
    /// <param name="mod">The module context.</param>
    /// <param name="indent">The indent (spaces).</param>
    /// <param name="kwSuffix">The keyword-location suffix relative to the entered location (e.g. <c>/type</c>).</param>
    /// <param name="absLoc">The absolute keyword location literal.</param>
    public static void InlineFail(PyModule mod, int indent, string kwSuffix, string absLoc)
    {
        string pad = new(' ', indent);
        string pad4 = new(' ', indent + 4);
        mod.Body.Append(pad).Append("if r is None:\n").Append(pad4).Append("return False\n");
        mod.Body.Append(pad).Append("r.fail(").Append(Str(kwSuffix)).Append(", ").Append(Str(absLoc)).Append(")\n");
        mod.Body.Append(pad).Append("ok = False\n");
    }

    /// <summary>The unqualified reference to a child type's threaded validator function in the single file.</summary>
    /// <param name="mod">The module context (unused; kept for call-site symmetry).</param>
    /// <param name="childTd">The child type.</param>
    /// <returns>The <c>_eval_&lt;name&gt;</c> function name, or null when the child has no generated validator.</returns>
    public static string? EvalRef(PyModule mod, TypeDeclaration childTd)
    {
        // Reduce to the canonical (deduped) declaration — the instance names were assigned to. A type with no
        // generated validator (an un-named true/false subschema the core did not emit) returns null, exactly as
        // the TypeScript EvalName does, so the caller SKIPS it. Every type shares one module, so a reference to
        // the current type (self-recursion) and to any other type is the same unqualified function call.
        _ = mod;
        TypeDeclaration reduced = childTd.ReducedTypeDeclaration().ReducedType;
        return PythonLanguageProvider.HasModule(reduced)
            ? PythonLanguageProvider.EvalNameOf(reduced)
            : null;
    }

    /// <summary>True when a module was generated for this (reduced) type — a side-effect-free presence check.</summary>
    /// <param name="td">The type declaration.</param>
    /// <returns>Whether the reduced type has a generated module.</returns>
    public static bool IsGenerated(TypeDeclaration td)
        => PythonLanguageProvider.HasModule(td.ReducedTypeDeclaration().ReducedType);

    /// <summary>The non-null validator references of a composition's members (mirrors CompositionEvals.Members).</summary>
    /// <typeparam name="TKeyword">The composition keyword type.</typeparam>
    /// <param name="mod">The module context.</param>
    /// <param name="composition">The composition map, or null.</param>
    /// <returns>The <c>evaluate_typed</c> references of the members that are generated types, in order.</returns>
    public static List<string> MemberEvals<TKeyword>(PyModule mod, IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? composition)
        where TKeyword : notnull
    {
        var result = new List<string>();
        if (composition is null)
        {
            return result;
        }

        foreach (KeyValuePair<TKeyword, IReadOnlyCollection<TypeDeclaration>> kv in composition)
        {
            foreach (TypeDeclaration m in kv.Value)
            {
                if (EvalRef(mod, m) is string e)
                {
                    result.Add(e);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The tracker argument for a sub-instance recursion: a fresh tracker when the child itself consults
    /// <c>unevaluated*</c> (so its own marking is retained), else the shared no-op tracker.
    /// </summary>
    /// <param name="mod">The module context.</param>
    /// <param name="child">The child type being recursed into.</param>
    /// <returns>The tracker expression (<c>fresh()</c> or <c>NOEV</c>).</returns>
    public static string SubEv(PyModule mod, TypeDeclaration child)
    {
        TypeDeclaration reduced = child.ReducedTypeDeclaration().ReducedType;
        return reduced.RequiresPropertyEvaluationTracking() || reduced.RequiresItemsEvaluationTracking()
            ? mod.Rt("fresh") + "()"
            : mod.Rt("NOEV");
    }

    /// <summary>True when a sub-instance recursion needs a fresh tracker (the child consults <c>unevaluated*</c>).</summary>
    /// <param name="child">The child type.</param>
    /// <returns>Whether a fresh tracker is required.</returns>
    public static bool NeedsFreshEv(TypeDeclaration child)
    {
        TypeDeclaration reduced = child.ReducedTypeDeclaration().ReducedType;
        return reduced.RequiresPropertyEvaluationTracking() || reduced.RequiresItemsEvaluationTracking();
    }

    /// <summary>Strip a leading '#' from a keyword path modifier (kl carries no '#').</summary>
    /// <param name="pathModifier">The path modifier.</param>
    /// <returns>The path modifier without a leading '#'.</returns>
    public static string StripHash(string pathModifier)
        => pathModifier.Length > 0 && pathModifier[0] == '#' ? pathModifier[1..] : pathModifier;

    /// <summary>The Python fail condition for a numeric bound / multipleOf.</summary>
    /// <param name="op">The required relation operator.</param>
    /// <param name="rawText">The operand's raw JSON number text.</param>
    /// <param name="mod">The module context.</param>
    /// <param name="valueExpr">The Python expression for the value under test (default <c>value</c>; a child expression when inlining).</param>
    /// <returns>The fail condition, or null for an unsupported operator.</returns>
    /// <remarks>
    /// An <b>integer</b> bound is exact as a Python <c>int</c> literal, so it compares natively
    /// (<c>value &lt;= 5</c>) — no per-check <c>Decimal</c> allocation, and still exact against an
    /// <c>int</c>/<c>float</c>/<c>Decimal</c> value (the caller's <c>is_num(value)</c> guard already excludes
    /// <c>bool</c>). A non-integer bound (float / high precision) keeps the exact Decimal <c>cmp</c> path, and
    /// <c>multipleOf</c> always stays on the exact runtime path (native float modulo is lossy).
    /// </remarks>
    public static string? NumericFailCondition(Operator op, string rawText, PyModule mod, string valueExpr = "value")
    {
        if (op == Operator.MultipleOf)
        {
            return $"not {mod.Rt("multiple_of")}({valueExpr}, {Str(rawText)})";
        }

        bool integerBound = IsIntegerLiteral(rawText);
        return op switch
        {
            Operator.GreaterThan => integerBound ? $"{valueExpr} <= {rawText}" : $"{mod.Rt("cmp")}({valueExpr}, {Str(rawText)}) <= 0",
            Operator.GreaterThanOrEquals => integerBound ? $"{valueExpr} < {rawText}" : $"{mod.Rt("cmp")}({valueExpr}, {Str(rawText)}) < 0",
            Operator.LessThan => integerBound ? $"{valueExpr} >= {rawText}" : $"{mod.Rt("cmp")}({valueExpr}, {Str(rawText)}) >= 0",
            Operator.LessThanOrEquals => integerBound ? $"{valueExpr} > {rawText}" : $"{mod.Rt("cmp")}({valueExpr}, {Str(rawText)}) > 0",
            _ => null,
        };
    }

    /// <summary>True when the raw JSON number text is a plain integer (no '.', 'e', 'E') — exact as a Python int.</summary>
    /// <param name="raw">The raw JSON number text.</param>
    /// <returns>Whether it is an integer literal.</returns>
    private static bool IsIntegerLiteral(string raw)
    {
        int i = raw.Length > 0 && raw[0] == '-' ? 1 : 0;
        if (i >= raw.Length)
        {
            return false;
        }

        for (; i < raw.Length; i++)
        {
            if (raw[i] < '0' || raw[i] > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The Python fail condition for a length / count bound (plain integer compare).</summary>
    /// <param name="op">The required relation operator.</param>
    /// <param name="left">The length expression.</param>
    /// <param name="constText">The bound (a small integer literal).</param>
    /// <returns>The fail condition, or null for an unsupported operator.</returns>
    public static string? LengthFailCondition(Operator op, string left, string constText) => op switch
    {
        Operator.GreaterThan => $"{left} <= {constText}",
        Operator.GreaterThanOrEquals => $"{left} < {constText}",
        Operator.LessThan => $"{left} >= {constText}",
        Operator.LessThanOrEquals => $"{left} > {constText}",
        _ => null,
    };

    /// <summary>The reduced member type declarations of a composition (allOf / anyOf / oneOf).</summary>
    /// <typeparam name="TKeyword">The composition keyword type.</typeparam>
    /// <param name="composition">The composition map, or null.</param>
    /// <returns>The reduced member type declarations, in order.</returns>
    public static List<TypeDeclaration> Members<TKeyword>(IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? composition)
        where TKeyword : notnull
    {
        var result = new List<TypeDeclaration>();
        if (composition is null)
        {
            return result;
        }

        foreach (KeyValuePair<TKeyword, IReadOnlyCollection<TypeDeclaration>> kv in composition)
        {
            foreach (TypeDeclaration m in kv.Value)
            {
                result.Add(m.ReducedTypeDeclaration().ReducedType);
            }
        }

        return result;
    }

    /// <summary>A compact Python literal for any JSON value (used by const/enum membership).</summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The Python literal.</returns>
    public static string Literal(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Str(value.GetString() ?? string.Empty),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Null => "None",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.Array => "[" + string.Join(", ", value.EnumerateArray().Select(Literal)) + "]",
        JsonValueKind.Object => "{" + string.Join(", ", value.EnumerateObject().Select(p => Str(p.Name) + ": " + Literal(p.Value))) + "}",
        _ => "None",
    };
}
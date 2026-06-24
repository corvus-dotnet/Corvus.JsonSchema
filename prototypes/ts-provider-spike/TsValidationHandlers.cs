using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// Handlers match on the keyword's CAPABILITY INTERFACES (ICoreTypeValidationKeyword,
// INumberConstantValidationKeyword, ...), never on the keyword text, and read constraints through
// those interfaces (AllowedCoreTypes, TryGetOperator, TryGetValidationConstants, ...). This is
// vocabulary-independent: one handler serves draft 4/6/7/2019-09/2020-12 even where the keyword name
// or shape differs (e.g. draft-4 boolean exclusiveMinimum maps to the same Operator).
internal interface ITsKeywordEmitter
{
    void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword);
}

internal static class TsEmit
{
    public static string Str(string name) => JsonSerializer.Serialize(name);

    public static string? EvalName(TypeDeclaration t)
        => t.TryGetMetadata<string>("Ts_FinalName", out string? n) && !string.IsNullOrEmpty(n) ? "evaluate" + n : null;

    public static string KindExpr(CoreTypes t) => t switch
    {
        CoreTypes.Object => "(typeof value === \"object\" && value !== null && !Array.isArray(value))",
        CoreTypes.Array => "Array.isArray(value)",
        CoreTypes.String => "typeof value === \"string\"",
        CoreTypes.Number => "typeof value === \"number\"",
        CoreTypes.Integer => "(typeof value === \"number\" && Number.isInteger(value))",
        CoreTypes.Boolean => "typeof value === \"boolean\"",
        CoreTypes.Null => "value === null",
        _ => "true",
    };

    // The TS fail-condition for an operator applied to a left expression and a constant.
    public static string? FailCondition(Operator op, string left, string constText) => op switch
    {
        Operator.GreaterThan => $"{left} <= {constText}",
        Operator.GreaterThanOrEquals => $"{left} < {constText}",
        Operator.LessThan => $"{left} >= {constText}",
        Operator.LessThanOrEquals => $"{left} > {constText}",
        Operator.MultipleOf => $"({left} % {constText}) !== 0",
        _ => null,
    };
}

internal sealed class TsTypeHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 100;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ICoreTypeValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        CoreTypes ct = ((ICoreTypeValidationKeyword)keyword).AllowedCoreTypes(td);
        if (ct == CoreTypes.None || ct == CoreTypes.Any)
        {
            return;
        }

        var kinds = new List<string>();
        if (ct.HasFlag(CoreTypes.Object)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Object)); }
        if (ct.HasFlag(CoreTypes.Array)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Array)); }
        if (ct.HasFlag(CoreTypes.String)) { kinds.Add(TsEmit.KindExpr(CoreTypes.String)); }
        if (ct.HasFlag(CoreTypes.Number)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Number)); }
        else if (ct.HasFlag(CoreTypes.Integer)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Integer)); }
        if (ct.HasFlag(CoreTypes.Boolean)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Boolean)); }
        if (ct.HasFlag(CoreTypes.Null)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Null)); }

        if (kinds.Count > 0)
        {
            sb.Append("  if (!(").Append(string.Join(" || ", kinds)).Append(")) { return false; }\n");
        }
    }
}

// minimum/maximum/exclusive*/multipleOf + minLength/maxLength + minItems/maxItems + min/maxProperties:
// all expose a constant + an Operator, so one handler covers them all across every draft.
internal sealed class TsConstantBoundHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword)
        => keyword is INumberConstantValidationKeyword or IStringLengthConstantValidationKeyword
            or IArrayLengthConstantValidationKeyword or IPropertyCountConstantValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        Operator op;
        JsonElement[]? consts;
        string guard, left;

        if (keyword is INumberConstantValidationKeyword num)
        {
            if (!num.TryGetOperator(td, out op) || !num.TryGetValidationConstants(td, out consts)) { return; }
            guard = "typeof value === \"number\"";
            left = "value";
        }
        else
        {
            var integer = (IIntegerConstantValidationKeyword)keyword;
            if (!integer.TryGetOperator(td, out op) || !integer.TryGetValidationConstants(td, out consts)) { return; }
            (guard, left) = keyword switch
            {
                IStringLengthConstantValidationKeyword => ("typeof value === \"string\"", "[...value].length"),
                IArrayLengthConstantValidationKeyword => ("Array.isArray(value)", "value.length"),
                _ => ("typeof value === \"object\" && value !== null && !Array.isArray(value)", "Object.keys(value).length"),
            };
        }

        if (consts is null || consts.Length == 0) { return; }
        string? cond = TsEmit.FailCondition(op, left, consts[0].GetRawText());
        if (cond is not null)
        {
            sb.Append("  if (").Append(guard).Append(" && ").Append(cond).Append(") { return false; }\n");
        }
    }
}

// enum + const: "value must be one of these constants" (membership). Both expose the constants via
// IValidationConstantProviderKeyword; bounds (which also carry a constant) are excluded by matching
// the membership-specific interfaces only.
internal sealed class TsMembershipHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfConstantValidationKeyword or ISingleConstantValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (keyword is not IValidationConstantProviderKeyword provider || !provider.TryGetValidationConstants(td, out JsonElement[]? consts))
        {
            return;
        }

        // an empty constant set (e.g. `enum: []`) emits `allowed = []`, which rejects every value.
        var literals = new List<string>();
        foreach (JsonElement c in consts)
        {
            literals.Add(c.GetRawText());
        }

        sb.Append("  { const allowed: readonly unknown[] = [").Append(string.Join(", ", literals)).Append("]; if (!allowed.some((a) => __eq(value, a))) { return false; } }\n");
    }
}

internal sealed class TsUniqueItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IUniqueItemsArrayValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IUniqueItemsArrayValidationKeyword)keyword).RequiresUniqueItems(td))
        {
            sb.Append("  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { for (let j = i + 1; j < value.length; j++) { if (__eq(value[i], value[j])) { return false; } } } }\n");
        }
    }
}

internal sealed class TsRegexHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IStringRegexValidationProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IValidationRegexProviderKeyword)keyword).TryGetValidationRegularExpressions(td, out IReadOnlyList<string>? regexes))
        {
            foreach (string r in regexes)
            {
                sb.Append("  if (typeof value === \"string\" && !new RegExp(").Append(TsEmit.Str(r)).Append(", \"u\").test(value)) { return false; }\n");
            }
        }
    }
}

internal sealed class TsPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 800;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (td.PropertyDeclarations.Count == 0) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            string key = TsEmit.Str(p.JsonPropertyName);
            string? e = TsEmit.EvalName(p.ReducedPropertyType);
            if (e is not null)
            {
                sb.Append("    if (Object.prototype.hasOwnProperty.call(o, ").Append(key).Append(") && !").Append(e).Append("(o[").Append(key).Append("])) { return false; }\n");
            }
        }

        sb.Append("  }\n");
    }
}

internal sealed class TsRequiredHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 700;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectRequiredPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Array) { return; }
        var names = new List<string>();
        foreach (JsonElement n in val.EnumerateArray())
        {
            if (n.ValueKind == JsonValueKind.String) { names.Add(n.GetString()!); }
        }

        if (names.Count == 0) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        foreach (string name in names)
        {
            sb.Append("    if (!Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(name)).Append(")) { return false; }\n");
        }

        sb.Append("  }\n");
    }
}

internal sealed class TsPatternPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 810;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPatternPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is null) { return; }
        var entries = new List<(string Pattern, string Name)>();
        foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
        {
            foreach (PatternPropertyDeclaration d in kv.Value)
            {
                string? n = TsEmit.EvalName(d.ReducedPatternPropertyType);
                if (n is not null) { entries.Add((d.Pattern, n)); }
            }
        }

        if (entries.Count == 0) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    for (const k of Object.keys(o)) {\n");
        foreach ((string pattern, string name) in entries)
        {
            sb.Append("      if (new RegExp(").Append(TsEmit.Str(pattern)).Append(", \"u\").test(k) && !").Append(name).Append("(o[k])) { return false; }\n");
        }

        sb.Append("    }\n  }\n");
    }
}

internal sealed class TsAdditionalPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 820;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ILocalEvaluatedPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        FallbackObjectPropertyType? fb = td.LocalEvaluatedPropertyType();
        string? name = fb is null ? null : TsEmit.EvalName(fb.ReducedType);
        if (name is null) { return; }

        var known = new List<string>();
        foreach (PropertyDeclaration p in td.PropertyDeclarations) { known.Add(TsEmit.Str(p.JsonPropertyName)); }

        var patterns = new List<string>();
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is not null)
        {
            foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
            {
                foreach (PatternPropertyDeclaration d in kv.Value) { patterns.Add("new RegExp(" + TsEmit.Str(d.Pattern) + ", \"u\")"); }
            }
        }

        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    const known = new Set<string>([").Append(string.Join(", ", known)).Append("]);\n");
        sb.Append("    const patterns: RegExp[] = [").Append(string.Join(", ", patterns)).Append("];\n");
        sb.Append("    for (const k of Object.keys(o)) {\n");
        sb.Append("      if (known.has(k)) { continue; }\n");
        sb.Append("      if (patterns.some((p) => p.test(k))) { continue; }\n");
        sb.Append("      if (!").Append(name).Append("(o[k])) { return false; }\n");
        sb.Append("    }\n  }\n");
    }
}

internal sealed class TsAllOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAllOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        foreach (string e in CompositionEvals.Members(td.AllOfCompositionTypes()))
        {
            sb.Append("  if (!").Append(e).Append("(value)) { return false; }\n");
        }
    }
}

internal sealed class TsAnyOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        var terms = new List<string>();
        foreach (string e in CompositionEvals.Members(td.AnyOfCompositionTypes())) { terms.Add(e + "(value)"); }
        if (terms.Count > 0)
        {
            sb.Append("  if (!(").Append(string.Join(" || ", terms)).Append(")) { return false; }\n");
        }
    }
}

internal sealed class TsOneOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IOneOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        var evals = CompositionEvals.Members(td.OneOfCompositionTypes());
        if (evals.Count > 0)
        {
            sb.Append("  { let c = 0;\n");
            foreach (string e in evals) { sb.Append("    if (").Append(e).Append("(value)) { c++; }\n"); }
            sb.Append("    if (c !== 1) { return false; }\n  }\n");
        }
    }
}

internal static class CompositionEvals
{
    public static List<string> Members<TKeyword>(IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? composition)
        where TKeyword : notnull
    {
        var result = new List<string>();
        if (composition is null) { return result; }
        foreach (KeyValuePair<TKeyword, IReadOnlyCollection<TypeDeclaration>> kv in composition)
        {
            foreach (TypeDeclaration m in kv.Value)
            {
                string? e = TsEmit.EvalName(m.ReducedTypeDeclaration().ReducedType);
                if (e is not null) { result.Add(e); }
            }
        }

        return result;
    }
}

internal sealed class TsPrefixItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ITupleTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // ExplicitTupleType (not TupleType) — the declared prefixItems positions, even when the array
        // also allows additional items (TupleType is null unless the tuple is closed).
        TupleTypeDeclaration? tuple = td.ExplicitTupleType();
        if (tuple is null) { return; }
        sb.Append("  if (Array.isArray(value)) {\n");
        for (int i = 0; i < tuple.ItemsTypes.Length; i++)
        {
            string? e = TsEmit.EvalName(tuple.ItemsTypes[i].ReducedType);
            if (e is not null)
            {
                sb.Append("    if (value.length > ").Append(i).Append(" && !").Append(e).Append("(value[").Append(i).Append("])) { return false; }\n");
            }
        }

        sb.Append("  }\n");
    }
}

internal sealed class TsItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IArrayItemsTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // ExplicitNonTupleItemsType carries the items-beyond-prefix type (incl. the `items:false`
        // never-type that rejects extras); fall back to ArrayItemsType for plain `items:{schema}`.
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        string? e = items is null ? null : TsEmit.EvalName(items.ReducedType);
        if (e is null) { return; }
        int start = td.ExplicitTupleType()?.ItemsTypes.Length ?? 0;
        sb.Append("  if (Array.isArray(value)) { for (let i = ").Append(start).Append("; i < value.length; i++) { if (!").Append(e).Append("(value[i])) { return false; } } }\n");
    }
}

internal sealed class TsPropertyNamesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 830;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyNameSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        SingleSubschemaKeywordTypeDeclaration? pn = td.PropertyNamesSubschemaType();
        string? e = pn is null ? null : TsEmit.EvalName(pn.ReducedType);
        if (e is null) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) { for (const k of Object.keys(value)) { if (!").Append(e).Append("(k)) { return false; } } }\n");
    }
}

internal sealed class TsDependentRequiredHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 720;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectDependentRequiredValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>>? dr = td.DependentRequired();
        if (dr is null) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        foreach (KeyValuePair<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>> kv in dr)
        {
            foreach (DependentRequiredDeclaration d in kv.Value)
            {
                sb.Append("    if (Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(d.JsonPropertyName)).Append(")) {\n");
                foreach (string dep in d.Dependencies)
                {
                    sb.Append("      if (!Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(dep)).Append(")) { return false; }\n");
                }

                sb.Append("    }\n");
            }
        }

        sb.Append("  }\n");
    }
}

internal sealed class TsIfThenElseHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 650;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ITernaryIfValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        SingleSubschemaKeywordTypeDeclaration? ifT = td.IfSubschemaType();
        string? ifE = ifT is null ? null : TsEmit.EvalName(ifT.ReducedType);
        if (ifE is null) { return; }
        SingleSubschemaKeywordTypeDeclaration? thenT = td.ThenSubschemaType();
        string? thenE = thenT is null ? null : TsEmit.EvalName(thenT.ReducedType);
        SingleSubschemaKeywordTypeDeclaration? elseT = td.ElseSubschemaType();
        string? elseE = elseT is null ? null : TsEmit.EvalName(elseT.ReducedType);

        sb.Append("  if (").Append(ifE).Append("(value)) {\n");
        if (thenE is not null) { sb.Append("    if (!").Append(thenE).Append("(value)) { return false; }\n"); }
        sb.Append("  } else {\n");
        if (elseE is not null) { sb.Append("    if (!").Append(elseE).Append("(value)) { return false; }\n"); }
        sb.Append("  }\n");
    }
}

// ---- EXTENSION DEMO: a handler for a capability the base set omits (format is annotation-only).
internal sealed class TsFormatExtensionHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IFormatProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IFormatProviderKeyword)keyword).TryGetFormat(td, out string? format) && format == "email")
        {
            sb.Append("  if (typeof value === \"string\" && !value.includes(\"@\")) { return false; } // EXTENSION: format=email\n");
        }
    }
}

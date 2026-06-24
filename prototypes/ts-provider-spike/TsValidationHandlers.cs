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

    // Does this type need an evaluation tracker threaded (it or an in-place applicator uses unevaluated*)?
    public static bool Tracks(TypeDeclaration t) => t.RequiresPropertyEvaluationTracking() || t.RequiresItemsEvaluationTracking();

    // The tracker argument for a SUB-INSTANCE recursion (a property value / array item is its own scope):
    // a fresh tracker when the child needs one, else the shared no-op tracker.
    public static string SubEv(TypeDeclaration child) => Tracks(child) ? "fresh()" : "NOEV";

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
        // Numeric bounds/multipleOf: evaluated EXACTLY on the number's text via BigInt (§4.1), passing the
        // schema operand's source literal and comparing against String(value) -- no lossy double math.
        if (keyword is INumberConstantValidationKeyword num)
        {
            if (!num.TryGetOperator(td, out Operator nop) || !num.TryGetValidationConstants(td, out JsonElement[]? nconsts) || nconsts.Length == 0)
            {
                return;
            }

            string? ncond = TsEmit.NumericFailCondition(nop, TsEmit.Str(nconsts[0].GetRawText()));
            if (ncond is not null)
            {
                sb.Append("  if (typeof value === \"number\" && ").Append(ncond).Append(") { return false; }\n");
            }

            return;
        }

        // Length/count bounds compare small safe integers -> plain JS is exact and faster.
        var integer = (IIntegerConstantValidationKeyword)keyword;
        if (!integer.TryGetOperator(td, out Operator op) || !integer.TryGetValidationConstants(td, out JsonElement[]? consts) || consts.Length == 0)
        {
            return;
        }

        (string guard, string left) = keyword switch
        {
            IStringLengthConstantValidationKeyword => ("typeof value === \"string\"", "[...value].length"),
            IArrayLengthConstantValidationKeyword => ("Array.isArray(value)", "value.length"),
            _ => ("typeof value === \"object\" && value !== null && !Array.isArray(value)", "Object.keys(value).length"),
        };

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
        // Only LOCAL properties (declared by this schema's own `properties`). Composed properties hoisted
        // from dependentSchemas/allOf/etc. into PropertyDeclarations are validated AND marked by their own
        // subschema validators -- marking them here would wrongly credit them at this level.
        var locals = new List<PropertyDeclaration>();
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            if (p.LocalOrComposed == LocalOrComposed.Local) { locals.Add(p); }
        }

        if (locals.Count == 0) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");

        // Enumerate the instance once; mark each evaluated property's index into the tracker (a no-op on
        // NOEV). Marking is unconditional so that an in-place applicator member credits its evaluations
        // into the parent's tracker (mirrors the C# generator's unconditional AddLocalEvaluatedProperty).
        sb.Append("    const keys = Object.keys(o);\n");
        sb.Append("    for (let i = 0; i < keys.length; i++) {\n      const k = keys[i];\n");
        bool first = true;
        foreach (PropertyDeclaration p in locals)
        {
            string? e = TsEmit.EvalName(p.ReducedPropertyType);
            if (e is null) { continue; }
            sb.Append("      ").Append(first ? "if" : "else if").Append(" (k === ").Append(TsEmit.Str(p.JsonPropertyName))
              .Append(") { if (!").Append(e).Append("(o[k], ").Append(TsEmit.SubEv(p.ReducedPropertyType)).Append(")) { return false; } ev.markProp(i); }\n");
            first = false;
        }

        sb.Append("    }\n  }\n");
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
        sb.Append("    const keys = Object.keys(o);\n    for (let i = 0; i < keys.length; i++) {\n      const k = keys[i];\n");
        foreach ((string pattern, string name) in entries)
        {
            sb.Append("      if (new RegExp(").Append(TsEmit.Str(pattern)).Append(", \"u\").test(k)) { if (!").Append(name).Append("(o[k], NOEV)) { return false; } ev.markProp(i); }\n");
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
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            if (p.LocalOrComposed == LocalOrComposed.Local) { known.Add(TsEmit.Str(p.JsonPropertyName)); }
        }

        var patterns = new List<string>();
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is not null)
        {
            foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
            {
                foreach (PatternPropertyDeclaration d in kv.Value) { patterns.Add("new RegExp(" + TsEmit.Str(d.Pattern) + ", \"u\")"); }
            }
        }

        string subEv = fb is null ? "NOEV" : TsEmit.SubEv(fb.ReducedType);
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    const known = new Set<string>([").Append(string.Join(", ", known)).Append("]);\n");
        sb.Append("    const patterns: RegExp[] = [").Append(string.Join(", ", patterns)).Append("];\n");
        sb.Append("    const keys = Object.keys(o);\n    for (let i = 0; i < keys.length; i++) {\n      const k = keys[i];\n");
        sb.Append("      if (known.has(k)) { continue; }\n");
        sb.Append("      if (patterns.some((p) => p.test(k))) { continue; }\n");
        sb.Append("      if (!").Append(name).Append("(o[k], ").Append(subEv).Append(")) { return false; } ev.markProp(i);\n");
        sb.Append("    }\n  }\n");
    }
}

internal sealed class TsAllOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAllOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // Each allOf member gets its OWN tracker, merged into the parent after it matches. (Not a shared
        // tracker: a member's own unevaluated* must see only its subtree, not a cousin member's.)
        foreach (string e in CompositionEvals.Members(td.AllOfCompositionTypes()))
        {
            sb.Append("  { const t = fresh(); if (!").Append(e).Append("(value, t)) { return false; } ev.mergeProps(t); ev.mergeItems(t); }\n");
        }
    }
}

internal sealed class TsAnyOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        List<string> members = CompositionEvals.Members(td.AnyOfCompositionTypes());
        if (members.Count == 0) { return; }

        // Evaluate every branch and OR-merge each matched branch's evaluations into the tracker (a no-op
        // on NOEV). Always merge -- the owner may not track, but an ancestor reached via in-place
        // applicators might, and the shared tracker carries the evaluations up to it.
        sb.Append("  { let m = false;\n");
        foreach (string e in members)
        {
            sb.Append("    { const t = fresh(); if (").Append(e).Append("(value, t)) { ev.mergeProps(t); ev.mergeItems(t); m = true; } }\n");
        }

        sb.Append("    if (!m) { return false; }\n  }\n");
    }
}

internal sealed class TsOneOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IOneOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        List<string> evals = CompositionEvals.Members(td.OneOfCompositionTypes());
        if (evals.Count == 0) { return; }

        // exactly one branch matches; merge only that branch's evaluations into the tracker.
        sb.Append("  { let c = 0; const acc = fresh();\n");
        foreach (string e in evals)
        {
            sb.Append("    { const t = fresh(); if (").Append(e).Append("(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }\n");
        }

        sb.Append("    if (c !== 1) { return false; }\n    ev.mergeProps(acc); ev.mergeItems(acc);\n  }\n");
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
            if (e is null) { continue; }
            string subEv = TsEmit.SubEv(tuple.ItemsTypes[i].ReducedType);
            sb.Append("    if (value.length > ").Append(i).Append(") { if (!").Append(e).Append("(value[").Append(i).Append("], ").Append(subEv).Append(")) { return false; } ev.markItem(").Append(i).Append("); }\n");
        }

        sb.Append("  }\n");
    }
}

internal sealed class TsItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    // items, but NOT unevaluatedItems (which also implements IArrayItemsTypeProviderKeyword).
    public bool HandlesKeyword(IKeyword keyword) => keyword is IArrayItemsTypeProviderKeyword and not IUnevaluatedArrayItemsTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // ExplicitNonTupleItemsType carries the items-beyond-prefix type (incl. the `items:false`
        // never-type that rejects extras); fall back to ArrayItemsType for plain `items:{schema}`.
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        string? e = items is null ? null : TsEmit.EvalName(items.ReducedType);
        if (e is null) { return; }
        int start = td.ExplicitTupleType()?.ItemsTypes.Length ?? 0;
        string subEv = TsEmit.SubEv(items!.ReducedType);
        sb.Append("  if (Array.isArray(value)) { for (let i = ").Append(start).Append("; i < value.length; i++) { if (!").Append(e).Append("(value[i], ").Append(subEv).Append(")) { return false; } ev.markItem(i); } }\n");
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
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) { for (const k of Object.keys(value)) { if (!").Append(e).Append("(k, NOEV)) { return false; } } }\n");
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

        // `if` is evaluated to select the branch; its evaluations count only when it matches. then/else
        // (the taken branch) is in-place and shares the tracker.
        sb.Append("  { const t = fresh();\n");
        sb.Append("    if (").Append(ifE).Append("(value, t)) {\n");
        sb.Append("      ev.mergeProps(t); ev.mergeItems(t);\n");
        if (thenE is not null) { sb.Append("      { const t2 = fresh(); if (!").Append(thenE).Append("(value, t2)) { return false; } ev.mergeProps(t2); ev.mergeItems(t2); }\n"); }
        sb.Append("    } else {\n");
        if (elseE is not null) { sb.Append("      { const t3 = fresh(); if (!").Append(elseE).Append("(value, t3)) { return false; } ev.mergeProps(t3); ev.mergeItems(t3); }\n"); }
        sb.Append("    }\n  }\n");
    }
}

// dependentSchemas: when a named property is present, the dependent subschema must validate the WHOLE
// instance (an in-place applicator -> share the parent tracker so its evaluations are credited).
internal sealed class TsDependentSchemasHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 610;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyDependentSchemasValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>>? ds = td.DependentSchemasSubschemaTypes();
        if (ds is null) { return; }
        foreach (KeyValuePair<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> kv in ds)
        {
            foreach (DependentSchemaDeclaration d in kv.Value)
            {
                string? e = TsEmit.EvalName(d.ReducedDepdendentSchemaType);
                if (e is null) { continue; }
                sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value) && Object.prototype.hasOwnProperty.call(value, ")
                  .Append(TsEmit.Str(d.JsonPropertyName)).Append(")) { const t = fresh(); if (!").Append(e).Append("(value, t)) { return false; } ev.mergeProps(t); ev.mergeItems(t); }\n");
            }
        }
    }
}

// contains: at least minContains (default 1) and at most maxContains items match the contains subschema.
// Matched items are marked evaluated (so unevaluatedItems credits them).
internal sealed class TsContainsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 520;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IArrayContainsValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!((IArrayContainsValidationKeyword)keyword).TryGetContainsItemType(td, out ArrayItemsTypeDeclaration? it) || it is null) { return; }
        string? e = TsEmit.EvalName(it.ReducedType);
        if (e is null) { return; }

        string min = "1";
        string? max = null;
        foreach (IKeyword k in td.Keywords())
        {
            if (k is IArrayContainsCountConstantValidationKeyword cc && cc.TryGetOperator(td, out Operator op) && cc.TryGetValidationConstants(td, out JsonElement[]? cs) && cs.Length > 0)
            {
                if (op == Operator.GreaterThanOrEquals) { min = cs[0].GetRawText(); }
                else if (op == Operator.LessThanOrEquals) { max = cs[0].GetRawText(); }
            }
        }

        sb.Append("  if (Array.isArray(value)) {\n");
        sb.Append("    let n = 0;\n");
        sb.Append("    for (let i = 0; i < value.length; i++) { if (").Append(e).Append("(value[i], NOEV)) { n++; ev.markItem(i); } }\n");
        sb.Append("    if (n < ").Append(min).Append(") { return false; }\n");
        if (max is not null) { sb.Append("    if (n > ").Append(max).Append(") { return false; }\n"); }
        sb.Append("  }\n");
    }
}

// not: the instance must NOT validate against the subschema.
internal sealed class TsNotHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 620;

    public bool HandlesKeyword(IKeyword keyword) => keyword is INotValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((INotValidationKeyword)keyword).TryGetNotType(td, out ReducedTypeDeclaration? notType) && notType is not null)
        {
            string? e = TsEmit.EvalName(notType.Value.ReducedType);
            if (e is not null)
            {
                // Give the not-subschema its OWN tracker (so its internal unevaluated* works), but never
                // merge it into the parent -- `not` discards its annotations.
                sb.Append("  if (").Append(e).Append("(value, ").Append(TsEmit.SubEv(notType.Value.ReducedType)).Append(")) { return false; }\n");
            }
        }
    }
}

// unevaluatedProperties: every own property NOT already evaluated (locally or by a matched in-place
// applicator) must validate against the fallback. Reads the evaluation tracker (ev), populated by the
// adjacent/applicator handlers that ran earlier (this handler has the highest priority, so it runs last).
internal sealed class TsUnevaluatedPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 900;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ILocalAndAppliedEvaluatedPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        FallbackObjectPropertyType? fb = td.LocalAndAppliedEvaluatedPropertyType();
        string? name = fb is null ? null : TsEmit.EvalName(fb.ReducedType);
        if (name is null) { return; }
        string subEv = TsEmit.SubEv(fb!.ReducedType);
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    const keys = Object.keys(o);\n");
        sb.Append("    for (let i = 0; i < keys.length; i++) {\n");
        sb.Append("      if (!ev.hasProp(i) && !").Append(name).Append("(o[keys[i]], ").Append(subEv).Append(")) { return false; }\n");
        sb.Append("      ev.markProp(i);\n");
        sb.Append("    }\n  }\n");
    }
}

// unevaluatedItems: every item NOT already evaluated must validate against the fallback.
internal sealed class TsUnevaluatedItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 900;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IUnevaluatedArrayItemsTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        ArrayItemsTypeDeclaration? items = td.ExplicitUnevaluatedItemsType();
        string? name = items is null ? null : TsEmit.EvalName(items.ReducedType);
        if (name is null) { return; }
        string subEv = TsEmit.SubEv(items!.ReducedType);
        sb.Append("  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!ev.hasItem(i) && !").Append(name).Append("(value[i], ").Append(subEv).Append(")) { return false; } ev.markItem(i); } }\n");
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

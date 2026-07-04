// <copyright file="TsObjectPropertiesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsObjectPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 800;

    // Fused: properties + patternProperties + additionalProperties are evaluated in ONE for..in pass (as
    // the C# generator does), not one loop each. Registered for all three keyword interfaces; emits once
    // per type -- on the "anchor" keyword (the first present) -- so the dispatch invocations collapse to
    // a single loop.
    public bool HandlesKeyword(IKeyword keyword)
        => keyword is IObjectPropertyValidationKeyword or IObjectPatternPropertyValidationKeyword or ILocalEvaluatedPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // LOCAL `properties` declarations -- but ONLY when the type actually has a `properties` keyword.
        // PropertyDeclarations also surfaces required-only names (no local schema), which must NOT be validated
        // or marked here -- they are matched by patternProperties/additionalProperties instead. (Composed
        // properties hoisted from allOf/dependentSchemas are validated + marked by their own subschema
        // validators, so they're excluded by the Local check too.)
        bool hasPropsKw = false;
        foreach (IKeyword kw in td.Keywords())
        {
            if (kw is IObjectPropertyValidationKeyword) { hasPropsKw = true; break; }
        }

        var locals = new List<PropertyDeclaration>();
        if (hasPropsKw)
        {
            foreach (PropertyDeclaration p in td.PropertyDeclarations)
            {
                if (p.LocalOrComposed == LocalOrComposed.Local && TsEmit.EvalName(p.ReducedPropertyType) is not null) { locals.Add(p); }
            }
        }

        var patterns = new List<(string Pattern, string Name, string SubEv, string PathMod)>();
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is not null)
        {
            foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
            {
                foreach (PatternPropertyDeclaration d in kv.Value)
                {
                    string? n = TsEmit.EvalName(d.ReducedPatternPropertyType);
                    if (n is not null) { patterns.Add((d.Pattern, n, TsEmit.SubEv(d.ReducedPatternPropertyType), d.KeywordPathModifier)); }
                }
            }
        }

        FallbackObjectPropertyType? fb = td.LocalEvaluatedPropertyType();
        string? addName = fb is null ? null : TsEmit.EvalName(fb.ReducedType);
        string addSubEv = fb is null ? "NOEV" : TsEmit.SubEv(fb.ReducedType);

        bool hasProps = locals.Count > 0;
        bool hasPattern = patterns.Count > 0;
        bool hasAdditional = addName is not null;
        if (!hasProps && !hasPattern && !hasAdditional) { return; }

        // Emit once: only on the anchor keyword (the first present among properties/patternProperties/additional).
        bool isAnchor = hasProps ? keyword is IObjectPropertyValidationKeyword
                      : hasPattern ? keyword is IObjectPatternPropertyValidationKeyword
                      : keyword is ILocalEvaluatedPropertyValidationKeyword;
        if (!isAnchor) { return; }

        // for..in own-key enumeration (allocation-free; no hasOwn -- a JSON instance owns all its for..in
        // keys, differing only under a polluted Object.prototype, the host's concern). properties and
        // patternProperties BOTH apply to a matching key (patternProperties is a fresh `if`, not `else if`);
        // additionalProperties applies ONLY to keys matched by neither (tracked via `m`). markProp per
        // applied key (a no-op on NOEV; unconditional so an in-place applicator member credits the parent).
        bool needMatched = hasAdditional && (hasProps || hasPattern);
        sb.Append("  if (__isObj(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    let i = -1;\n");
        sb.Append("    for (const k in o) {\n      i++;\n");
        if (needMatched) { sb.Append("      let m = false;\n"); }
        bool first = true;
        foreach (PropertyDeclaration p in locals)
        {
            sb.Append("      ").Append(first ? "if" : "else if").Append(" (k === ").Append(TsEmit.Str(p.JsonPropertyName))
              .Append(") { if (!").Append(TsEmit.EvalName(p.ReducedPropertyType)).Append("(o[k], ").Append(TsEmit.SubEv(p.ReducedPropertyType)).Append(TsEmit.ChildKey(p.KeywordPathModifier))
              .Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markProp(i);").Append(needMatched ? " m = true;" : string.Empty).Append(" }\n");
            first = false;
        }

        foreach ((string pattern, string name, string subEv, string pathMod) in patterns)
        {
            sb.Append("      if (__re(").Append(TsEmit.Str(pattern)).Append(").test(k)) { if (!").Append(name).Append("(o[k], ").Append(subEv).Append(TsEmit.ChildKey(pathMod))
              .Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markProp(i);").Append(needMatched ? " m = true;" : string.Empty).Append(" }\n");
        }

        if (hasAdditional)
        {
            if (needMatched)
            {
                sb.Append("      if (!m) { if (!").Append(addName!).Append("(o[k], ").Append(addSubEv).Append(TsEmit.ChildKey(fb!.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markProp(i); }\n");
            }
            else
            {
                sb.Append("      if (!").Append(addName!).Append("(o[k], ").Append(addSubEv).Append(TsEmit.ChildKey(fb!.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markProp(i);\n");
            }
        }

        sb.Append("    }\n  }\n");
    }
}
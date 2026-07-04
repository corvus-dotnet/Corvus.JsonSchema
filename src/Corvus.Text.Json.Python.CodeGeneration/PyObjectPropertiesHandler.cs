// <copyright file="PyObjectPropertiesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// Fused: `properties` + `patternProperties` + `additionalProperties` are evaluated in ONE key pass (as the
// C#/TS generators do). Registered for all three keyword interfaces; emits once, on the anchor keyword (the
// first present), so the dispatch invocations collapse to a single loop. A member whose type has no generated
// validator is skipped (mirrors the TS `EvalName(...) is not null` guards).
//
// Exact-name `properties` dispatch uses a module-level `_PROPS` table (name -> (module, needs-fresh, path))
// looked up with `.get(_k)` — O(1) per key — instead of an O(#props) `if/elif` chain, the C# generator's
// "dictionary trick" (TypeScript found it a wash in V8; this is the CPython A/B). The table stores the sibling
// MODULE object (not its `evaluate_typed`) so building it at import is safe under a circular schema; the
// function is resolved at call time.
internal sealed class PyObjectPropertiesHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 800;

    public bool HandlesKeyword(IKeyword keyword)
        => keyword is IObjectPropertyValidationKeyword or IObjectPatternPropertyValidationKeyword or ILocalEvaluatedPropertyValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        bool hasPropsKeyword = false;
        foreach (IKeyword kw in td.Keywords())
        {
            if (kw is IObjectPropertyValidationKeyword)
            {
                hasPropsKeyword = true;
                break;
            }
        }

        var locals = new List<PropertyDeclaration>();
        if (hasPropsKeyword)
        {
            foreach (PropertyDeclaration p in td.PropertyDeclarations)
            {
                if (p.LocalOrComposed == LocalOrComposed.Local && PyEmit.IsGenerated(p.ReducedPropertyType))
                {
                    locals.Add(p);
                }
            }
        }

        var patterns = new List<(string Pattern, TypeDeclaration Type, string PathMod)>();
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is not null)
        {
            foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
            {
                foreach (PatternPropertyDeclaration d in kv.Value)
                {
                    if (PyEmit.IsGenerated(d.ReducedPatternPropertyType))
                    {
                        patterns.Add((d.Pattern, d.ReducedPatternPropertyType, d.KeywordPathModifier));
                    }
                }
            }
        }

        FallbackObjectPropertyType? fb = td.LocalEvaluatedPropertyType();
        bool hasAdditional = fb is not null && PyEmit.IsGenerated(fb.ReducedType);
        bool hasProps = locals.Count > 0;
        bool hasPattern = patterns.Count > 0;
        if (!hasProps && !hasPattern && !hasAdditional)
        {
            return;
        }

        // Emit once: only on the anchor keyword (the first present among properties/patternProperties/additional).
        bool isAnchor = hasProps ? keyword is IObjectPropertyValidationKeyword
                      : hasPattern ? keyword is IObjectPatternPropertyValidationKeyword
                      : keyword is ILocalEvaluatedPropertyValidationKeyword;
        if (!isAnchor)
        {
            return;
        }

        // `properties` and `patternProperties` both apply to a matching key (patternProperties is a fresh
        // `if`, not `elif`); `additionalProperties` applies ONLY to keys matched by neither (tracked via `_m`).
        bool needMatched = hasAdditional && (hasProps || hasPattern);

        // Exact-name dispatch: a module-level `_PROPS` table (O(1) `.get`) pays off only for WIDE objects; for
        // a handful of properties the plain `if/elif` chain is faster (the A/B crossover in CPython — the dict's
        // hash + subscript costs more than 1-2 interned-string compares). Threshold picked from the py-bench A/B.
        const int dictThreshold = 8;
        bool useDict = locals.Count >= dictThreshold;

        // Module-level dispatch table for the exact-name properties (wide objects only). The table is named per
        // type and stores each property's validator FUNCTION directly (single file — no module indirection, and
        // self-recursion is just the current type's own function).
        string propsTable = "_PROPS_" + PythonLanguageProvider.ModuleNameOf(td);
        bool anyFresh = false;
        if (hasProps && useDict)
        {
            mod.ModuleLevel.Append(propsTable).Append(" = {\n");
            foreach (PropertyDeclaration p in locals)
            {
                TypeDeclaration reduced = p.ReducedPropertyType.ReducedTypeDeclaration().ReducedType;
                bool needsFresh = PyEmit.NeedsFreshEv(p.ReducedPropertyType);
                anyFresh |= needsFresh;
                mod.ModuleLevel.Append("    ").Append(PyEmit.Str(p.JsonPropertyName)).Append(": (")
                    .Append(PythonLanguageProvider.EvalNameOf(reduced)).Append(", ").Append(needsFresh ? "True" : "False").Append(", ")
                    .Append(PyEmit.Str(PyEmit.StripHash(p.KeywordPathModifier))).Append("),\n");
            }

            mod.ModuleLevel.Append("}\n");
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_obj")).Append("(value):\n");
        mod.Body.Append("        _track = not ev.n\n");
        mod.Body.Append("        _i = -1\n");
        mod.Body.Append("        for _k in value:\n");
        mod.Body.Append("            _i += 1\n");
        if (needMatched)
        {
            mod.Body.Append("            _m = False\n");
        }

        if (hasProps && useDict)
        {
            string subEv = anyFresh
                ? "(" + mod.Rt("fresh") + "() if _p[1] else " + mod.Rt("NOEV") + ")"
                : mod.Rt("NOEV");
            mod.Body.Append("            _p = ").Append(propsTable).Append(".get(_k)\n");
            mod.Body.Append("            if _p is not None:\n");
            PyEmit.EmitDescentCall(mod, "_p[0]", "value[_k]", subEv, PyEmit.PropInstSeg, "_p[2]", 16);
            mod.Body.Append("                if not _c:\n");
            PyEmit.Propagate(mod, 20);
            mod.Body.Append("                if _track: ev.mark_prop(_i)\n");
            if (needMatched)
            {
                mod.Body.Append("                _m = True\n");
            }
        }
        else if (hasProps)
        {
            // Narrow object: a plain `if/elif _k == "name"` chain (below the dict threshold). A property whose
            // type is a trivial scalar assertion is INLINED (`isinstance`/`is_num`/...) instead of a cross-module
            // call — the big saving on flat objects, where the fixed per-property call cost dominates.
            bool first = true;
            foreach (PropertyDeclaration p in locals)
            {
                mod.Body.Append("            ").Append(first ? "if" : "elif").Append(" _k == ").Append(PyEmit.Str(p.JsonPropertyName)).Append(":\n");

                // A flat-scalar property (type + const/enum/bounds/pattern) is INLINED — its checks emitted
                // against `value[_k]` with no cross-function call (the big saving on flat objects, where the
                // fixed per-property call cost dominates). Anything structural falls back to a normal descent.
                if (PyEmit.TryEmitFlatScalarInline(mod, p.ReducedPropertyType, "value[_k]", PyEmit.PropInstSeg, PyEmit.Str(PyEmit.StripHash(p.KeywordPathModifier)), 16))
                {
                    mod.Body.Append("                if _track: ev.mark_prop(_i)\n");
                    if (needMatched)
                    {
                        mod.Body.Append("                _m = True\n");
                    }
                }
                else
                {
                    this.Recurse(mod, p.ReducedPropertyType, p.KeywordPathModifier, 16, needMatched);
                }

                first = false;
            }
        }

        foreach ((string pattern, TypeDeclaration type, string pathMod) in patterns)
        {
            mod.Body.Append("            if ").Append(mod.Rt("re_test")).Append("(").Append(PyEmit.Str(EcmaRegexPythonTranslator.Translate(pattern))).Append(", _k):\n");
            this.Recurse(mod, type, pathMod, 16, needMatched);
        }

        if (hasAdditional)
        {
            if (needMatched)
            {
                mod.Body.Append("            if not _m:\n");
                this.Recurse(mod, fb!.ReducedType, fb.KeywordPathModifier, 16, mark: false);
            }
            else
            {
                this.Recurse(mod, fb!.ReducedType, fb.KeywordPathModifier, 12, mark: false);
            }
        }
    }

    // Emit a lazy-location descent into {child}(value[_k], SubEv, r) then propagate + `ev.mark_prop(_i)`
    // (optionally setting `_m`), at the given base indent. Used by patternProperties/additionalProperties (the
    // exact-name properties go through the `_PROPS` dispatch table instead). The child is generated, so EvalRef
    // is non-null.
    private void Recurse(PyModule mod, TypeDeclaration childType, string pathModifier, int indent, bool mark)
    {
        string evalRef = PyEmit.EvalRef(mod, childType)!;
        string pad = new(' ', indent);
        PyEmit.EmitDescentCall(mod, evalRef, "value[_k]", PyEmit.SubEv(mod, childType), PyEmit.PropInstSeg, PyEmit.Str(PyEmit.StripHash(pathModifier)), indent);
        mod.Body.Append(pad).Append("if not _c:\n");
        PyEmit.Propagate(mod, indent + 4);
        mod.Body.Append(pad).Append("if _track: ev.mark_prop(_i)\n");
        if (mark)
        {
            mod.Body.Append(pad).Append("_m = True\n");
        }
    }
}
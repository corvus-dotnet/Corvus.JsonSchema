// <copyright file="TsUnevaluatedPropertiesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

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
        sb.Append("  if (__isObj(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    let i = -1;\n");
        sb.Append("    for (const k in o) {\n      i++;\n");
        sb.Append("      if (!ev.hasProp(i) && !").Append(name).Append("(o[k], ").Append(subEv).Append(TsEmit.ChildKey(fb!.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" }\n");
        sb.Append("      ev.markProp(i);\n");
        sb.Append("    }\n  }\n");
    }
}
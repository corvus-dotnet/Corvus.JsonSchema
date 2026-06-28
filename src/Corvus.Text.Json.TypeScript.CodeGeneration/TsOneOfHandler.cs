// <copyright file="TsOneOfHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsOneOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IOneOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        List<string> evals = CompositionEvals.Members(td.OneOfCompositionTypes());
        if (evals.Count == 0) { return; }

        // exactly one branch matches; merge only that branch's evaluations into the tracker.
        //
        // Detailed mode (r !== null): each branch gets its OWN sub-collector (rb) so a failed disjunction can
        // surface the per-branch sub-failures. We buffer ONLY failed branches' sub-collectors. When the
        // disjunction fails with c === 0 (all branches failed) those buffered sub-failures explain why, so we
        // merge them in addition to the composite /oneOf failure. When c > 1 (over-match) the failed branches'
        // sub-failures are misleading noise (the schema matched too MANY branches, not too few), so we emit the
        // composite ONLY. The boolean hot path (r === null) keeps subs === null and builds no sub-collectors.
        sb.Append("  { let c = 0; const acc = fresh(); const subs = r === null ? null : [];\n");
        int i = 0;
        foreach (string e in evals)
        {
            string klExpr = "(rb === null ? kl : kl + \"/oneOf/" + i + "\")";
            sb.Append("    { const t = fresh(); const rb = subs === null ? null : new Results(r.verbose); if (").Append(e).Append("(value, t, il, ").Append(klExpr).Append(", rb)) { c++; acc.mergeProps(t); acc.mergeItems(t); } else if (rb !== null) { subs.push(rb); } }\n");
            i++;
        }

        sb.Append("    if (c !== 1) { if (r === null) return false; if (subs !== null && c === 0) { for (const s of subs) { r.merge(s); } } ").Append(TsEmit.FailRecord(td, keyword.Keyword)).Append(" ok = false; }\n    ev.mergeProps(acc); ev.mergeItems(acc);\n  }\n");
    }
}
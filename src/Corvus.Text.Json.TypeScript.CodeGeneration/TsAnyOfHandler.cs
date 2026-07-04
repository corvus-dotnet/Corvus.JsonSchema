// <copyright file="TsAnyOfHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

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
        //
        // Detailed mode (r !== null): each branch gets its OWN sub-collector (rb) so that, when the
        // disjunction fails (no branch matched), every branch's sub-failures are surfaced (merged) in
        // addition to the composite /anyOf failure. The boolean hot path (r === null) keeps rb === null,
        // builds no sub-collectors and no kl concatenation, and short-circuits on the first match.
        // subs is typed (not an inferred never[]) and rb is gated on `r`, so the per-branch null-checks
        // correlate for a strict consumer (strictNullChecks / exactOptionalPropertyTypes).
        sb.Append("  { let m = false; const subs: Results[] | null = r === null ? null : [];\n");
        int i = 0;
        foreach (string e in members)
        {
            string klExpr = "(rb === null ? kl : kl + \"/anyOf/" + i + "\")";
            sb.Append("    { const t = fresh(); const rb = r === null ? null : new Results(r.verbose); if (").Append(e).Append("(value, t, il, ").Append(klExpr).Append(", rb)) { ev.mergeProps(t); ev.mergeItems(t); m = true; } else if (rb !== null && subs !== null) { subs.push(rb); } }\n");
            i++;
        }

        sb.Append("    if (!m) { if (r === null) return false; if (subs !== null) { for (const s of subs) { r.merge(s); } } ").Append(TsEmit.FailRecord(td, keyword.Keyword)).Append(" ok = false; }\n  }\n");
    }
}
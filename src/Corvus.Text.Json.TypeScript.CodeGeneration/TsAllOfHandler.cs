// <copyright file="TsAllOfHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsAllOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAllOfSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // Each allOf member gets its OWN tracker, merged into the parent after it matches. (Not a shared
        // tracker: a member's own unevaluated* must see only its subtree, not a cousin member's.) The member
        // records its own failures; kv.Key.GetPathModifier(member, i) is the path TAKEN into it (#/allOf/<i>,
        // plus the member's ReducedPathModifier — i.e. /$ref when the member is a reference).
        var allOfComposition = td.AllOfCompositionTypes();
        if (allOfComposition is null) { return; }
        foreach (var kv in allOfComposition)
        {
            int i = 0;
            foreach (TypeDeclaration m in kv.Value)
            {
                var rtd = m.ReducedTypeDeclaration();
                string? e = TsEmit.EvalName(rtd.ReducedType);
                if (e is not null)
                {
                    sb.Append("  { const t = fresh(); if (!").Append(e).Append("(value, t").Append(TsEmit.ChildValue(kv.Key.GetPathModifier(rtd, i))).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.mergeProps(t); ev.mergeItems(t); }\n");
                }

                i++;
            }
        }
    }
}
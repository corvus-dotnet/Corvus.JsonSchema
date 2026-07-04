// <copyright file="TsIfThenElseHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

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
        if (thenE is not null) { sb.Append("      { const t2 = fresh(); if (!").Append(thenE).Append("(value, t2").Append(TsEmit.ChildValue(thenT!.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.mergeProps(t2); ev.mergeItems(t2); }\n"); }
        sb.Append("    } else {\n");
        if (elseE is not null) { sb.Append("      { const t3 = fresh(); if (!").Append(elseE).Append("(value, t3").Append(TsEmit.ChildValue(elseT!.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.mergeProps(t3); ev.mergeItems(t3); }\n"); }
        sb.Append("    }\n  }\n");
    }
}
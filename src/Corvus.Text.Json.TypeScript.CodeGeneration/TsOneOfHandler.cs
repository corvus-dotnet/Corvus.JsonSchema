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
        sb.Append("  { let c = 0; const acc = fresh();\n");
        foreach (string e in evals)
        {
            sb.Append("    { const t = fresh(); if (").Append(e).Append("(value, t)) { c++; acc.mergeProps(t); acc.mergeItems(t); } }\n");
        }

        sb.Append("    if (c !== 1) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n    ev.mergeProps(acc); ev.mergeItems(acc);\n  }\n");
    }
}
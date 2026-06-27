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
        sb.Append("  { let m = false;\n");
        foreach (string e in members)
        {
            sb.Append("    { const t = fresh(); if (").Append(e).Append("(value, t)) { ev.mergeProps(t); ev.mergeItems(t); m = true; } }\n");
        }

        sb.Append("    if (!m) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n  }\n");
    }
}
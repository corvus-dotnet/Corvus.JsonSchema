// <copyright file="TsContainsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

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
        sb.Append("    if (n < ").Append(min).Append(") { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
        if (max is not null) { sb.Append("    if (n > ").Append(max).Append(") { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n"); }
        sb.Append("  }\n");
    }
}
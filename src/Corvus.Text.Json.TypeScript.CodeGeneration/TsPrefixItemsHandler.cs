// <copyright file="TsPrefixItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsPrefixItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ITupleTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // ExplicitTupleType (not TupleType) — the declared prefixItems positions, even when the array
        // also allows additional items (TupleType is null unless the tuple is closed).
        TupleTypeDeclaration? tuple = td.ExplicitTupleType();
        if (tuple is null) { return; }
        sb.Append("  if (Array.isArray(value)) {\n");
        for (int i = 0; i < tuple.ItemsTypes.Length; i++)
        {
            string? e = TsEmit.EvalName(tuple.ItemsTypes[i].ReducedType);
            if (e is null) { continue; }
            string subEv = TsEmit.SubEv(tuple.ItemsTypes[i].ReducedType);
            sb.Append("    if (value.length > ").Append(i).Append(") { if (!").Append(e).Append("(value[").Append(i).Append("], ").Append(subEv).Append(TsEmit.ChildIdx(i.ToString(), tuple.Keyword.GetPathModifier(tuple.ItemsTypes[i], i))).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markItem(").Append(i).Append("); }\n");
        }

        sb.Append("  }\n");
    }
}
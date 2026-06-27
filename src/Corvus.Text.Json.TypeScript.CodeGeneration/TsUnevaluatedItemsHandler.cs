// <copyright file="TsUnevaluatedItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// unevaluatedItems: every item NOT already evaluated must validate against the fallback.
internal sealed class TsUnevaluatedItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 900;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IUnevaluatedArrayItemsTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        ArrayItemsTypeDeclaration? items = td.ExplicitUnevaluatedItemsType();
        string? name = items is null ? null : TsEmit.EvalName(items.ReducedType);
        if (name is null) { return; }
        string subEv = TsEmit.SubEv(items!.ReducedType);
        sb.Append("  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { if (!ev.hasItem(i) && !").Append(name).Append("(value[i], ").Append(subEv).Append(TsEmit.ChildIdx("i", items!.Keyword.GetPathModifier(items!))).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markItem(i); } }\n");
    }
}
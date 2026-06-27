// <copyright file="TsItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    // items, but NOT unevaluatedItems (which also implements IArrayItemsTypeProviderKeyword).
    public bool HandlesKeyword(IKeyword keyword) => keyword is IArrayItemsTypeProviderKeyword and not IUnevaluatedArrayItemsTypeProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // ExplicitNonTupleItemsType carries the items-beyond-prefix type (incl. the `items:false`
        // never-type that rejects extras); fall back to ArrayItemsType for plain `items:{schema}`.
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        string? e = items is null ? null : TsEmit.EvalName(items.ReducedType);
        if (e is null) { return; }
        int start = td.ExplicitTupleType()?.ItemsTypes.Length ?? 0;
        string subEv = TsEmit.SubEv(items!.ReducedType);
        sb.Append("  if (Array.isArray(value)) { for (let i = ").Append(start).Append("; i < value.length; i++) { if (!").Append(e).Append("(value[i], ").Append(subEv).Append(TsEmit.ChildIdx("i", items!.Keyword.GetPathModifier(items!))).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.markItem(i); } }\n");
    }
}
// <copyright file="TsPropertyNamesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsPropertyNamesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 830;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyNameSubschemaValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        SingleSubschemaKeywordTypeDeclaration? pn = td.PropertyNamesSubschemaType();
        string? e = pn is null ? null : TsEmit.EvalName(pn.ReducedType);
        if (e is null) { return; }
        sb.Append("  if (__isObj(value)) { for (const k in value) { if (!").Append(e).Append("(k, NOEV").Append(TsEmit.ChildKey(pn!.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" } } }\n");
    }
}
// <copyright file="TsNotHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// not: the instance must NOT validate against the subschema.
internal sealed class TsNotHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 620;

    public bool HandlesKeyword(IKeyword keyword) => keyword is INotValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((INotValidationKeyword)keyword).TryGetNotType(td, out ReducedTypeDeclaration? notType) && notType is not null)
        {
            string? e = TsEmit.EvalName(notType.Value.ReducedType);
            if (e is not null)
            {
                // Give the not-subschema its OWN tracker (so its internal unevaluated* works), but never
                // merge it into the parent -- `not` discards its annotations.
                sb.Append("  if (").Append(e).Append("(value, ").Append(TsEmit.SubEv(notType.Value.ReducedType)).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
            }
        }
    }
}
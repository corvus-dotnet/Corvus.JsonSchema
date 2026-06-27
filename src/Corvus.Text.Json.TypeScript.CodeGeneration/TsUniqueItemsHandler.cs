// <copyright file="TsUniqueItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsUniqueItemsHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IUniqueItemsArrayValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IUniqueItemsArrayValidationKeyword)keyword).RequiresUniqueItems(td))
        {
            sb.Append("  if (Array.isArray(value)) { for (let i = 0; i < value.length; i++) { for (let j = i + 1; j < value.length; j++) { if (__eq(value[i], value[j])) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" } } } }\n");
        }
    }
}
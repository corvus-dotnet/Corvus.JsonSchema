// <copyright file="TsFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// format assertion (design §5.5): every standard format validated on the string via the __fmt runtime.
// Only registered by CreateWithFormatAssertion (the optional/format suite); the default leaves format an
// annotation. Non-strings are always valid; unknown formats are not asserted (__fmt returns true).
internal sealed class TsFormatHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 550;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IFormatProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IFormatProviderKeyword)keyword).TryGetFormat(td, out string? format) && !string.IsNullOrEmpty(format))
        {
            sb.Append("  if (typeof value === \"string\" && !__fmt(").Append(TsEmit.Str(format!)).Append(", value)) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
        }
    }
}
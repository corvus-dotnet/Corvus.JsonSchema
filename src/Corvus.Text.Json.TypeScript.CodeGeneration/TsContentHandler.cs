// <copyright file="TsContentHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// content assertion: contentEncoding (base64) + contentMediaType (application/json). Matches the
// contentMediaType keyword and reads both via the accessors; annotation-only unless asserted.
internal sealed class TsContentHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 560;

    // Match contentMediaType OR contentEncoding (a schema may have only one). Reading both via the
    // accessors means the emitted check is identical for either keyword; if both are present it is emitted
    // twice (redundant but correct).
    public bool HandlesKeyword(IKeyword keyword) => keyword is IContentMediaTypeValidationKeyword or IContentEncodingValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        string? media = td.ExplicitContentMediaType();
        string? enc = td.ExplicitContentEncoding();
        if (media is null && enc is null) { return; }
        sb.Append("  if (typeof value === \"string\" && !__fmtContent(value, ")
          .Append(enc is null ? "null" : TsEmit.Str(enc)).Append(", ")
          .Append(media is null ? "null" : TsEmit.Str(media)).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
    }
}
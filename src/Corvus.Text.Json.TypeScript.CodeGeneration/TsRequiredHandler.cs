// <copyright file="TsRequiredHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsRequiredHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 700;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectRequiredPropertyValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Array) { return; }
        var names = new List<string>();
        foreach (JsonElement n in val.EnumerateArray())
        {
            if (n.ValueKind == JsonValueKind.String) { names.Add(n.GetString()!); }
        }

        if (names.Count == 0) { return; }
        sb.Append("  if (__isObj(value)) {\n");
        foreach (string name in names)
        {
            sb.Append("    if (!Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(name)).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
        }

        sb.Append("  }\n");
    }
}
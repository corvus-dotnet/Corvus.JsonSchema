// <copyright file="TsRegexHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsRegexHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IStringRegexValidationProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IValidationRegexProviderKeyword)keyword).TryGetValidationRegularExpressions(td, out IReadOnlyList<string>? regexes))
        {
            foreach (string r in regexes)
            {
                sb.Append("  if (typeof value === \"string\" && !__re(").Append(TsEmit.Str(r)).Append(").test(value)) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
            }
        }
    }
}
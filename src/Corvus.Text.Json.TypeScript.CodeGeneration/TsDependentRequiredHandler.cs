// <copyright file="TsDependentRequiredHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsDependentRequiredHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 720;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectDependentRequiredValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>>? dr = td.DependentRequired();
        if (dr is null) { return; }
        sb.Append("  if (__isObj(value)) {\n");
        foreach (KeyValuePair<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>> kv in dr)
        {
            foreach (DependentRequiredDeclaration d in kv.Value)
            {
                sb.Append("    if (Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(d.JsonPropertyName)).Append(")) {\n");
                foreach (string dep in d.Dependencies)
                {
                    sb.Append("      if (!Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(dep)).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
                }

                sb.Append("    }\n");
            }
        }

        sb.Append("  }\n");
    }
}
// <copyright file="TsMembershipHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// enum + const: "value must be one of these constants" (membership). Both expose the constants via
// IValidationConstantProviderKeyword; bounds (which also carry a constant) are excluded by matching
// the membership-specific interfaces only.
internal sealed class TsMembershipHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfConstantValidationKeyword or ISingleConstantValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (keyword is not IValidationConstantProviderKeyword provider || !provider.TryGetValidationConstants(td, out JsonElement[]? consts))
        {
            return;
        }

        // an empty constant set (e.g. `enum: []`) emits `allowed = []`, which rejects every value.
        var literals = new List<string>();
        foreach (JsonElement c in consts)
        {
            literals.Add(c.GetRawText());
        }

        sb.Append("  { const allowed: readonly unknown[] = [").Append(string.Join(", ", literals)).Append("]; if (!allowed.some((a) => __eq(value, a))) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" } }\n");
    }
}
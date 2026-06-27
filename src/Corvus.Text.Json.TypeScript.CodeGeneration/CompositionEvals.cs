// <copyright file="CompositionEvals.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal static class CompositionEvals
{
    public static List<string> Members<TKeyword>(IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? composition)
        where TKeyword : notnull
    {
        var result = new List<string>();
        if (composition is null) { return result; }
        foreach (KeyValuePair<TKeyword, IReadOnlyCollection<TypeDeclaration>> kv in composition)
        {
            foreach (TypeDeclaration m in kv.Value)
            {
                string? e = TsEmit.EvalName(m.ReducedTypeDeclaration().ReducedType);
                if (e is not null) { result.Add(e); }
            }
        }

        return result;
    }
}
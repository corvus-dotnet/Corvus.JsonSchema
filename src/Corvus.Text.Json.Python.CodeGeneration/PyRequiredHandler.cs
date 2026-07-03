// <copyright file="PyRequiredHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// The `required` keyword: each named property must be present on an object instance.
internal sealed class PyRequiredHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 700;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectRequiredPropertyValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var names = new List<string>();
        foreach (JsonElement n in val.EnumerateArray())
        {
            if (n.ValueKind == JsonValueKind.String)
            {
                names.Add(n.GetString()!);
            }
        }

        if (names.Count == 0)
        {
            return;
        }

        // `is_obj` narrows `value` to a dict for the membership tests (TypeGuard).
        mod.Body.Append("    if ").Append(mod.Rt("is_obj")).Append("(value):\n");
        foreach (string name in names)
        {
            PyEmit.Check(mod, td, keyword.Keyword, PyEmit.Str(name) + " not in value", 8);
        }
    }
}
// <copyright file="PyMembershipHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// enum + const: "value must be one of these constants" (membership), compared by JSON deep-equality.
internal sealed class PyMembershipHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfConstantValidationKeyword or ISingleConstantValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (keyword is not IValidationConstantProviderKeyword provider || !provider.TryGetValidationConstants(td, out JsonElement[]? consts))
        {
            return;
        }

        var literals = new List<string>();
        foreach (JsonElement c in consts)
        {
            literals.Add(PyEmit.Literal(c));
        }

        // An empty constant set (e.g. `enum: []`) rejects every value.
        mod.Body.Append("    _allowed: list[object] = [").Append(string.Join(", ", literals)).Append("]\n");
        PyEmit.Check(mod, td, keyword.Keyword, "not any(" + mod.Rt("eq") + "(value, _a) for _a in _allowed)", 4);
    }
}
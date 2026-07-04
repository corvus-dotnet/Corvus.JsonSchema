// <copyright file="PyUniqueItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// uniqueItems: no two array elements are JSON-deep-equal.
internal sealed class PyUniqueItemsHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IUniqueItemsArrayValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (((IUniqueItemsArrayValidationKeyword)keyword).RequiresUniqueItems(td))
        {
            mod.Body.Append("    if ").Append(mod.Rt("is_arr")).Append("(value):\n");
            mod.Body.Append("        for _i in range(len(value)):\n");
            mod.Body.Append("            for _j in range(_i + 1, len(value)):\n");
            mod.Body.Append("                if ").Append(mod.Rt("eq")).Append("(value[_i], value[_j]):\n");
            PyEmit.FailBody(mod, td, keyword.Keyword, 20);
        }
    }
}
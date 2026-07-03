// <copyright file="PyTypeHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// The `type` keyword (plus OpenAPI `nullable`, which adds CoreTypes.Null): emit the UNION of allowed core
// types once, on the first core-type keyword, so secondary core-type keywords are no-ops here.
internal sealed class PyTypeHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 100;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ICoreTypeValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (!ReferenceEquals(keyword, FirstCoreTypeKeyword(td)))
        {
            return;
        }

        if (PyEmit.CoreTypeCheckExpr(mod, td, "value") is string expr)
        {
            PyEmit.Check(mod, td, keyword.Keyword, expr, 4);
        }
    }

    private static IKeyword? FirstCoreTypeKeyword(TypeDeclaration td)
    {
        foreach (IKeyword keyword in td.Keywords())
        {
            if (keyword is ICoreTypeValidationKeyword)
            {
                return keyword;
            }
        }

        return null;
    }
}
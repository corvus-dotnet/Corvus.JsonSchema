// <copyright file="PyNotHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// not: the instance must NOT validate against the subschema. Evaluated in boolean mode with its own tracker
// (its internal unevaluated* works), never merged into the parent — `not` discards its annotations.
internal sealed class PyNotHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 620;

    public bool HandlesKeyword(IKeyword keyword) => keyword is INotValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (((INotValidationKeyword)keyword).TryGetNotType(td, out ReducedTypeDeclaration? notType)
            && notType is not null
            && PyEmit.EvalRef(mod, notType.Value.ReducedType) is string evalRef)
        {
            string condition = evalRef + "(value, " + PyEmit.SubEv(mod, notType.Value.ReducedType) + ", None)";
            PyEmit.Check(mod, td, keyword.Keyword, condition, 4);
        }
    }
}
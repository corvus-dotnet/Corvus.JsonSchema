// <copyright file="PyUnevaluatedItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// unevaluatedItems: every array item NOT already evaluated must validate against the fallback.
internal sealed class PyUnevaluatedItemsHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 900;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IUnevaluatedArrayItemsTypeProviderKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        ArrayItemsTypeDeclaration? items = td.ExplicitUnevaluatedItemsType();
        if (items is null)
        {
            return;
        }

        if (PyEmit.EvalRef(mod, items.ReducedType) is not string evalRef)
        {
            return;
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_arr")).Append("(value):\n");
        mod.Body.Append("        for _idx in range(len(value)):\n");
        mod.Body.Append("            if not ev.has_item(_idx):\n");
        PyEmit.EmitDescentCall(mod, evalRef, "value[_idx]", PyEmit.SubEv(mod, items.ReducedType), "\"/\" + str(_idx)", PyEmit.Str("/" + keyword.Keyword), 16);
        mod.Body.Append("                if not _c:\n");
        PyEmit.Propagate(mod, 20);
        mod.Body.Append("            ev.mark_item(_idx)\n");
    }
}
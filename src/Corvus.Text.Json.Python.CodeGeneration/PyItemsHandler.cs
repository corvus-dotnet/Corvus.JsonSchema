// <copyright file="PyItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// items (the items-beyond-prefix schema, incl. the `items:false` never-type that rejects extras); NOT
// unevaluatedItems. Every element from the end of the prefix onward must match.
internal sealed class PyItemsHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IArrayItemsTypeProviderKeyword and not IUnevaluatedArrayItemsTypeProviderKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        if (items is null)
        {
            return;
        }

        if (PyEmit.EvalRef(mod, items.ReducedType) is not string evalRef)
        {
            return;
        }

        int start = td.ExplicitTupleType()?.ItemsTypes.Length ?? 0;
        mod.Body.Append("    if ").Append(mod.Rt("is_arr")).Append("(value):\n");
        mod.Body.Append("        _track = not ev.n\n");
        mod.Body.Append("        for _idx in range(").Append(start).Append(", len(value)):\n");
        if (PyEmit.TryEmitFlatScalarInline(mod, items.ReducedType, "value[_idx]", "\"/\" + str(_idx)", PyEmit.Str("/" + keyword.Keyword), 12))
        {
            mod.Body.Append("            if _track: ev.mark_item(_idx)\n");
        }
        else
        {
            PyEmit.EmitDescentCall(mod, evalRef, "value[_idx]", PyEmit.SubEv(mod, items.ReducedType), "\"/\" + str(_idx)", PyEmit.Str("/" + keyword.Keyword), 12);
            mod.Body.Append("            if not _c:\n");
            PyEmit.Propagate(mod, 16);
            mod.Body.Append("            if _track: ev.mark_item(_idx)\n");
        }
    }
}
// <copyright file="PyPrefixItemsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// prefixItems (and the draft-7 array-form `items`): element at each declared position must match that
// position's schema. A position whose schema has no generated validator is skipped (TS `if (e is null) continue`).
internal sealed class PyPrefixItemsHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 850;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ITupleTypeProviderKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        TupleTypeDeclaration? tuple = td.ExplicitTupleType();
        if (tuple is null || tuple.ItemsTypes.Length == 0)
        {
            return;
        }

        var positions = new List<int>();
        for (int i = 0; i < tuple.ItemsTypes.Length; i++)
        {
            if (PyEmit.IsGenerated(tuple.ItemsTypes[i].ReducedType))
            {
                positions.Add(i);
            }
        }

        if (positions.Count == 0)
        {
            return;
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_arr")).Append("(value):\n");
        mod.Body.Append("        _track = not ev.n\n");
        foreach (int i in positions)
        {
            TypeDeclaration itemType = tuple.ItemsTypes[i].ReducedType;
            string evalRef = PyEmit.EvalRef(mod, itemType)!;
            mod.Body.Append("        if len(value) > ").Append(i).Append(":\n");
            PyEmit.EmitDescentCall(mod, evalRef, "value[" + i + "]", PyEmit.SubEv(mod, itemType), PyEmit.Str("/" + i), PyEmit.Str("/prefixItems/" + i), 12);
            mod.Body.Append("            if not _c:\n");
            PyEmit.Propagate(mod, 16);
            mod.Body.Append("            if _track: ev.mark_item(").Append(i).Append(")\n");
        }
    }
}
// <copyright file="PyPropertyNamesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// propertyNames: every property NAME (a string) must validate against the subschema.
internal sealed class PyPropertyNamesHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 830;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyNameSubschemaValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        SingleSubschemaKeywordTypeDeclaration? pn = td.PropertyNamesSubschemaType();
        if (pn is null)
        {
            return;
        }

        if (PyEmit.EvalRef(mod, pn.ReducedType) is not string evalRef)
        {
            return;
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_obj")).Append("(value):\n");
        mod.Body.Append("        for _k in value:\n");
        PyEmit.EmitDescentCall(mod, evalRef, "_k", PyEmit.SubEv(mod, pn.ReducedType), PyEmit.PropInstSeg, PyEmit.Str("/" + keyword.Keyword), 12);
        mod.Body.Append("            if not _c:\n");
        PyEmit.Propagate(mod, 16);
    }
}
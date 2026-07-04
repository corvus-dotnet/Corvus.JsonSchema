// <copyright file="PyDependentSchemasHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// dependentSchemas: when a named property is present, the dependent subschema must validate the WHOLE
// instance (an in-place applicator, so its evaluations are merged into the parent tracker).
internal sealed class PyDependentSchemasHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 610;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyDependentSchemasValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>>? ds = td.DependentSchemasSubschemaTypes();
        if (ds is null)
        {
            return;
        }

        foreach (KeyValuePair<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> kv in ds)
        {
            foreach (DependentSchemaDeclaration d in kv.Value)
            {
                if (PyEmit.EvalRef(mod, d.ReducedDepdendentSchemaType) is not string evalRef)
                {
                    continue;
                }

                mod.Body.Append("    if ").Append(mod.Rt("is_obj")).Append("(value) and ").Append(PyEmit.Str(d.JsonPropertyName)).Append(" in value:\n");
                mod.Body.Append("        _dst = ").Append(mod.Rt("fresh")).Append("()\n");
                PyEmit.EmitDescentCall(mod, evalRef, "value", "_dst", "\"\"", PyEmit.Str(PyEmit.StripHash(d.KeywordPathModifier)), 8);
                mod.Body.Append("        if not _c:\n");
                PyEmit.Propagate(mod, 12);
                mod.Body.Append("        ev.merge_props(_dst)\n        ev.merge_items(_dst)\n");
            }
        }
    }
}
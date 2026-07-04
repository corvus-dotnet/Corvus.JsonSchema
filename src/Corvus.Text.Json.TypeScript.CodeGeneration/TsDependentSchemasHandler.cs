// <copyright file="TsDependentSchemasHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// dependentSchemas: when a named property is present, the dependent subschema must validate the WHOLE
// instance (an in-place applicator -> share the parent tracker so its evaluations are credited).
internal sealed class TsDependentSchemasHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 610;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectPropertyDependentSchemasValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>>? ds = td.DependentSchemasSubschemaTypes();
        if (ds is null) { return; }
        foreach (KeyValuePair<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> kv in ds)
        {
            foreach (DependentSchemaDeclaration d in kv.Value)
            {
                string? e = TsEmit.EvalName(d.ReducedDepdendentSchemaType);
                if (e is null) { continue; }
                sb.Append("  if (__isObj(value) && Object.prototype.hasOwnProperty.call(value, ")
                  .Append(TsEmit.Str(d.JsonPropertyName)).Append(")) { const t = fresh(); if (!").Append(e).Append("(value, t").Append(TsEmit.ChildValue(d.KeywordPathModifier)).Append(")) { ").Append(TsEmit.Propagate).Append(" } ev.mergeProps(t); ev.mergeItems(t); }\n");
            }
        }
    }
}
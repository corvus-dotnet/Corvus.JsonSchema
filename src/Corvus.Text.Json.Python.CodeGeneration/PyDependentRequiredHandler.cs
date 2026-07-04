// <copyright file="PyDependentRequiredHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// dependentRequired: when a named property is present, each of its dependency properties must also be present.
internal sealed class PyDependentRequiredHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 720;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IObjectDependentRequiredValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>>? dr = td.DependentRequired();
        if (dr is null)
        {
            return;
        }

        var entries = new List<DependentRequiredDeclaration>();
        foreach (KeyValuePair<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>> kv in dr)
        {
            foreach (DependentRequiredDeclaration d in kv.Value)
            {
                if (d.Dependencies.Count > 0)
                {
                    entries.Add(d);
                }
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_obj")).Append("(value):\n");
        foreach (DependentRequiredDeclaration d in entries)
        {
            mod.Body.Append("        if ").Append(PyEmit.Str(d.JsonPropertyName)).Append(" in value:\n");
            foreach (string dependency in d.Dependencies)
            {
                mod.Body.Append("            if ").Append(PyEmit.Str(dependency)).Append(" not in value:\n");
                PyEmit.FailBody(mod, td, keyword.Keyword, 16);
            }
        }
    }
}
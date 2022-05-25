// <copyright file="IJsonSchemaBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.JsonSchema.TypeBuilder
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface implemented by Json Schema Builders.
    /// </summary>
    public interface IJsonSchemaBuilder
    {
        /// <summary>
        /// Builds types for the schema provided by the given reference.
        /// </summary>
        /// <param name="reference">a uri-reference to the schema in which to build the types.</param>
        /// <param name="rootNamespace">The root namespace to use for types.</param>
        /// <param name="rebase">Rebase the root reference as if it were a root document.</param>
        /// <param name="baseUriToNamespaceMap">A map of base URIs to namespaces to use for specific types.</param>
        /// <param name="rootTypeName">A specific root type name for the root entity.</param>
        /// <returns>A <see cref="Task"/> which completes once the types are built. The tuple provides the root type name, and the generated types.</returns>
        Task<(string rootType, ImmutableDictionary<string, (string, string)> generatedTypes)> BuildTypesFor(string reference, string rootNamespace, bool rebase = false, Dictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null);

        /// <summary>
        /// Rebases a reference to an artificial root document.
        /// </summary>
        /// <param name="reference">The reference to rebase as a root document.</param>
        /// <returns>A <see cref="Task{TResult}"/> which, when complete, provides the artificial reference of the root document.</returns>
        Task<string> RebaseReferenceAsRootDocument(string reference);
    }
}
// <copyright file="WalkContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// The local state for building a type declaration.
/// </summary>
internal class WalkContext
{
    private readonly Stack<JsonSchemaScope> scopeStack = new();
    private readonly JsonSchemaTypeBuilder typeBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="WalkContext"/> class.
    /// </summary>
    /// <param name="typeBuilder">The type builder for the context.</param>
    /// <param name="rootSchema">The root schema for the context.</param>
    public WalkContext(JsonSchemaTypeBuilder typeBuilder, LocatedSchema rootSchema)
    {
        this.scopeStack.Push((rootSchema.Location, new JsonReference("#"), rootSchema, false, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
        this.typeBuilder = typeBuilder;
        this.RootSchema = rootSchema;
    }

    /// <summary>
    /// Gets the current scope.
    /// </summary>
    public JsonSchemaScope Scope => this.scopeStack.Peek();

    /// <summary>
    /// Gets the document location for the given subschema.
    /// </summary>
    public JsonReference SubschemaLocation => this.Scope.Location.Apply(this.Scope.Pointer);

    /// <summary>
    /// Gets the root schema for the context.
    /// </summary>
    public LocatedSchema RootSchema { get; }

    /// <summary>
    /// Leave the existing scope.
    /// </summary>
    /// <returns>The scope we have just left.</returns>
    public JsonSchemaScope LeaveScope()
    {
        JsonSchemaScope currentScope = this.scopeStack.Pop();

        // As we pop the scope, put any types that were dynamically replaced with the original values.
        foreach ((JsonReference location, TypeDeclaration type) in currentScope.ReplacedDynamicTypes)
        {
            this.typeBuilder.ReplaceLocatedTypeDeclaration(location, type);
        }

        return currentScope;
    }

    /// <summary>
    /// Enters a new scope for a subschema.
    /// </summary>
    /// <param name="pointer">The pointer to the subschema from the base schema.</param>
    public void EnterSubschemaScope(JsonReference pointer)
    {
        Debug.Assert(!pointer.HasUri, "The pointer must not have a URI.");
        Debug.Assert(pointer.HasFragment, "The pointer must have a fragment.");
        JsonSchemaScope currentScope = this.scopeStack.Peek();
        this.scopeStack.Push((currentScope.Location, pointer, currentScope.Schema, false, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
    }

    /// <summary>
    /// Update the scope location and flags with a new dynamic scope.
    /// </summary>
    /// <param name="newScopeLocation">The new scope location.</param>
    /// <param name="schema">The schema to become the base schema.</param>
    public void EnterDynamicScope(JsonReference newScopeLocation, LocatedSchema schema)
    {
        this.scopeStack.Push((newScopeLocation, new JsonReference("#"), schema, false, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
    }

    /// <summary>
    /// Update the scope location and flags with a new dynamic scope.
    /// </summary>
    /// <param name="referenceBaseLocation">The reference base location.</param>
    /// <param name="baseSchema">The base schema for the reference.</param>
    /// <param name="subschemaPointer">The pointer to the subschema.</param>
    public void EnterReferenceScope(JsonReference referenceBaseLocation, LocatedSchema baseSchema, JsonReference subschemaPointer)
    {
        this.scopeStack.Push((referenceBaseLocation, subschemaPointer, baseSchema, false, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
    }

    /// <summary>
    /// Update the scope with an unencoded property name.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    public void EnterSubschemaScopeForUnencodedPropertyName(string name)
    {
        this.EnterSubschemaScope(this.Scope.Pointer.AppendUnencodedPropertyNameToFragment(name));
    }

    /// <summary>
    /// Update the scope with an array index.
    /// </summary>
    /// <param name="index">The index in the array.</param>
    public void EnterSubschemaScopeForArrayIndex(int index)
    {
        this.EnterSubschemaScope(this.Scope.Pointer.AppendArrayIndexToFragment(index));
    }

    /// <summary>
    /// Finds the scope containing the first dynamic anchor that corresponds to the given anchor name.
    /// </summary>
    /// <param name="anchor">The anchor name.</param>
    /// <param name="baseScopeLocation">The base scope location in which the anchor was found.</param>
    /// <returns><see langword="true"/> if a dynamic anchor was found that matches the name, otherwise <see langword="false"/>.</returns>
    public bool TryGetScopeForFirstDynamicAnchor(string anchor, [NotNullWhen(true)] out JsonReference? baseScopeLocation)
    {
        JsonSchemaScope? foundScope = null;

        foreach (JsonSchemaScope scope in this.scopeStack.Reverse())
        {
            // Ignore consecutive identical scopes
            if (foundScope is JsonSchemaScope fs && fs.Location == scope.Location)
            {
                continue;
            }

            foundScope = scope;

            if (scope.Schema.TryGetAnchor(anchor, out Anchor? registeredAnchor) && registeredAnchor.IsDynamic)
            {
                baseScopeLocation = scope.Location;
                return true;
            }
        }

        baseScopeLocation = null;
        return false;
    }

    /// <summary>
    /// Finds the scope containing the first dynamic anchor that corresponds to the given anchor name.
    /// </summary>
    /// <param name="baseScopeLocation">The base scope location in which the anchor was found.</param>
    /// <returns><see langword="true"/> if a dynamic anchor was found that matches the name, otherwise <see langword="false"/>.</returns>
    public bool TryGetScopeForFirstRecursiveAnchor([NotNullWhen(true)] out JsonReference? baseScopeLocation)
    {
        JsonSchemaScope? foundScope = null;

        foreach (JsonSchemaScope scope in this.scopeStack.Reverse())
        {
            // Ignore consecutive identical scopes
            if (foundScope is JsonSchemaScope fs && fs.Location == scope.Location)
            {
                continue;
            }

            foundScope = scope;

            if (scope.Schema.IsRecursiveAnchor)
            {
                baseScopeLocation = scope.Location;
                return true;
            }
        }

        baseScopeLocation = null;
        return false;
    }

    /// <summary>
    /// Finds the previous scope Location in the stack, collapsing any identical scopes.
    /// </summary>
    /// <param name="previousScope">The previous scope Location.</param>
    /// <returns><see langword="true"/> if there was a previous scope.</returns>
    internal bool TryGetPreviousScope([NotNullWhen(true)] out JsonReference? previousScope)
    {
        JsonReference current = this.Scope.Location;
        foreach (JsonSchemaScope scope in this.scopeStack)
        {
            if (scope.Location == current)
            {
                continue;
            }

            previousScope = scope.Location;
            return true;
        }

        previousScope = null;
        return false;
    }

    /// <summary>
    /// Stashes away a subschema replacement that was made for a dynamic context.
    /// </summary>
    /// <param name="subschemaLocation">The subschema location.</param>
    /// <param name="previousDeclaration">The previous type declaration.</param>
    internal void ReplaceDeclarationInScope(JsonReference subschemaLocation, TypeDeclaration previousDeclaration)
    {
        // We pop the item off the stack, update its replaced dynamic types, and push it back on.
        JsonSchemaScope currentScope = this.scopeStack.Pop();
        this.scopeStack.Push(new(currentScope.Location, currentScope.Pointer, currentScope.Schema, currentScope.IsDynamicScope, currentScope.ReplacedDynamicTypes.Add((subschemaLocation, previousDeclaration))));
    }
}
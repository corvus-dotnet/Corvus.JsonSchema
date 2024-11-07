// <copyright file="TypeBuilderContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// The local state for building a type declaration.
/// </summary>
public class TypeBuilderContext
{
    private readonly Stack<JsonSchemaScope> scopeStack = new();
    private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations;
    private readonly JsonReference baseLocation;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeBuilderContext"/> class.
    /// </summary>
    /// <param name="schemaRegistry">The schema registry.</param>
    /// <param name="locatedTypeDeclarations">The existing locatedTypeDeclarations.</param>
    /// <param name="rootLocatedSchema">The root schema for the context.</param>
    /// <param name="baseLocation">The base location for generation.</param>
    public TypeBuilderContext(
        JsonSchemaRegistry schemaRegistry,
        Dictionary<string, TypeDeclaration> locatedTypeDeclarations,
        LocatedSchema rootLocatedSchema,
        JsonReference baseLocation)
    {
        this.scopeStack.Push((rootLocatedSchema.Location, new JsonReference("#"), rootLocatedSchema, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
        this.SchemaRegistry = schemaRegistry;
        this.RootLocatedSchema = rootLocatedSchema;
        this.locatedTypeDeclarations = locatedTypeDeclarations;
        this.baseLocation = baseLocation;
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
    public LocatedSchema RootLocatedSchema { get; }

    /// <summary>
    /// Gets the reversed scope stack.
    /// </summary>
    public IEnumerable<JsonSchemaScope> ReversedStack => this.scopeStack.Reverse();

    /// <summary>
    /// Gets the scope stack.
    /// </summary>
    public IEnumerable<JsonSchemaScope> Stack => this.scopeStack.AsEnumerable();

    /// <summary>
    /// Gets the <see cref="JsonSchemaRegistry"/>.
    /// </summary>
    public JsonSchemaRegistry SchemaRegistry { get; }

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
            this.ReplaceLocatedTypeDeclaration(location, type);
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
        this.scopeStack.Push((currentScope.Location, pointer, currentScope.LocatedSchema, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
    }

    /// <summary>
    /// Update the scope location and flags with a new scope.
    /// </summary>
    /// <param name="scopeName">The new scope name.</param>
    /// <param name="typeDeclaration">The type declaration whose schema is to become the base schema.</param>
    /// <param name="existingTypeDeclaration">The existing type declaration, if any.</param>
    /// <returns>True if the base scope was entered.</returns>
    public bool TryEnterBaseScope(string scopeName, TypeDeclaration typeDeclaration, out TypeDeclaration? existingTypeDeclaration)
    {
        if (this.Scope.Location.Uri.EndsWith(scopeName.AsSpan()))
        {
            // Ignore if we were already directly in the scope.
            existingTypeDeclaration = null;
            return false;
        }

        JsonReference newScopeNameReference = new(scopeName);
        JsonReference newScopeLocation;
        JsonReference newScopePath;

        if (newScopeNameReference.HasFragment && newScopeNameReference.Fragment.Length > 1)
        {
            if (newScopeNameReference.HasUri)
            {
                throw new InvalidOperationException("Unable to enter a base scope with both URI and fragment.");
            }

            newScopePath = newScopeNameReference;
            newScopeLocation = this.Scope.Location;
        }
        else
        {
            newScopePath = new JsonReference("#");
            newScopeLocation = this.Scope.Location.Apply(newScopeNameReference);
        }

        JsonReference currentLocation = this.SubschemaLocation;
        this.scopeStack.Push(
            (newScopeLocation,
            newScopePath,
            typeDeclaration.LocatedSchema,
            ImmutableList<(JsonReference Location,
            TypeDeclaration Type)>.Empty));

        if (currentLocation != this.SubschemaLocation)
        {
            // We have already built this in a preceding reference, at the dynamic location, so we need to replace our "local" version of the type
            // and then return the version we were already building/had already built.
            if (this.TryGetTypeDeclarationForCurrentLocation(out TypeDeclaration? alreadyBuildingDynamicTypeDeclaration))
            {
                if (alreadyBuildingDynamicTypeDeclaration.LocatedSchema.Location == newScopeLocation)
                {
                    this.ReplaceLocatedTypeDeclaration(currentLocation, alreadyBuildingDynamicTypeDeclaration);
                    existingTypeDeclaration = alreadyBuildingDynamicTypeDeclaration;
                    return true;
                }
                else
                {
                    if (this.TryGetTypeDeclarationForCurrentLocation(out TypeDeclaration? previousDefinition))
                    {
                        this.ReplaceDeclarationInScope(this.SubschemaLocation, previousDefinition);
                        this.RemoveLocatedTypeDeclaration(this.SubschemaLocation);
                    }
                }
            }

            this.AddLocatedTypeDeclaration(this.SubschemaLocation, typeDeclaration);
        }

        existingTypeDeclaration = null;
        return true;
    }

    /// <summary>
    /// Update the scope location for a new base location and schema, with subschema pointer.
    /// </summary>
    /// <param name="referenceBaseLocation">The reference base location.</param>
    /// <param name="baseSchema">The base schema for the reference.</param>
    /// <param name="subschemaPointer">The pointer to the subschema.</param>
    public void EnterReferenceScope(JsonReference referenceBaseLocation, LocatedSchema baseSchema, JsonReference subschemaPointer)
    {
        this.scopeStack.Push((referenceBaseLocation, subschemaPointer, baseSchema, ImmutableList<(JsonReference Location, TypeDeclaration Type)>.Empty));
    }

    /// <summary>
    /// Enter a dynamic scope.
    /// </summary>
    /// <param name="typeDeclaration">The base type declaration.</param>
    /// <param name="dynamicTypeDeclaration">The existing dynamic type declaration.</param>
    /// <param name="dynamicScope">The dynamic scope.</param>
    /// <param name="existingScopeTypeDeclaration">The existing type declaration for the dynamic scope,
    /// or <see langword="null"/> if there is no existing type declaration.</param>
    public void EnterDynamicScope(
        TypeDeclaration typeDeclaration,
        TypeDeclaration dynamicTypeDeclaration,
        JsonReference dynamicScope,
        out TypeDeclaration? existingScopeTypeDeclaration)
    {
        // We remove the existing one and replace it, for this context.
        this.ReplaceDeclarationInScope(this.SubschemaLocation, dynamicTypeDeclaration);
        this.RemoveLocatedTypeDeclaration(this.SubschemaLocation);
        UpdateDynamicLocation(typeDeclaration, dynamicScope);

        if (this.TryGetLocatedTypeDeclaration(typeDeclaration.LocatedSchema.Location, out TypeDeclaration? existingTypeForDynamicScope))
        {
            // If we already exist in the dynamic location
            // Add it to the current locatedTypeDeclarations for this subschema, and return it.
            this.AddLocatedTypeDeclaration(this.SubschemaLocation, existingTypeForDynamicScope);
            existingScopeTypeDeclaration = existingTypeForDynamicScope;
        }
        else
        {
            existingScopeTypeDeclaration = null;
            this.SchemaRegistry.TryAddLocatedSchema(typeDeclaration.LocatedSchema.Location, typeDeclaration.LocatedSchema);
            this.AddLocatedTypeDeclaration(this.SubschemaLocation, typeDeclaration);
            this.AddLocatedTypeDeclaration(typeDeclaration.LocatedSchema.Location, typeDeclaration);
        }
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
    /// Finds the previous scope Location in the stack, collapsing any identical scopes.
    /// </summary>
    /// <param name="previousScope">The previous scope Location.</param>
    /// <returns><see langword="true"/> if there was a previous scope.</returns>
    public bool TryGetPreviousScope([NotNullWhen(true)] out JsonReference? previousScope)
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
    /// Replace the located type declaration.
    /// </summary>
    /// <param name="location">The location at which the type declaration is to be replaced.</param>
    /// <param name="typeDeclaration">The type declaration with which to replace it.</param>
    public void ReplaceLocatedTypeDeclaration(JsonReference location, TypeDeclaration typeDeclaration)
    {
        this.locatedTypeDeclarations.Remove(location);
        this.locatedTypeDeclarations.Add(location, typeDeclaration);
    }

    /// <summary>
    /// Try to get a located type declaration.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="typeDeclaration">The resulting type declaration.</param>
    /// <returns>The located type declaration.</returns>
    public bool TryGetLocatedTypeDeclaration(JsonReference location, [NotNullWhen(true)] out TypeDeclaration? typeDeclaration)
    {
        return this.locatedTypeDeclarations.TryGetValue(location, out typeDeclaration);
    }

    /// <summary>
    /// Try to get a located type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The resulting type declaration.</param>
    /// <returns>The located type declaration.</returns>
    public bool TryGetTypeDeclarationForCurrentLocation([NotNullWhen(true)] out TypeDeclaration? typeDeclaration)
    {
        return this.TryGetLocatedTypeDeclaration(this.SubschemaLocation, out typeDeclaration);
    }

    /// <summary>
    /// Try to get a located schema.
    /// </summary>
    /// <param name="locatedSchema">The resulting located schema.</param>
    /// <returns><see langword="true"/> if the schema could be located.</returns>
    public bool TryGetLocatedSchemaForCurrentLocation([NotNullWhen(true)] out LocatedSchema? locatedSchema)
    {
        return this.SchemaRegistry.TryGetLocatedSchema(this.SubschemaLocation, out locatedSchema);
    }

    /// <summary>
    /// Add a located type declaration.
    /// </summary>
    /// <param name="subschemaLocation">The subschema location.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    public void AddLocatedTypeDeclaration(JsonReference subschemaLocation, TypeDeclaration typeDeclaration)
    {
        this.locatedTypeDeclarations.Add(subschemaLocation, typeDeclaration);
    }

    /// <summary>
    /// Remove a located type declaration.
    /// </summary>
    /// <param name="subschemaLocation">The subschema location at which to remove the type declaration.</param>
    public void RemoveLocatedTypeDeclaration(JsonReference subschemaLocation)
    {
        this.locatedTypeDeclarations.Remove(subschemaLocation);
    }

    /// <summary>
    /// Stashes away a subschema replacement that was made for a dynamic scope.
    /// </summary>
    /// <param name="subschemaLocation">The subschema location.</param>
    /// <param name="previousDeclaration">The previous type declaration.</param>
    public void ReplaceDeclarationInScope(JsonReference subschemaLocation, TypeDeclaration previousDeclaration)
    {
        // We pop the item off the stack, update its replaced dynamic types, and push it back on.
        JsonSchemaScope currentScope = this.scopeStack.Pop();
        this.scopeStack.Push(new(currentScope.Location, currentScope.Pointer, currentScope.LocatedSchema, currentScope.ReplacedDynamicTypes.Add((subschemaLocation, previousDeclaration))));
    }

    /// <summary>
    /// Build the type declaration for the given type builder context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> which, when complete, provides the built type declaration.</returns>
    /// <exception cref="InvalidOperationException">A registered schema could not be found at the current type builder context location.</exception>
    public async ValueTask<TypeDeclaration> BuildTypeDeclarationForCurrentScope(CancellationToken cancellationToken)
    {
        if (!this.TryGetLocatedSchemaForCurrentLocation(out LocatedSchema? schema))
        {
            throw new InvalidOperationException($"Unable to find the schema at {this.SubschemaLocation}");
        }

        if (WellKnownTypeDeclarations.TryGetBooleanOrEmptySchemaTypeDeclaration(schema, out TypeDeclaration? booleanTypeDeclaration))
        {
            if (this.TryGetTypeDeclarationForCurrentLocation(out TypeDeclaration? existingBooleanTypeDeclaration))
            {
                return existingBooleanTypeDeclaration;
            }

            this.AddLocatedTypeDeclaration(this.SubschemaLocation, booleanTypeDeclaration);
            return booleanTypeDeclaration;
        }

        TypeDeclaration typeDeclaration = new(schema);

        CustomKeywords.ApplyBeforeScope(this, typeDeclaration, cancellationToken);

        if (this.TryGetTypeDeclarationForCurrentLocation(out TypeDeclaration? existingTypeDeclaration))
        {
            Anchors.ApplyScopeResult result = Anchors.ApplyScopeToExistingType(this, typeDeclaration, existingTypeDeclaration);
            if (result.IsCompleteTypeDeclaration)
            {
                return existingTypeDeclaration;
            }
        }
        else
        {
            Anchors.ApplyScopeToNewType(this, typeDeclaration);
            this.AddLocatedTypeDeclaration(this.SubschemaLocation, typeDeclaration);
        }

        // Try to enter the dynamic scope of the ID - note that this *replaces*
        // the current scope, so we will be automatically popping it when we get done in the finally block.
        // Make sure all other code goes in the try/finally block to avoid scope leaks.
        bool enteredDynamicScope = CodeGeneration.Scope.TryEnterScope(this, typeDeclaration, cancellationToken, out TypeDeclaration? existingTypeDeclarationForScope);

        try
        {
            if (enteredDynamicScope && existingTypeDeclarationForScope is TypeDeclaration etd)
            {
                return etd;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return WellKnownTypeDeclarations.JsonAny;
            }

            CustomKeywords.ApplyBeforeSubschemas(this, typeDeclaration, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return WellKnownTypeDeclarations.JsonAny;
            }

            await Subschemas.BuildSubschemaTypes(this, typeDeclaration, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return WellKnownTypeDeclarations.JsonAny;
            }

            CustomKeywords.ApplyAfterSubschemas(this, typeDeclaration, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return WellKnownTypeDeclarations.JsonAny;
            }

            typeDeclaration.RelativeSchemaLocation = this.GetRelativeLocationFor(typeDeclaration.LocatedSchema.Location);
            typeDeclaration.BuildComplete = true;
            return typeDeclaration;
        }
        finally
        {
            if (enteredDynamicScope)
            {
                this.LeaveScope();
            }
        }
    }

    /// <summary>
    /// Updates the located schema associated with this type to present a new dynamic location.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="dynamicScopeLocation">The new dynamic scope location.</param>
    private static void UpdateDynamicLocation(TypeDeclaration typeDeclaration, JsonReference dynamicScopeLocation)
    {
        JsonReferenceBuilder builder = typeDeclaration.LocatedSchema.Location.AsBuilder();
        builder = new JsonReferenceBuilder(builder.Scheme, builder.Authority, builder.Path, ("dynamicScope=" + Uri.EscapeDataString(dynamicScopeLocation.ToString())).AsSpan(), builder.Fragment);
        typeDeclaration.UpdateLocation(builder.AsReference());
    }

    /// <summary>
    /// Gets reference for the target location relative to the base location.
    /// </summary>
    /// <param name="target">The target location.</param>
    /// <returns>The relative location.</returns>
    /// <remarks>The target must be an absolute location.</remarks>
    private JsonReference GetRelativeLocationFor(JsonReference target)
    {
        if (target.IsImplicitFile)
        {
            if (target == this.baseLocation)
            {
                return new JsonReference(Path.GetFileName(target));
            }

            return this.baseLocation.MakeRelative(target);
        }

        return target;
    }
}
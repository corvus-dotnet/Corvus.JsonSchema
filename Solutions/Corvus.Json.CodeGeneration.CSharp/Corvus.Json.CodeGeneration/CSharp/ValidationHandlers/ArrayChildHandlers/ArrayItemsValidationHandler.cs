// <copyright file="ArrayItemsValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for array items, when there is no explicit tuple type.
/// </summary>
public class ArrayItemsValidationHandler : IChildArrayItemValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="ArrayItemsValidationHandler"/>.
    /// </summary>
    public static ArrayItemsValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // This is the emitter for the array items validation code if there is no tuple.
        if (typeDeclaration.ExplicitTupleType() is not null)
        {
            return generator;
        }

        if (typeDeclaration.ExplicitNonTupleItemsType() is ArrayItemsTypeDeclaration nonTupleItems)
        {
            return generator
                .AppendValidateNonTupleItemsType(nonTupleItems, nonTupleItems.ReducedType == typeDeclaration.ArrayItemsType()?.ReducedType);
        }

        if (typeDeclaration.ExplicitUnevaluatedItemsType() is ArrayItemsTypeDeclaration unevaluatedItems)
        {
            return generator
                .AppendValidateUnevaluatedItemsType(unevaluatedItems, unevaluatedItems.ReducedType == typeDeclaration.ArrayItemsType()?.ReducedType);
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}
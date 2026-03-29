// <copyright file="TupleValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A string length validation handler.
/// </summary>
public class TupleValidationHandler : IChildArrayItemValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="TupleValidationHandler"/>.
    /// </summary>
    public static TupleValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ExplicitTupleType() is TupleTypeDeclaration tupleTypeDeclaration)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("ValidationContext itemResult;")
                .AppendLineIndent("switch (length)")
                .AppendLineIndent("{")
                .PushIndent();

            int i = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeDeclaration.ItemsTypes)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                generator
                    .AppendSeparatorLine()
                    .AppendIndent("case ")
                    .Append(i)
                    .AppendLine(":")
                    .PushIndent()
                        .AppendLineIndent("if (level > ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent(
                                "result = result.PushValidationLocationReducedPathModifier(new(",
                                SymbolDisplay.FormatLiteral(tupleTypeDeclaration.Keyword.GetPathModifier(item, i), true),
                                "));")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "itemResult = arrayEnumerator.Current.As<",
                            item.ReducedType.FullyQualifiedDotnetTypeName(),
                            ">().Validate(result.CreateChildContext(), level);")
                        .AppendBlockIndent(
                        """
                        if (level == ValidationLevel.Flag && !itemResult.IsValid)
                        {
                            return itemResult;
                        }

                        result = result.MergeResults(itemResult.IsValid, level, itemResult);
                        
                        if (level > ValidationLevel.Basic)
                        {
                            result = result.PopLocation();
                        }
                        
                        result = result.WithLocalItemIndex(length);
                        break;
                        """)
                    .PopIndent();
                i++;
            }

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("default:")
                    .PushIndent();

            if (typeDeclaration.ExplicitNonTupleItemsType() is ArrayItemsTypeDeclaration explicitItemsType)
            {
                generator
                        .AppendValidateNonTupleItemsType(explicitItemsType, explicitItemsType.ReducedType == typeDeclaration.ArrayItemsType()?.ReducedType);
            }
            else if (typeDeclaration.ExplicitUnevaluatedItemsType() is ArrayItemsTypeDeclaration explicitUnevaluatedItemsType)
            {
                generator
                        .AppendValidateUnevaluatedItemsType(explicitUnevaluatedItemsType, explicitUnevaluatedItemsType.ReducedType == typeDeclaration.ArrayItemsType()?.ReducedType);
            }

            generator
                    .AppendLineIndent("break;")
                    .PopIndent()
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}
// <copyright file="BuiltInNumberTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in number type.
/// </summary>
public sealed class BuiltInNumberTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInNumberTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInNumberTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInNumberTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.Number) != 0 &&
            typeDeclaration.AllowedCoreTypes().CountTypes() == 1)
        {
            string? candidateFormat = null;

            // We are a simple string type
            foreach (IValidationKeyword keyword in typeDeclaration.ValidationKeywords())
            {
                if (keyword is ICoreTypeValidationKeyword)
                {
                    continue;
                }

                if (keyword is IFormatValidationKeyword formatKeyword)
                {
                    formatKeyword.TryGetFormat(typeDeclaration, out candidateFormat);
                    continue;
                }

                // This is "some other" validation keyword, so we can't continue.
                return false;
            }

            typeDeclaration.SetDotnetNamespace("Corvus.Json");

            if (candidateFormat is string format)
            {
                switch (format)
                {
                    case "byte":
                        typeDeclaration.SetDotnetTypeName("JsonByte");
                        break;
                    case "uint16":
                        typeDeclaration.SetDotnetTypeName("JsonUInt16");
                        break;
                    case "uint32":
                        typeDeclaration.SetDotnetTypeName("JsonUInt32");
                        break;
                    case "uint64":
                        typeDeclaration.SetDotnetTypeName("JsonUInt64");
                        break;
                    case "uint128":
                        typeDeclaration.SetDotnetTypeName("JsonUInt128");
                        break;
                    case "sbyte":
                        typeDeclaration.SetDotnetTypeName("JsonSByte");
                        break;
                    case "int16":
                        typeDeclaration.SetDotnetTypeName("JsonInt16");
                        break;
                    case "int32":
                        typeDeclaration.SetDotnetTypeName("JsonInt32");
                        break;
                    case "int64":
                        typeDeclaration.SetDotnetTypeName("JsonInt64");
                        break;
                    case "int128":
                        typeDeclaration.SetDotnetTypeName("JsonInt128");
                        break;
                    case "half":
                        typeDeclaration.SetDotnetTypeName("JsonHalf");
                        break;
                    case "single":
                        typeDeclaration.SetDotnetTypeName("JsonSingle");
                        break;
                    case "double":
                        typeDeclaration.SetDotnetTypeName("JsonDouble");
                        break;
                    default:
                        typeDeclaration.SetDotnetTypeName("JsonNumber");
                        break;
                }
            }
            else
            {
                // This is a simple number
                typeDeclaration.SetDotnetTypeName("JsonNumber");
            }

            return true;
        }

        return false;
    }
}
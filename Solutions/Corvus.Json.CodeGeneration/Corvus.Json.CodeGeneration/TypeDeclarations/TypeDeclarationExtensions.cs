// <copyright file="TypeDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Extension methods for <see cref="TypeDeclaration"/>.
/// </summary>
public static class TypeDeclarationExtensions
{
    private delegate bool ArrayItemKeywordAccessor<T>(T keyword, TypeDeclaration typeDeclaration, out ArrayItemsTypeDeclaration? value)
        where T : IArrayItemsTypeProviderKeyword;

    private delegate bool ObjectPropertyKeywordAccessor<T>(T keyword, TypeDeclaration typeDeclaration, out FallbackObjectPropertyType? value)
    where T : IFallbackObjectPropertyTypeProviderKeyword;

    /// <summary>
    /// Gets a value indicating whether this type declaration
    /// has a sibling-hiding keyword.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration has a sibling-hiding keyword.</returns>
    public static bool HasSiblingHidingKeyword(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(HasSiblingHidingKeyword), out bool hasSiblingHidingKeyword))
        {
            hasSiblingHidingKeyword =
                that.Keywords()
                    .OfType<IHidesSiblingsKeyword>()
                    .Any();

            that.SetMetadata(nameof(HasSiblingHidingKeyword), hasSiblingHidingKeyword);
        }

        return hasSiblingHidingKeyword;
    }

    /// <summary>
    /// Gets the explicit content media type for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The content media type, or <see langword="null"/> if the type is not set.</returns>
    public static string? ExplicitContentMediaType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitContentMediaType), out string? result))
        {
            bool found = false;

            foreach (IContentMediaTypeValidationKeyword keyword in that.Keywords().OfType<IContentMediaTypeValidationKeyword>())
            {
                if (keyword.TryGetContentMediaType(that, out string? m))
                {
                    result = m;
                    that.SetMetadata(nameof(ExplicitContentMediaType), result);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                result = null;
                that.SetMetadata(nameof(ExplicitContentMediaType), result);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the explicit content media type for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The content media type, or <see langword="null"/> if the type is not set.</returns>
    public static string? ExplicitContentEncoding(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitContentEncoding), out string? result))
        {
            bool found = false;

            foreach (IContentEncodingValidationKeyword keyword in that.Keywords().OfType<IContentEncodingValidationKeyword>())
            {
                if (keyword.TryGetContentEncoding(that, out string? m))
                {
                    result = m;
                    that.SetMetadata(nameof(ExplicitContentEncoding), result);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                result = null;
                that.SetMetadata(nameof(ExplicitContentEncoding), result);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a value indicating whether the type is deprecated.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <param name="message">A deprecation message, or <see langword="null"/> if no message is provided.</param>
    /// <returns>True if the type is deprecated.</returns>
    public static bool IsDeprecated(this TypeDeclaration that, [MaybeNullWhen(false)] out string? message)
    {
        if (!that.TryGetMetadata(nameof(IsDeprecated), out (bool, string?) result))
        {
            bool found = false;
            foreach (IDeprecatedKeyword keyword in that.Keywords().OfType<IDeprecatedKeyword>())
            {
                if (keyword.IsDeprecated(that, out string? m))
                {
                    result = (true, m);
                    that.SetMetadata(nameof(IsDeprecated), result);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                result = (false, default);
                that.SetMetadata(nameof(IsDeprecated), result);
            }
        }

        message = result.Item2;
        return result.Item1;
    }

    /// <summary>
    /// Gets a value indicating whether the type has an exclusive maximum modifier.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration has an exclusive maximum modifier.</returns>
    public static bool HasExclusiveMaximumModifier(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(HasExclusiveMaximumModifier), out bool? result))
        {
            result = that.Keywords().OfType<IExclusiveMaximumBooleanKeyword>().Any(k => k.HasModifier(that));
            that.SetMetadata(nameof(HasExclusiveMaximumModifier), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets a value indicating whether the type has an exclusive minimum modifier.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration has an exclusive minimum modifier.</returns>
    public static bool HasExclusiveMinimumModifier(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(HasExclusiveMinimumModifier), out bool? result))
        {
            result = that.Keywords().OfType<IExclusiveMinimumBooleanKeyword>().Any(k => k.HasModifier(that));
            that.SetMetadata(nameof(HasExclusiveMinimumModifier), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets a value indicating whether the type requires string value validation.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration requires string value validation.</returns>
    public static bool RequiresStringValueValidation(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(RequiresStringValueValidation), out bool? result))
        {
            result = that.Keywords().OfType<IStringValueValidationKeyword>().Any();
            that.SetMetadata(nameof(RequiresStringValueValidation), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets a value indicating whether the type requires the string length in string validation.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration requires the string length in string validation.</returns>
    public static bool RequiresStringLength(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(RequiresStringLength), out bool? result))
        {
            result = that.Keywords().OfType<IStringValidationKeyword>().Any(k => k.RequiresStringLength(that));
            that.SetMetadata(nameof(RequiresStringLength), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets a value indicating whether the type requires the property count in object validation.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration requires the property count in object validation.</returns>
    public static bool RequiresPropertyCount(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(RequiresPropertyCount), out bool? result))
        {
            result = that.Keywords().OfType<IObjectValidationKeyword>().Any(k => k.RequiresPropertyCount(that));
            that.SetMetadata(nameof(RequiresPropertyCount), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets the pattern property declarations for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration for which to get the pattern properties.</param>
    /// <returns>The collection of <see cref="PatternPropertyDeclaration"/>, by keyword or <see langword="null"/>
    /// if no pattern properties were defined.</returns>
    public static IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? PatternProperties(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(
                nameof(PatternProperties),
                out IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? result))
        {
            Dictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> dictionary = [];

            foreach (IObjectPatternPropertyValidationKeyword keyword in that.Keywords().OfType<IObjectPatternPropertyValidationKeyword>())
            {
                if (keyword.TryGetValidationRegularExpressions(that, out IReadOnlyList<string>? patterns))
                {
                    List<PatternPropertyDeclaration> list = [];
                    int i = 0;
                    foreach (TypeDeclaration typeDeclaration in keyword.GetSubschemaTypeDeclarations(that))
                    {
                        list.Add(new(keyword, typeDeclaration, patterns[i]));
                        ++i;
                    }

                    if (list.Count > 0)
                    {
                        dictionary[keyword] = list;
                    }
                }
            }

            if (dictionary.Count > 0)
            {
                result = dictionary;
            }

            that.SetMetadata(nameof(PatternProperties), result);
        }

        return result;
    }

    /// <summary>
    /// Gets the reduced type declaration for the type.
    /// </summary>
    /// <param name="that">The type declaration to reduce.</param>
    /// <returns>The fully-reduced type declaration.</returns>
    public static ReducedTypeDeclaration ReducedTypeDeclaration(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ReducedTypeDeclaration), out ReducedTypeDeclaration reducedTypeDeclaration))
        {
            reducedTypeDeclaration = ReduceType(that, new JsonReference("#"));
            that.SetMetadata(nameof(ReducedTypeDeclaration), reducedTypeDeclaration);
        }

        return reducedTypeDeclaration;

        static ReducedTypeDeclaration ReduceType(
            TypeDeclaration baseType,
            JsonReference currentPathModifier)
        {
            if (baseType.CanReduce())
            {
                foreach (IReferenceKeyword refKeyword in baseType.Keywords().OfType<IReferenceKeyword>())
                {
                    if (refKeyword.GetSubschemaTypeDeclarations(baseType).FirstOrDefault() is TypeDeclaration referencedTypeDeclaration)
                    {
                        JsonReference updatedPathModifier = currentPathModifier.AppendUnencodedPropertyNameToFragment(refKeyword.Keyword);
                        ReducedTypeDeclaration declaration = referencedTypeDeclaration.ReducedTypeDeclaration();
                        updatedPathModifier = updatedPathModifier.AppendFragment(declaration.ReducedPathModifier);
                        return new(declaration.ReducedType, updatedPathModifier);
                    }
                }

                // If we were able to reduce, but there were no reference keywords, that means we reduced to JsonAny
                return new(WellKnownTypeDeclarations.JsonAny, currentPathModifier);
            }

            return new(baseType, currentPathModifier);
        }
    }

    /// <summary>
    /// Gets the 'if' subschema type, if available.
    /// </summary>
    /// <param name="that">The type declaration for which to get the subschema type.</param>
    /// <returns>The <see cref="SingleSubschemaKeywordTypeDeclaration"/>, or <see langword="null"/> if no type was available.</returns>
    public static SingleSubschemaKeywordTypeDeclaration? IfSubschemaType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(IfSubschemaType), out SingleSubschemaKeywordTypeDeclaration? subschemaType))
        {
            // We are only expecting to deal with a single if-like keyword.
            if (that.Keywords().OfType<IIfValidationKeyword>().FirstOrDefault() is IIfValidationKeyword keyword &&
                keyword.TryGetIfDeclaration(that, out TypeDeclaration? value) &&
                value is TypeDeclaration typeDeclaration)
            {
                subschemaType = new(typeDeclaration, keyword);
            }
            else
            {
                subschemaType = null;
            }

            that.SetMetadata(nameof(IfSubschemaType), subschemaType);
        }

        return subschemaType;
    }

    /// <summary>
    /// Gets the 'then' subschema type, if available.
    /// </summary>
    /// <param name="that">The type declaration for which to get the subschema type.</param>
    /// <returns>The <see cref="SingleSubschemaKeywordTypeDeclaration"/>, or <see langword="null"/> if no type was available.</returns>
    public static SingleSubschemaKeywordTypeDeclaration? ThenSubschemaType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ThenSubschemaType), out SingleSubschemaKeywordTypeDeclaration? subschemaType))
        {
            // We are only expecting to deal with a single then-like keyword.
            if (that.Keywords().OfType<IIfThenValidationKeyword>().FirstOrDefault() is IIfThenValidationKeyword keyword &&
                keyword.TryGetThenDeclaration(that, out TypeDeclaration? value) &&
                value is TypeDeclaration typeDeclaration)
            {
                subschemaType = new(typeDeclaration, keyword);
            }
            else
            {
                subschemaType = null;
            }

            that.SetMetadata(nameof(ThenSubschemaType), subschemaType);
        }

        return subschemaType;
    }

    /// <summary>
    /// Gets the 'property names' subschema type, if available.
    /// </summary>
    /// <param name="that">The type declaration for which to get the subschema type.</param>
    /// <returns>The <see cref="SingleSubschemaKeywordTypeDeclaration"/>, or <see langword="null"/> if no type was available.</returns>
    public static SingleSubschemaKeywordTypeDeclaration? PropertyNamesSubschemaType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(PropertyNamesSubschemaType), out SingleSubschemaKeywordTypeDeclaration? subschemaType))
        {
            // We are only expecting to deal with a single then-like keyword.
            if (that.Keywords().OfType<IObjectPropertyNameSubschemaValidationKeyword>().FirstOrDefault() is IObjectPropertyNameSubschemaValidationKeyword keyword &&
                keyword.TryGetPropertyNameDeclaration(that, out TypeDeclaration? value) &&
                value is TypeDeclaration typeDeclaration)
            {
                subschemaType = new(typeDeclaration, keyword);
            }
            else
            {
                subschemaType = null;
            }

            that.SetMetadata(nameof(PropertyNamesSubschemaType), subschemaType);
        }

        return subschemaType;
    }

    /// <summary>
    /// Gets the 'else' subschema type, if available.
    /// </summary>
    /// <param name="that">The type declaration for which to get the subschema type.</param>
    /// <returns>The <see cref="SingleSubschemaKeywordTypeDeclaration"/>, or <see langword="null"/> if no type was available.</returns>
    public static SingleSubschemaKeywordTypeDeclaration? ElseSubschemaType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ElseSubschemaType), out SingleSubschemaKeywordTypeDeclaration? subschemaType))
        {
            // We are only expecting to deal with a single else-like keyword.
            if (that.Keywords().OfType<ITernaryIfElseValidationKeyword>().FirstOrDefault() is ITernaryIfElseValidationKeyword keyword &&
                keyword.TryGetElseDeclaration(that, out TypeDeclaration? value) &&
                value is TypeDeclaration typeDeclaration)
            {
                subschemaType = new(typeDeclaration, keyword);
            }
            else
            {
                subschemaType = null;
            }

            that.SetMetadata(nameof(ElseSubschemaType), subschemaType);
        }

        return subschemaType;
    }

    /// <summary>
    /// Gets the dependent required declarations for the type declaration, grouped by the providing keyword.
    /// </summary>
    /// <param name="that">The type declaration for which to get the dependent required declarations.</param>
    /// <returns>A map of the keyword to the ordered collection of required property names.</returns>
    public static IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>>? DependentRequired(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(DependentRequired), out IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>>? compositionTypes))
        {
            compositionTypes = BuildDependentRequiredSubschemaTypes(that);
            that.SetMetadata(nameof(DependentRequired), compositionTypes);
        }

        return compositionTypes;

        static IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>> BuildDependentRequiredSubschemaTypes(TypeDeclaration that)
        {
            Dictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>> result = [];

            foreach (IObjectDependentRequiredValidationKeyword keyword in that.Keywords().OfType<IObjectDependentRequiredValidationKeyword>())
            {
                result[keyword] = keyword.GetDependentRequiredDeclarations(that);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the dependent schema declarations for the type declaration, grouped by the providing keyword.
    /// </summary>
    /// <param name="that">The type declaration for which to get the dependent schema declarations.</param>
    /// <returns>A map of the keyword to the ordered collection of composition types.</returns>
    public static IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>>? DependentSchemasSubschemaTypes(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(DependentSchemasSubschemaTypes), out IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>>? compositionTypes))
        {
            compositionTypes = BuildDependentSchemasSubschemaTypes(that);
            that.SetMetadata(nameof(DependentSchemasSubschemaTypes), compositionTypes);
        }

        return compositionTypes;

        static IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> BuildDependentSchemasSubschemaTypes(TypeDeclaration that)
        {
            Dictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> result = [];

            foreach (IObjectPropertyDependentSchemasValidationKeyword keyword in that.Keywords().OfType<IObjectPropertyDependentSchemasValidationKeyword>())
            {
                result[keyword] = keyword.GetDependentSchemaDeclarations(that);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the AllOf composition types for the type declaration, grouped by the providing keyword.
    /// </summary>
    /// <param name="that">The type declaration for which to get the AllOf composition types.</param>
    /// <returns>A map of the keyword to the ordered collection of composition types.</returns>
    public static IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>>? AllOfCompositionTypes(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(AllOfCompositionTypes), out IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes))
        {
            compositionTypes = BuildAllOfCompositionTypes(that);
            that.SetMetadata(nameof(AllOfCompositionTypes), compositionTypes);
        }

        return compositionTypes;

        static IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> BuildAllOfCompositionTypes(TypeDeclaration that)
        {
            Dictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> result = [];

            foreach (IAllOfSubschemaValidationKeyword keyword in that.Keywords().OfType<IAllOfSubschemaValidationKeyword>())
            {
                result[keyword] = keyword.GetSubschemaTypeDeclarations(that);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the AnyOf composition types for the type declaration, grouped by the providing keyword.
    /// </summary>
    /// <param name="that">The type declaration for which to get the AnyOf composition types.</param>
    /// <returns>A map of the keyword to the ordered collection of composition types.</returns>
    public static IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>>? AnyOfCompositionTypes(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(AnyOfCompositionTypes), out IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes))
        {
            compositionTypes = BuildAnyOfCompositionTypes(that);
            that.SetMetadata(nameof(AnyOfCompositionTypes), compositionTypes);
        }

        return compositionTypes;

        static IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> BuildAnyOfCompositionTypes(TypeDeclaration that)
        {
            Dictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> result = [];

            foreach (IAnyOfSubschemaValidationKeyword keyword in that.Keywords().OfType<IAnyOfSubschemaValidationKeyword>())
            {
                result[keyword] = keyword.GetSubschemaTypeDeclarations(that);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the AnyOf constant values for the type declaration, grouped by the providing keyword.
    /// </summary>
    /// <param name="that">The type declaration for which to get the AnyOf const values.</param>
    /// <returns>A map of the keyword to the ordered collection of const values.</returns>
    public static IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]>? AnyOfConstantValues(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(AnyOfConstantValues), out IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]>? constValues))
        {
            constValues = BuildAnyOfConstValues(that);
            that.SetMetadata(nameof(AnyOfConstantValues), constValues);
        }

        return constValues;

        static IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> BuildAnyOfConstValues(TypeDeclaration that)
        {
            Dictionary<IAnyOfConstantValidationKeyword, JsonElement[]> result = [];

            foreach (IAnyOfConstantValidationKeyword keyword in that.Keywords().OfType<IAnyOfConstantValidationKeyword>())
            {
                if (keyword.TryGetValidationConstants(that, out JsonElement[]? value) &&
                    value is JsonElement[] constants)
                {
                    result[keyword] = constants;
                }
                else
                {
                    result[keyword] = [];
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the OneOf composition types for the type declaration, grouped by the providing keyword.
    /// </summary>
    /// <param name="that">The type declaration for which to get the OneOf composition types.</param>
    /// <returns>A map of the keyword to the ordered collection of composition types.</returns>
    public static IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>>? OneOfCompositionTypes(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(OneOfCompositionTypes), out IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>>? compositionTypes))
        {
            compositionTypes = BuildOneOfCompositionTypes(that);
            that.SetMetadata(nameof(OneOfCompositionTypes), compositionTypes);
        }

        return compositionTypes;

        static IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> BuildOneOfCompositionTypes(TypeDeclaration that)
        {
            Dictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> result = [];

            foreach (IOneOfSubschemaValidationKeyword keyword in that.Keywords().OfType<IOneOfSubschemaValidationKeyword>())
            {
                result[keyword] = keyword.GetSubschemaTypeDeclarations(that);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the single constant value on the type.
    /// </summary>
    /// <param name="that">The type declaration for which to get the single constant value.</param>
    /// <returns>The single constant value for the type.</returns>
    public static JsonElement SingleConstantValue(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(SingleConstantValue), out JsonElement constantValue))
        {
            // First set to null to avoid recursion issues
            that.SetMetadata(nameof(SingleConstantValue), default(JsonElement));
            SetSingleConstantValue(that);
            that.TryGetMetadata(nameof(SingleConstantValue), out constantValue);
        }

        return constantValue;
    }

    /// <summary>
    /// Gets the single constant value on the type.
    /// </summary>
    /// <param name="that">The type declaration for which to get the single constant value.</param>
    /// <returns>The single constant value for the type.</returns>
    public static JsonElement ExplicitSingleConstantValue(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitSingleConstantValue), out JsonElement constantValue))
        {
            // First set to null to avoid recursion issues
            that.SetMetadata(nameof(ExplicitSingleConstantValue), default(JsonElement));
            SetSingleConstantValue(that);
            that.TryGetMetadata(nameof(ExplicitSingleConstantValue), out constantValue);
        }

        return constantValue;
    }

    /// <summary>
    /// Gets the format that is explicitly defined on the type.
    /// </summary>
    /// <param name="that">The type declaration for which to get the explicit format.</param>
    /// <returns>The explicit format for the type.</returns>
    public static string? ExplicitFormat(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitFormat), out string? format))
        {
            // First set to null to avoid recursion issues
            that.SetMetadata(nameof(ExplicitFormat), default(string?));
            SetFormat(that);
            that.TryGetMetadata(nameof(ExplicitFormat), out format);
        }

        return format;
    }

    /// <summary>
    /// Gets a value indicating whether the format is an assertion.
    /// </summary>
    /// <param name="that">The type declaration for which to get the explicit format.</param>
    /// <returns><see langoward="true"/> if the format should be asserted.</returns>
    public static bool IsFormatAssertion(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(IsFormatAssertion), out bool? assertion))
        {
            // First set to false to avoid recursion issues
            that.SetMetadata(nameof(IsFormatAssertion), false);
            SetFormat(that);
            that.TryGetMetadata(nameof(IsFormatAssertion), out assertion);
        }

        return assertion ?? false;
    }

    /// <summary>
    /// Gets the format for the type.
    /// </summary>
    /// <param name="that">The type declaration for which to get the format.</param>
    /// <returns>The format for the type.</returns>
    public static string? Format(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(Format), out string? format))
        {
            // First set to null to avoid recursion issues
            that.SetMetadata(nameof(Format), default(string?));
            SetFormat(that);
            that.TryGetMetadata(nameof(Format), out format);
        }

        return format;
    }

    /// <summary>
    /// Gets the rank for the array, or null if the item is not an array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array rank.</param>
    /// <returns>The rank for the array.</returns>
    public static int? ArrayRank(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ArrayRank), out int? arrayRank))
        {
            if (that.TupleType() is not null)
            {
                that.SetMetadata(nameof(ArrayRank), default(TypeDeclaration?));
                arrayRank = null;
            }
            else
            {
                // Set us to null if we are recursive
                that.SetMetadata(nameof(ArrayRank), default(int?));

                arrayRank = GetArrayRank(that);

                that.SetMetadata(nameof(ArrayRank), arrayRank);
            }
        }

        return arrayRank;

        static int? GetArrayRank(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return null;
            }

            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
            {
                return 1 + (arrayItemsType.ReducedType.ArrayRank() ?? 0);
            }

            return null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration is a map object.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type is a map object, otherwise false.</returns>
    public static bool IsMapObject(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(IsMapObject), out bool? result))
        {
            result = that.FallbackObjectPropertyType() is not null;
            that.SetMetadata(nameof(IsMapObject), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets a value indicating whether this is a "pure" tuple
    /// (i.e. it only permits a fixed set of array items and no
    /// others).
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the array is a tuple.</returns>
    public static bool IsTuple(this TypeDeclaration that)
    {
        return that.TupleType() is not null;
    }

    /// <summary>
    /// Gets a value indicating whether this is a fixed-size numeric array.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type represents a fixed-size numeric array.</returns>
    public static bool IsFixedSizeNumericArray(this TypeDeclaration that)
    {
        return that.IsFixedSizeArray() && that.IsNumericArray();
    }

    /// <summary>
    /// Gets a value indicating whether this is a fixed size array in all ranks.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the array is a fixed size in every rank.</returns>
    public static bool IsFixedSizeArray(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(IsFixedSizeArray), out bool? isFixedSizeArray))
        {
            isFixedSizeArray = GetIsFixedSizeArray(that);

            // Set the value before we correct for the child (to protect against
            // recursive references)
            that.SetMetadata(nameof(IsFixedSizeArray), isFixedSizeArray);

            if (isFixedSizeArray.Value &&
                that.ArrayItemsType() is ArrayItemsTypeDeclaration childItemsType &&
                (childItemsType.ReducedType.ImpliedCoreTypes() & CoreTypes.Array) != 0)
            {
                // Overwrite the value if our child array is not fixed size.
                isFixedSizeArray = childItemsType.ReducedType.IsFixedSizeArray();
                that.SetMetadata(nameof(IsFixedSizeArray), isFixedSizeArray);
            }
        }

        return isFixedSizeArray ?? false;

        static bool GetIsFixedSizeArray(TypeDeclaration typeDeclaration)
        {
            return typeDeclaration.ArrayDimension() is not null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a numeric array in all ranks.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns>The dimension for the array.</returns>
    public static bool IsNumericArray(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(IsNumericArray), out bool? isNumericArray))
        {
            // Set the value that indicates we are *not* a numeric array
            // (which will be the case if our items type recursively references
            // us)
            that.SetMetadata(nameof(IsNumericArray), false);
            isNumericArray = GetIsNumericArray(that);
            that.SetMetadata(nameof(IsNumericArray), isNumericArray);
        }

        return isNumericArray ?? false;

        static bool GetIsNumericArray(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration childItemsType)
            {
                if ((childItemsType.ReducedType.ImpliedCoreTypes() & (CoreTypes.Number | CoreTypes.Integer)) != 0 &&
                    (childItemsType.ReducedType.ImpliedCoreTypes() & ~(CoreTypes.Number | CoreTypes.Integer)) == 0)
                {
                    return true;
                }

                if ((childItemsType.ReducedType.ImpliedCoreTypes() & CoreTypes.Array) != 0 &&
                    childItemsType.ReducedType.ImpliedCoreTypes().CountTypes() == 1)
                {
                    return GetIsNumericArray(childItemsType.ReducedType);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the value buffer size for the full fixed-size array in all ranks.
    /// </summary>
    /// <param name="that">The type declaration for which to get the buffer size.</param>
    /// <returns>The buffer size for the array.</returns>
    public static int? ArrayValueBufferSize(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ArrayValueBufferSize), out int? arrayValueBufferSize))
        {
            // Set our size to null initially to avoid recursion errors.
            that.SetMetadata(nameof(ArrayValueBufferSize), default(int?));
            arrayValueBufferSize = GetArrayValueBufferSize(that);

            that.SetMetadata(nameof(ArrayValueBufferSize), arrayValueBufferSize);
        }

        return arrayValueBufferSize;

        static int? GetArrayValueBufferSize(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.ArrayDimension() is int value)
            {
                if (ChildArrayValueBufferSize(typeDeclaration) is int childValue)
                {
                    return value * childValue;
                }

                return value;
            }

            return null;
        }

        static int? ChildArrayValueBufferSize(TypeDeclaration that)
        {
            if (that.ArrayItemsType() is ArrayItemsTypeDeclaration childItemsType)
            {
                if (childItemsType.ReducedType.ArrayValueBufferSize() is int childValue)
                {
                    return childValue;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the dimension for the array, or null if the item is not an array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array rank.</param>
    /// <returns>The dimension for the array.</returns>
    public static int? ArrayDimension(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ArrayDimension), out int? arrayDimension))
        {
            arrayDimension = GetArrayDimension(that);
            that.SetMetadata(nameof(ArrayDimension), arrayDimension);
        }

        return arrayDimension;

        static int? GetArrayDimension(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return null;
            }

            int minimumValue = int.MaxValue;
            int maximumValue = 0;

            foreach (IArrayLengthConstantValidationKeyword keyword in
                        typeDeclaration.Keywords().OfType<IArrayLengthConstantValidationKeyword>())
            {
                if (keyword.TryGetValue(typeDeclaration, out int value) &&
                    keyword.TryGetOperator(typeDeclaration, out Operator op))
                {
                    switch (op)
                    {
                        case Operator.Equals:
                            minimumValue = Math.Min(value, minimumValue);
                            maximumValue = Math.Max(value, maximumValue);
                            break;
                        case Operator.GreaterThanOrEquals:
                            minimumValue = Math.Min(value, minimumValue);
                            break;
                        case Operator.LessThanOrEquals:
                            maximumValue = Math.Max(value, maximumValue);
                            break;
                        case Operator.GreaterThan:
                            minimumValue = Math.Min(value + 1, minimumValue);
                            break;
                        case Operator.LessThan:
                            maximumValue = Math.Max(value - 1, maximumValue);
                            break;
                    }
                }
            }

            if (minimumValue != maximumValue)
            {
                return null;
            }

            return minimumValue;
        }
    }

    /// <summary>
    /// Gets the type of a strongly-typed array, or <see langword="null"/> if the type is not
    /// a strongly-typed array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
    public static ArrayItemsTypeDeclaration? ExplicitArrayItemsType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitArrayItemsType), out ArrayItemsTypeDeclaration? itemsType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(ExplicitArrayItemsType), out itemsType);
        }

        return itemsType;
    }

    /// <summary>
    /// Gets the type of a strongly-typed array, or <see langword="null"/> if the type is not
    /// a strongly-typed array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
    public static ArrayItemsTypeDeclaration? ArrayItemsType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ArrayItemsType), out ArrayItemsTypeDeclaration? itemsType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(ArrayItemsType), out itemsType);
        }

        return itemsType;
    }

    /// <summary>
    /// Gets the type of properties in a strongly-typed object, or <see langword="null"/> if the type is not
    /// a strongly-typed object.
    /// </summary>
    /// <param name="that">The type declaration for which to get the object property type.</param>
    /// <returns>The <see cref="CodeGeneration.FallbackObjectPropertyType"/> for the strongly typed object properties.</returns>
    public static FallbackObjectPropertyType? FallbackObjectPropertyType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(FallbackObjectPropertyType), out FallbackObjectPropertyType? itemsType))
        {
            BuildObjectTypes(that);
            that.TryGetMetadata(nameof(FallbackObjectPropertyType), out itemsType);
        }

        return itemsType;
    }

    /// <summary>
    /// Gets the collection of explicitly required properties in an object, or <see langword="null"/> if the type is not
    /// a strongly-typed object.
    /// </summary>
    /// <param name="that">The type declaration for which to get the object property type.</param>
    /// <returns>The <see cref="CodeGeneration.FallbackObjectPropertyType"/> for the strongly typed object properties.</returns>
    public static IReadOnlyCollection<PropertyDeclaration>? ExplicitRequiredProperties(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitRequiredProperties), out IReadOnlyCollection<PropertyDeclaration>? propertyTypes))
        {
            propertyTypes =
                that.PropertyDeclarations.Where(
                    p =>
                        p.RequiredOrOptional == RequiredOrOptional.Required).ToList();

            that.SetMetadata(nameof(ExplicitRequiredProperties), propertyTypes);
        }

        return propertyTypes;
    }

    /// <summary>
    /// Gets the collection of explicitly required properties in an object, or <see langword="null"/> if the type is not
    /// a strongly-typed object.
    /// </summary>
    /// <param name="that">The type declaration for which to get the object property type.</param>
    /// <returns>The <see cref="CodeGeneration.FallbackObjectPropertyType"/> for the strongly typed object properties.</returns>
    public static IReadOnlyCollection<PropertyDeclaration>? ExplicitProperties(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitProperties), out IReadOnlyCollection<PropertyDeclaration>? propertyTypes))
        {
            propertyTypes =
                that.PropertyDeclarations.Where(
                    p => p.LocalOrComposed == LocalOrComposed.Local || p.RequiredOrOptional == RequiredOrOptional.Required).ToList();

            that.SetMetadata(nameof(ExplicitProperties), propertyTypes);
        }

        return propertyTypes;
    }

    /// <summary>
    /// Gets the type of additional properties (evaluated in the local scope) in a strongly-typed object, or <see langword="null"/>
    /// if no such type is defined.
    /// </summary>
    /// <param name="that">The type declaration for which to get the object property type.</param>
    /// <returns>The <see cref="CodeGeneration.FallbackObjectPropertyType"/> for the strongly typed object properties.</returns>
    public static FallbackObjectPropertyType? LocalEvaluatedPropertyType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(LocalEvaluatedPropertyType), out FallbackObjectPropertyType? itemsType))
        {
            BuildObjectTypes(that);
            that.TryGetMetadata(nameof(LocalEvaluatedPropertyType), out itemsType);
        }

        return itemsType;
    }

    /// <summary>
    /// Gets the type of additional properties (evaluated in the local-and-applied scope) in a strongly-typed object, or <see langword="null"/>
    /// if no such type is defined.
    /// </summary>
    /// <param name="that">The type declaration for which to get the object property type.</param>
    /// <returns>The <see cref="CodeGeneration.FallbackObjectPropertyType"/> for the strongly typed object properties.</returns>
    public static FallbackObjectPropertyType? LocalAndAppliedEvaluatedPropertyType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(LocalAndAppliedEvaluatedPropertyType), out FallbackObjectPropertyType? itemsType))
        {
            BuildObjectTypes(that);
            that.TryGetMetadata(nameof(LocalAndAppliedEvaluatedPropertyType), out itemsType);
        }

        return itemsType;
    }

    /// <summary>
    /// Gets the type of non-tuple types in a typed array, or <see langword="null"/> if the type is not
    /// a strongly-typed array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
    public static ArrayItemsTypeDeclaration? ExplicitNonTupleItemsType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitNonTupleItemsType), out ArrayItemsTypeDeclaration? nonTupleItemsType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(ExplicitNonTupleItemsType), out nonTupleItemsType);
        }

        return nonTupleItemsType;
    }

    /// <summary>
    /// Gets the type of a strongly-typed array, or <see langword="null"/> if the type is not
    /// a strongly-typed array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
    public static ArrayItemsTypeDeclaration? NonTupleItemsType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(NonTupleItemsType), out ArrayItemsTypeDeclaration? nonTupleItemsType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(NonTupleItemsType), out nonTupleItemsType);
        }

        return nonTupleItemsType;
    }

    /// <summary>
    /// Gets the type of unevaluated item types in a typed array, or <see langword="null"/> if the type is not
    /// a strongly-typed array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
    public static ArrayItemsTypeDeclaration? ExplicitUnevaluatedItemsType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitUnevaluatedItemsType), out ArrayItemsTypeDeclaration? unevaluatedItemsType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(ExplicitUnevaluatedItemsType), out unevaluatedItemsType);
        }

        return unevaluatedItemsType;
    }

    /// <summary>
    /// Gets the explicit type of a tuple, or <see langword="null"/> if the type is not
    /// explicitly a strongly-typed tuple.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="TupleTypeDeclaration"/> for the tuple items.</returns>
    /// <remarks>This will return a tuple if and only if it is explicitly declared on this type.
    /// Typically this is used for validation purposes.</remarks>
    /// <seealso cref="TupleType(TypeDeclaration)"/>
    /// <seealso cref="ImplicitTupleType(TypeDeclaration)"/>
    public static TupleTypeDeclaration? ExplicitTupleType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ExplicitTupleType), out TupleTypeDeclaration? tupleType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(ExplicitTupleType), out tupleType);
        }

        return tupleType;
    }

    /// <summary>
    /// Gets the implicit type of a tuple, or <see langword="null"/> if the type is not
    /// explicitly a strongly-typed tuple.
    /// </summary>
    /// <param name="that">The type declaration for which to get the tuple type.</param>
    /// <returns>The <see cref="TupleTypeDeclaration"/> for the tuple items.</returns>
    /// <remarks>This will return a tuple if it is explicitly declared on this type, or
    /// through composition. Typically this is used for creation purposes.</remarks>
    /// <seealso cref="TupleType(TypeDeclaration)"/>
    /// <seealso cref="ExplicitTupleType(TypeDeclaration)"/>
    public static TupleTypeDeclaration? ImplicitTupleType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(ImplicitTupleType), out TupleTypeDeclaration? tupleType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(ImplicitTupleType), out tupleType);
        }

        return tupleType;
    }

    /// <summary>
    /// Gets the type of a tuple, or <see langword="null"/> if the type is not
    /// a strongly-typed tuple.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="TupleTypeDeclaration"/> for the tuple items.</returns>
    /// <remarks>This will return null if the type allows items other than the tuple.</remarks>
    /// <seealso cref="ImplicitTupleType(TypeDeclaration)"/>
    /// <seealso cref="ExplicitTupleType(TypeDeclaration)"/>
    public static TupleTypeDeclaration? TupleType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(TupleType), out TupleTypeDeclaration? tupleType))
        {
            BuildArrayTypes(that);
            that.TryGetMetadata(nameof(TupleType), out tupleType);
        }

        return tupleType;
    }

    /// <summary>
    /// Gets a value indicating whether this type declaration
    /// is defined in a definitions container.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration is in a definitions container.</returns>
    public static bool IsInDefinitionsContainer(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(IsInDefinitionsContainer), out bool isInDefinitionsContainer))
        {
            isInDefinitionsContainer = GetIsInDefinitionsContainer(that);
            that.SetMetadata(nameof(IsInDefinitionsContainer), isInDefinitionsContainer);
        }

        return isInDefinitionsContainer;

        static bool GetIsInDefinitionsContainer(TypeDeclaration typeDeclaration)
        {
            JsonReference reference = typeDeclaration.LocatedSchema.Location;

            foreach (IDefinitionsKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IDefinitionsKeyword>())
            {
                if (reference is { HasFragment: true, Fragment.Length: > 1 } && reference.Fragment.LastIndexOf('/') == keyword.Keyword.Length + 2 && reference.Fragment[2..].StartsWith(keyword.Keyword.AsSpan()))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires items evaluation tracking.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires items evaluation tracking.</returns>
    public static bool RequiresItemsEvaluationTracking(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresItemsEvaluationTracking during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresItemsEvaluationTracking), out bool requiresTracking))
        {
            requiresTracking = GetRequiresItemsEvaluationTracking(that);
            that.SetMetadata(nameof(RequiresItemsEvaluationTracking), requiresTracking);
        }

        return requiresTracking;

        static bool GetRequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return typeDeclaration
                .Keywords()
                .OfType<IArrayValidationKeyword>()
                .Any(k => k.RequiresItemsEvaluationTracking(typeDeclaration));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires array length tracking.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires the array length.</returns>
    public static bool RequiresArrayLength(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresArrayLength during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresArrayLength), out bool requiresTracking))
        {
            requiresTracking = GetRequiresArrayLength(that);
            that.SetMetadata(nameof(RequiresArrayLength), requiresTracking);
        }

        return requiresTracking;

        static bool GetRequiresArrayLength(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return typeDeclaration.Keywords()
                .OfType<IArrayValidationKeyword>()
                .Any(k => k.RequiresArrayLength(typeDeclaration));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires array enumeration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires the array enumeration.</returns>
    public static bool RequiresArrayEnumeration(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresArrayEnumeration during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresArrayEnumeration), out bool requiresEnumeration))
        {
            requiresEnumeration = GetRequiresArrayEnumeration(that);
            that.SetMetadata(nameof(RequiresArrayEnumeration), requiresEnumeration);
        }

        return requiresEnumeration;

        static bool GetRequiresArrayEnumeration(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return
                typeDeclaration.Keywords()
                    .OfType<IArrayValidationKeyword>()
                    .Any(k => k.RequiresArrayEnumeration(typeDeclaration));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires object enumeration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires the array enumeration.</returns>
    public static bool RequiresObjectEnumeration(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresObjectEnumeration during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresObjectEnumeration), out bool requiresEnumeration))
        {
            requiresEnumeration = GetRequiresObjectEnumeration(that);
            that.SetMetadata(nameof(RequiresObjectEnumeration), requiresEnumeration);
        }

        return requiresEnumeration;

        static bool GetRequiresObjectEnumeration(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return
                typeDeclaration.Keywords()
                    .OfType<IObjectValidationKeyword>()
                    .Any(k => k.RequiresObjectEnumeration(typeDeclaration));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires property evaluation tracking.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires property evaluation tracking.</returns>
    public static bool RequiresPropertyEvaluationTracking(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresPropertyEvaluationTracking during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresPropertyEvaluationTracking), out bool requiresTracking))
        {
            requiresTracking = GetRequiresPropertyEvaluationTracking(that);
            that.SetMetadata(nameof(RequiresPropertyEvaluationTracking), requiresTracking);
        }

        return requiresTracking;

        static bool GetRequiresPropertyEvaluationTracking(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return
                typeDeclaration.Keywords()
                    .OfType<IObjectValidationKeyword>()
                    .Any(k => k.RequiresPropertyEvaluationTracking(typeDeclaration));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires the value kind.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires the value kind.</returns>
    public static bool RequiresJsonValueKind(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresValueKind during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresJsonValueKind), out bool requiresJsonValueKind))
        {
            requiresJsonValueKind = GetRequiresJsonValueKind(that);
            that.SetMetadata(nameof(RequiresJsonValueKind), requiresJsonValueKind);
        }

        return requiresJsonValueKind;

        static bool GetRequiresJsonValueKind(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return
                typeDeclaration.Keywords()
                    .OfType<IValueKindValidationKeyword>()
                    .Any();
        }
    }

    /// <summary>
    /// Indicates whether the type declaration has a default value.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type has a default value.</returns>
    public static bool HasDefaultValue(this TypeDeclaration that)
    {
        return that.DefaultValue().ValueKind != JsonValueKind.Undefined;
    }

    /// <summary>
    /// Gets the default value for an instance.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The default value for the instance, or <see langword="default"/> if there is
    /// no default value.</returns>
    public static JsonElement DefaultValue(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use DefaultValue during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(DefaultValue), out JsonElement? defaultValue))
        {
            defaultValue = GetDefaultValue(that);
            that.SetMetadata(nameof(DefaultValue), defaultValue);
        }

        return defaultValue!.Value;

        static JsonElement GetDefaultValue(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return default;
            }

            foreach (IDefaultValueProviderKeyword keyword in typeDeclaration.Keywords().OfType<IDefaultValueProviderKeyword>())
            {
                if (keyword.TryGetDefaultValue(typeDeclaration, out JsonElement defaultValue))
                {
                    return defaultValue;
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the type declarations provided by <see cref="ICompositionKeyword"/> keywords for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The collection of composition type declarations.</returns>
    public static IReadOnlyCollection<TypeDeclaration> CompositionTypeDeclarations(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use DefinitionKeywords during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(CompositionTypeDeclarations), out IReadOnlyCollection<TypeDeclaration>? compositionTypeDeclarations))
        {
            HashSet<TypeDeclaration> result = [];

            foreach (ISubschemaProviderKeyword keyword in that.Keywords().OfType<ICompositionKeyword>().OfType<ISubschemaProviderKeyword>())
            {
                foreach (TypeDeclaration subschema in keyword.GetSubschemaTypeDeclarations(that))
                {
                    result.Add(subschema.ReducedTypeDeclaration().ReducedType);
                }
            }

            compositionTypeDeclarations = result.OrderBy(t => t.LocatedSchema.Location).ToList();

            that.SetMetadata(nameof(CompositionTypeDeclarations), compositionTypeDeclarations);
        }

        return compositionTypeDeclarations ?? [];
    }

    /// <summary>
    /// Gets the <see cref="IKeyword" /> keywords for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The collection of validation keywords.</returns>
    public static IReadOnlyCollection<IKeyword> Keywords(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(Keywords), out IReadOnlyCollection<IKeyword>? keywords))
        {
            if (!InternalTryGetKeywords(that, out keywords))
            {
                keywords = [];
            }

            that.SetMetadata(nameof(Keywords), keywords);
        }

        return keywords ?? [];

        static bool InternalTryGetKeywords(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyCollection<IKeyword>? keywords)
        {
            var allKeywords = typeDeclaration.LocatedSchema.Vocabulary.Keywords.Where(typeDeclaration.HasKeyword).ToList();

            if (allKeywords.Count > 0)
            {
                keywords = allKeywords;
                return true;
            }

            keywords = null;
            return false;
        }
    }

    /// <summary>
    /// Gets the <see cref="IValidationKeyword" /> keywords for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The collection of validation keywords.</returns>
    public static IReadOnlyCollection<IValidationKeyword> ValidationKeywords(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use ValidationKeywords during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(ValidationKeywords), out IReadOnlyCollection<IValidationKeyword>? validationKeywords))
        {
            if (!TryGetKeywords(that, out validationKeywords))
            {
                validationKeywords = [];
            }

            that.SetMetadata(nameof(ValidationKeywords), validationKeywords);
        }

        return validationKeywords ?? [];
    }

    /// <summary>
    /// Gets the <see cref="IDefinitionsKeyword"/> keywords for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The collection of definition keywords.</returns>
    public static IReadOnlyCollection<IDefinitionsKeyword> DefinitionsKeywords(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use DefinitionKeywords during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(DefinitionsKeywords), out IReadOnlyCollection<IDefinitionsKeyword>? definitionKeywords))
        {
            if (!TryGetKeywords(that, out definitionKeywords))
            {
                definitionKeywords = [];
            }

            that.SetMetadata(nameof(DefinitionsKeywords), definitionKeywords);
        }

        return definitionKeywords ?? [];
    }

    /// <summary>
    /// Gets the <see cref="ITypeNameProviderKeyword"/> keywords for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The collection of type name provider keywords.</returns>
    public static IReadOnlyCollection<ITypeNameProviderKeyword> TypeNameProviderKeywords(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use TypeNameProviderKeywords during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(TypeNameProviderKeywords), out IReadOnlyCollection<ITypeNameProviderKeyword>? typeNameProviderKeywords))
        {
            if (!TryGetKeywords(that, out typeNameProviderKeywords))
            {
                typeNameProviderKeywords = [];
            }

            that.SetMetadata(nameof(TypeNameProviderKeywords), typeNameProviderKeywords);
        }

        return typeNameProviderKeywords ?? [];
    }

    /// <summary>
    /// Gets the ordered validation handlers for the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <param name="languageProvider">The language provider.</param>
    /// <returns>The ordered collection of validation handlers.</returns>
    public static IReadOnlyCollection<IKeywordValidationHandler> OrderedValidationHandlers(this TypeDeclaration that, ILanguageProvider languageProvider)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use OrderedValidationHandlers during the type build process.");
        }

        // We build a key that includes the type of the language provider, so we can reuse the type declaration
        // across multiple language providers if we so wish.
        string key = $"{nameof(OrderedValidationHandlers)}_{languageProvider.GetType().Name}";
        if (!that.TryGetMetadata(key, out IReadOnlyCollection<IKeywordValidationHandler>? orderedValidationHandlers))
        {
            if (!TryGetOrderedValidationHandlers(that, languageProvider, out orderedValidationHandlers))
            {
                orderedValidationHandlers = [];
            }

            that.SetMetadata(key, orderedValidationHandlers);
        }

        return orderedValidationHandlers ?? [];

        static bool TryGetOrderedValidationHandlers(
            TypeDeclaration typeDeclaration,
            ILanguageProvider languageProvider,
            [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? orderedValidationHandlers)
        {
            List<IKeywordValidationHandler> handlers =
                [..typeDeclaration.ValidationKeywords()
                .Distinct()
                .SelectMany(
                    t =>
                    {
                        if (languageProvider.TryGetValidationHandlersFor(t, out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers))
                        {
                            return validationHandlers;
                        }

                        return [];
                    })
                .Distinct()
                .OrderBy(h => h.ValidationHandlerPriority)];

            if (handlers.Count > 0)
            {
                orderedValidationHandlers = handlers;
                return true;
            }

            orderedValidationHandlers = null;
            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration can be reduced.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type can be reduced.</returns>
    public static bool CanReduce(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use CanReduce during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(CanReduce), out bool canReduce))
        {
            canReduce = CanReduce(that.LocatedSchema);
            that.SetMetadata(nameof(CanReduce), canReduce);
        }

        return canReduce;

        static bool CanReduce(LocatedSchema locatedSchema)
        {
            if (locatedSchema.IsBooleanSchema)
            {
                return false;
            }

            IKeyword? hidesSiblingsKeyword = locatedSchema.Vocabulary.Keywords
                    .FirstOrDefault(k => k is IHidesSiblingsKeyword && locatedSchema.Schema.HasKeyword(k));

            if (hidesSiblingsKeyword is IKeyword k)
            {
                // We have a keyword that hides its siblings
                // is it something that blocks reduction?
                return k.CanReduce(locatedSchema.Schema);
            }

            return locatedSchema.Vocabulary.Keywords.All(
                    k => k.CanReduce(locatedSchema.Schema));
        }
    }

    /// <summary>
    /// Gets a value indicating the core types represented by the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The CoreTypes implied by the type declaration.</returns>
    public static CoreTypes ImpliedCoreTypesOrAny(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the core types during the type build process.");
        }

        CoreTypes coreTypes = that.ImpliedCoreTypes();

        // If there are no implied types, this is actually any Any type.
        if ((coreTypes & CoreTypes.Any) == 0)
        {
            return CoreTypes.Any;
        }

        return coreTypes;
    }

    /// <summary>
    /// Gets a value indicating the core types represented by the type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The CoreTypes implied by the type declaration.</returns>
    public static CoreTypes ImpliedCoreTypes(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the core types during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(ImpliedCoreTypes), out CoreTypes coreTypes))
        {
            // Set us to None temporarily to avoid recursion.
            that.SetMetadata(nameof(ImpliedCoreTypes), CoreTypes.None);

            // Then union the types
            coreTypes = Composition.UnionImpliesCoreTypeForTypeDeclaration(that, CoreTypes.None);

            // Then set the actual metadata
            that.SetMetadata(nameof(ImpliedCoreTypes), coreTypes);
        }

        return coreTypes;
    }

    /// <summary>
    /// Gets a value indicating the core types represented by the type declaration,
    /// considering only locally implied types, not composite subschema.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The CoreTypes implied by the type declaration.</returns>
    public static CoreTypes LocallyImpliedCoreTypes(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the core types during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(LocallyImpliedCoreTypes), out CoreTypes coreTypes))
        {
            // Set us to None temporarily to avoid recursion.
            that.SetMetadata(nameof(LocallyImpliedCoreTypes), CoreTypes.None);

            // Then union the types
            coreTypes = Composition.UnionLocallyImpliedCoreTypeForTypeDeclaration(that, CoreTypes.None);

            // Then set the actual metadata
            that.SetMetadata(nameof(LocallyImpliedCoreTypes), coreTypes);
        }

        return coreTypes;
    }

    /// <summary>
    /// Gets the union of <see cref="CoreTypes"/> to match for type validation, or
    /// <see cref="CoreTypes.None"/> if any type is valid.
    /// </summary>
    /// <param name="that">The <see cref="TypeDeclaration"/> for which to get the type.</param>
    /// <returns>The union of <see cref="CoreTypes"/> that may be valid for this type declaration, or <see cref="CoreTypes.None"/>
    /// if no core types are explicitly required.</returns>
    public static CoreTypes AllowedCoreTypes(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the allowed core types during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(AllowedCoreTypes), out CoreTypes coreTypes))
        {
            coreTypes = GetAllowedCoreTypes(that);
            that.SetMetadata(nameof(AllowedCoreTypes), coreTypes);
        }

        return coreTypes;

        static CoreTypes GetAllowedCoreTypes(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.LocatedSchema.Schema.ValueKind != JsonValueKind.Object)
            {
                return CoreTypes.Any;
            }

            CoreTypes result = CoreTypes.None;

            foreach (ICoreTypeValidationKeyword coreTypeValidationKeyword in typeDeclaration.ValidationKeywords().OfType<ICoreTypeValidationKeyword>())
            {
                result |= coreTypeValidationKeyword.AllowedCoreTypes(typeDeclaration);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the (optional) short documentation for this type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The short documentation for the type declaration.</returns>
    public static string? ShortDocumentation(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the short documentation during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(ShortDocumentation), out string? shortDocumentation))
        {
            shortDocumentation = Documentation.TryGetShortDocumentation(that, out shortDocumentation) ? shortDocumentation : null;
            that.SetMetadata(nameof(ShortDocumentation), shortDocumentation);
        }

        return shortDocumentation;
    }

    /// <summary>
    /// Gets the (optional) long documentation for this type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The long documentation for the type declaration.</returns>
    public static string? LongDocumentation(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the long documentation during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(LongDocumentation), out string? longDocumentation))
        {
            longDocumentation = Documentation.TryGetLongDocumentation(that, out longDocumentation) ? longDocumentation : null;
            that.SetMetadata(nameof(LongDocumentation), longDocumentation);
        }

        return longDocumentation;
    }

    /// <summary>
    /// Gets the (optional) examples for this type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The examples for the type declaration.</returns>
    public static string[]? Examples(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the examples during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(Examples), out string[]? examples))
        {
            examples = Documentation.TryGetExamples(that, out examples) ? examples : null;
            that.SetMetadata(nameof(Examples), examples);
        }

        return examples;
    }

    /// <summary>
    /// Gets the (optional) validation constants for this type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The validation constants for the type declaration.</returns>
    public static IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]>? ValidationConstants(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the validation constants during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(ValidationConstants), out IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]>? constants))
        {
            constants = TryGetValidationConstants(that, out constants) ? constants : null;
            that.SetMetadata(nameof(ValidationConstants), constants);
        }

        return constants;

        static bool TryGetValidationConstants(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]>? validationConstants)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                validationConstants = null;
                return false;
            }

            Dictionary<IValidationConstantProviderKeyword, JsonElement[]> constants = [];

            foreach (IValidationConstantProviderKeyword keyword in typeDeclaration.Keywords().OfType<IValidationConstantProviderKeyword>())
            {
                if (keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? keywordConstants))
                {
                    constants.Add(keyword, keywordConstants);
                }
            }

            if (constants.Count > 0)
            {
                validationConstants = constants;
                return true;
            }

            validationConstants = null;
            return false;
        }
    }

    /// <summary>
    /// Gets the (optional) typed validation constants for this type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The typed validation constants for the type declaration.</returns>
    public static IReadOnlyDictionary<ITypedValidationConstantProviderKeyword, TypedValidationConstantDefinition[]>? TypedValidationConstants(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the validation constants during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(TypedValidationConstants), out IReadOnlyDictionary<ITypedValidationConstantProviderKeyword, TypedValidationConstantDefinition[]>? constants))
        {
            constants = TryGetTypedValidationConstants(that, out constants) ? constants : null;
            that.SetMetadata(nameof(TypedValidationConstants), constants);
        }

        return constants;

        static bool TryGetTypedValidationConstants(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyDictionary<ITypedValidationConstantProviderKeyword, TypedValidationConstantDefinition[]>? validationConstants)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                validationConstants = null;
                return false;
            }

            Dictionary<ITypedValidationConstantProviderKeyword, TypedValidationConstantDefinition[]> constants = [];

            foreach (ITypedValidationConstantProviderKeyword keyword in typeDeclaration.Keywords().OfType<ITypedValidationConstantProviderKeyword>())
            {
                if (keyword.TryGetValidationConstants(typeDeclaration, out TypedValidationConstantDefinition[]? keywordConstants))
                {
                    constants.Add(keyword, keywordConstants);
                }
            }

            if (constants.Count > 0)
            {
                validationConstants = constants;
                return true;
            }

            validationConstants = null;
            return false;
        }
    }

    /// <summary>
    /// Gets the (optional) validation regular expressions for this type declaration.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The validation regular expressions for the type declaration.</returns>
    public static IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>>? ValidationRegularExpressions(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot get the validation regular expressions during the type build process.");
        }

        if (!that.TryGetMetadata(
            nameof(ValidationRegularExpressions),
            out IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>>? expressions))
        {
            expressions = TryGetValidationRegularExpressions(that, out expressions) ? expressions : null;
            that.SetMetadata(nameof(ValidationRegularExpressions), expressions);
        }

        return expressions;

        static bool TryGetValidationRegularExpressions(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>>? validationRegularExpressions)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                validationRegularExpressions = null;
                return false;
            }

            Dictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>> constants = [];

            foreach (IValidationRegexProviderKeyword keyword in typeDeclaration.Keywords().OfType<IValidationRegexProviderKeyword>())
            {
                if (keyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string>? keywordConstants))
                {
                    constants.Add(keyword, keywordConstants);
                }
            }

            if (constants.Count > 0)
            {
                validationRegularExpressions = constants;
                return true;
            }

            validationRegularExpressions = null;
            return false;
        }
    }

    private static bool TryGetKeywords<T>(
        TypeDeclaration typeDeclaration,
        [NotNullWhen(true)] out IReadOnlyCollection<T>? keywords)
        where T : notnull, IKeyword
    {
        var allKeywords =
            typeDeclaration.Keywords()
                .OfType<T>().ToList();

        if (allKeywords.Count > 0)
        {
            keywords = allKeywords;
            return true;
        }

        keywords = null;
        return false;
    }

    private static void SetSingleConstantValue(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.HasSiblingHidingKeyword())
        {
            typeDeclaration.SetMetadata(nameof(SingleConstantValue), default(JsonElement));
            return;
        }

        JsonElement foundElement = default;

        foreach (ISingleConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<ISingleConstantValidationKeyword>())
        {
            if (keyword.TryGetConstantValue(typeDeclaration, out JsonElement value) &&
                value.ValueKind != JsonValueKind.Undefined)
            {
                if (foundElement.ValueKind == JsonValueKind.Undefined)
                {
                    // We don't have an existing constant value,
                    foundElement = value;
                }
#if !BUILDING_SOURCE_GENERATOR
                else if (JsonElement.DeepEquals(foundElement, value))
                {
                    // The new constant is the same as the old value
                    // (typically because a composing type explicitly restates the
                    // constant from one of the composed types)
                    continue;
                }
#endif
                else
                {
                    // We have more than keyword that explicitly provides a single constant value
                    // (which is an odd situation...we will not attempt to make sense of it)
                    break;
                }
            }
        }

        typeDeclaration.SetMetadata(nameof(ExplicitSingleConstantValue), foundElement);

        // Now go through all the allOf union types and see if we can find one
        foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IAllOfSubschemaValidationKeyword>())
        {
            foreach (TypeDeclaration t in keyword.GetSubschemaTypeDeclarations(typeDeclaration))
            {
                if (t.SingleConstantValue() is JsonElement constantValue &&
                    constantValue.ValueKind != JsonValueKind.Undefined)
                {
                    if (foundElement.ValueKind == JsonValueKind.Undefined)
                    {
                        // We don't have an existing declaration, or
                        // we are overriding an implicit declaration with
                        // an explicit declaration
                        foundElement = constantValue;
                    }
#if !BUILDING_SOURCE_GENERATOR
                    else if (JsonElement.DeepEquals(foundElement, constantValue))
                    {
                        // The new type is the same as the old type
                        // (typically because a composing type explicitly restates the
                        // type from one of the composed types)
                        continue;
                    }
#endif
                    else
                    {
                        // We have more than keyword that explicitly provides a format
                        // (which is an odd situation...we will not attempt to make sense of it)
                        break;
                    }
                }
            }
        }

        typeDeclaration.SetMetadata(nameof(SingleConstantValue), foundElement);
    }

    private static void SetFormat(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.HasSiblingHidingKeyword())
        {
            typeDeclaration.SetMetadata(nameof(ExplicitFormat), default(string?));
            typeDeclaration.SetMetadata(nameof(Format), default(string?));
            return;
        }

        string? foundFormat = null;
        bool isAssertion = false;

        foreach (IFormatProviderKeyword keyword in typeDeclaration.Keywords().OfType<IFormatProviderKeyword>())
        {
            if (keyword.TryGetFormat(typeDeclaration, out string? value) &&
                value is string format)
            {
                if (foundFormat is null)
                {
                    // We don't have an existing format,
                    foundFormat = format;
                    isAssertion = keyword is IFormatValidationKeyword;
                }
                else if (foundFormat.Equals(format, StringComparison.Ordinal))
                {
                    // The new format is the same as the old format
                    // (typically because a composing type explicitly restates the
                    // format from one of the composed types)
                    continue;
                }
                else
                {
                    // We have more than keyword that explicitly provides a format
                    // (which is an odd situation...we will not attempt to make sense of it)
                    break;
                }
            }
        }

        typeDeclaration.SetMetadata(nameof(ExplicitFormat), foundFormat);
        typeDeclaration.SetMetadata(nameof(IsFormatAssertion), isAssertion);

        // Now go through all the allOf union types and see if we can find one
        foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IAllOfSubschemaValidationKeyword>())
        {
            foreach (TypeDeclaration t in keyword.GetSubschemaTypeDeclarations(typeDeclaration))
            {
                if (t.Format() is string format)
                {
                    if (foundFormat is null)
                    {
                        // We don't have an existing declaration, or
                        // we are overriding an implicit declaration with
                        // an explicit declaration
                        foundFormat = format;
                    }
                    else if (foundFormat.Equals(format, StringComparison.Ordinal))
                    {
                        // The new type is the same as the old type
                        // (typically because a composing type explicitly restates the
                        // type from one of the composed types)
                        continue;
                    }
                    else
                    {
                        // We have more than keyword that explicitly provides a format
                        // (which is an odd situation...we will not attempt to make sense of it)
                        break;
                    }
                }
            }
        }

        typeDeclaration.SetMetadata(nameof(Format), foundFormat);
    }

    private static void BuildArrayTypes(TypeDeclaration typeDeclaration)
    {
        TupleTypeDeclaration? tupleType = GetTupleType(typeDeclaration);

        ArrayItemsTypeDeclaration? itemsType =
            GetItemsType(
                typeDeclaration,
                static (IArrayItemsTypeProviderKeyword k, TypeDeclaration t, out ArrayItemsTypeDeclaration? v)
                    => k.TryGetArrayItemsType(t, out v),
                static t => t.ArrayItemsType());

        ArrayItemsTypeDeclaration? nonTupleItems =
            GetItemsType(
                typeDeclaration,
                static (INonTupleArrayItemsTypeProviderKeyword k, TypeDeclaration t, out ArrayItemsTypeDeclaration? v) =>
                    k.TryGetNonTupleArrayItemsType(t, out v),
                static t => t.NonTupleItemsType());

        ArrayItemsTypeDeclaration? unevaluatedItems =
            GetItemsType(
                typeDeclaration,
                static (IUnevaluatedArrayItemsTypeProviderKeyword k, TypeDeclaration t, out ArrayItemsTypeDeclaration? v) =>
                    k.TryGetUnevaluatedArrayItemsType(t, out v),
                static t => t.ExplicitUnevaluatedItemsType());

        bool isTuple = tupleType is not null && DeniesNonTupleItems(nonTupleItems, unevaluatedItems);

        if (isTuple)
        {
            typeDeclaration.SetMetadata(nameof(TupleType), tupleType);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(TupleType), default(TupleTypeDeclaration?));
        }

        if (tupleType is TupleTypeDeclaration ti && !ti.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(ImplicitTupleType), ti);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(ImplicitTupleType), default(TupleTypeDeclaration?));
        }

        if (tupleType is TupleTypeDeclaration te && te.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(ExplicitTupleType), te);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(ExplicitTupleType), default(TupleTypeDeclaration?));
        }

        if (!isTuple)
        {
            typeDeclaration.SetMetadata(nameof(ArrayItemsType), itemsType);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(ArrayItemsType), default(ArrayItemsTypeDeclaration?));
        }

        if (itemsType is ArrayItemsTypeDeclaration i && i.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(ExplicitArrayItemsType), i);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(ExplicitArrayItemsType), default(ArrayItemsTypeDeclaration?));
        }

        typeDeclaration.SetMetadata(nameof(NonTupleItemsType), nonTupleItems);

        if (nonTupleItems is ArrayItemsTypeDeclaration nti && nti.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(ExplicitNonTupleItemsType), nti);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(ExplicitNonTupleItemsType), default(ArrayItemsTypeDeclaration?));
        }

        if (unevaluatedItems is ArrayItemsTypeDeclaration ui && ui.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(ExplicitUnevaluatedItemsType), ui);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(ExplicitUnevaluatedItemsType), default(ArrayItemsTypeDeclaration?));
        }

        static ArrayItemsTypeDeclaration? GetItemsType<T>(
            TypeDeclaration typeDeclaration,
            ArrayItemKeywordAccessor<T> keywordAccessor,
            Func<TypeDeclaration, ArrayItemsTypeDeclaration?> childAccessor)
            where T : IArrayItemsTypeProviderKeyword
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return null;
            }

            if (typeDeclaration.ImpliedCoreTypes().CountTypes() != 1)
            {
                return null;
            }

            ArrayItemsTypeDeclaration? declaration = null;

            foreach (T keyword in typeDeclaration.Keywords().OfType<T>())
            {
                if (keywordAccessor(keyword, typeDeclaration, out ArrayItemsTypeDeclaration? value) &&
                    value is ArrayItemsTypeDeclaration itemsType)
                {
                    if (declaration is null ||
                            (itemsType.IsExplicit && !declaration.IsExplicit))
                    {
                        // We don't have an existing declaration,
                        declaration = itemsType;
                    }
                    else if (declaration.ReducedType == itemsType.ReducedType)
                    {
                        // The new type is the same as the old type
                        // (typically because a composing type explicitly restates the
                        // type from one of the composed types)
                        continue;
                    }
                    else
                    {
                        // We have more than one keyword that explicitly provides an array
                        // type.
                        return null;
                    }
                }
            }

            // Now go through all the allOf union types and see if we can find one
            foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IAllOfSubschemaValidationKeyword>())
            {
                foreach (TypeDeclaration t in keyword.GetSubschemaTypeDeclarations(typeDeclaration))
                {
                    if (childAccessor(t) is ArrayItemsTypeDeclaration referencedArrayItemsTypeDeclaration)
                    {
                        if (declaration is null)
                        {
                            // We don't have an existing declaration, or
                            // we are overriding an implicit declaration with
                            // an explicit declaration
                            declaration = new(referencedArrayItemsTypeDeclaration.UnreducedType, false, referencedArrayItemsTypeDeclaration.Keyword);
                        }
                        else if (declaration.ReducedType == referencedArrayItemsTypeDeclaration.ReducedType)
                        {
                            // The new type is the same as the old type
                            // (typically because a composing type explicitly restates the
                            // type from one of the composed types)
                            continue;
                        }
                        else
                        {
                            // We have more than keyword that explicitly provides an array
                            // type.
                            return null;
                        }
                    }
                }
            }

            return declaration;
        }

        static TupleTypeDeclaration? GetTupleType(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return null;
            }

            if (typeDeclaration.ImpliedCoreTypes().CountTypes() != 1)
            {
                return null;
            }

            TupleTypeDeclaration? declaration = null;

            foreach (ITupleTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<ITupleTypeProviderKeyword>())
            {
                if (keyword.TryGetTupleType(typeDeclaration, out TupleTypeDeclaration? value) &&
                    value is TupleTypeDeclaration itemsType)
                {
                    if (declaration is null ||
                            (itemsType.IsExplicit && !declaration.IsExplicit))
                    {
                        // We don't have an existing declaration,
                        declaration = itemsType;
                    }
                    else if (declaration.ItemsTypesSequenceEquals(itemsType))
                    {
                        // The new type is the same as the old type
                        // (typically because a composing type explicitly restates the
                        // type from one of the composed types)
                        continue;
                    }
                    else
                    {
                        // We have more than keyword that explicitly provides an array
                        // type.
                        return null;
                    }
                }
            }

            // Now go through all the allOf union types and see if we can find one
            foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IAllOfSubschemaValidationKeyword>())
            {
                foreach (TypeDeclaration t in keyword.GetSubschemaTypeDeclarations(typeDeclaration))
                {
                    if (t.TupleType() is TupleTypeDeclaration referencedTupleTypeDeclaration)
                    {
                        if (declaration is null)
                        {
                            // We don't have an existing declaration, or
                            // we are overriding an implicit declaration with
                            // an explicit declaration
                            declaration = new(referencedTupleTypeDeclaration.UnreducedTypes, false, referencedTupleTypeDeclaration.Keyword);
                        }
                        else if (declaration.IsExplicit)
                        {
                            continue;
                        }
                        else if (declaration.ItemsTypesSequenceEquals(referencedTupleTypeDeclaration))
                        {
                            // The new type is the same as the old type
                            // (typically because a composing type explicitly restates the
                            // type from one of the composed types)
                            continue;
                        }
                        else
                        {
                            // We ignore the case where we are trying to pick up a second
                            // non-explicit case. We could get more sophisticated in future.
                            continue;
                        }
                    }
                }
            }

            return declaration;
        }
    }

    private static void BuildObjectTypes(TypeDeclaration typeDeclaration)
    {
        FallbackObjectPropertyType? propertyType =
            GetPropertyType(
                typeDeclaration,
                static (IFallbackObjectPropertyTypeProviderKeyword k, TypeDeclaration t, out FallbackObjectPropertyType? v)
                    => k.TryGetFallbackObjectPropertyType(t, out v),
                static t => t.FallbackObjectPropertyType());

        FallbackObjectPropertyType? localEvaluatedPropertyType =
            GetPropertyType(
                typeDeclaration,
                static (ILocalEvaluatedPropertyValidationKeyword k, TypeDeclaration t, out FallbackObjectPropertyType? v) =>
                    k.TryGetFallbackObjectPropertyType(t, out v),
                static t => t.LocalEvaluatedPropertyType(),
                withComposition: false);

        FallbackObjectPropertyType? localAndAppliedEvaluatedPropertyType =
            GetPropertyType(
                typeDeclaration,
                static (ILocalAndAppliedEvaluatedPropertyValidationKeyword k, TypeDeclaration t, out FallbackObjectPropertyType? v) =>
                    k.TryGetFallbackObjectPropertyType(t, out v),
                static t => t.LocalAndAppliedEvaluatedPropertyType(),
                withComposition: false);

        if (typeDeclaration.HasPropertyDeclarations)
        {
            typeDeclaration.SetMetadata(nameof(FallbackObjectPropertyType), default(FallbackObjectPropertyType?));
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(FallbackObjectPropertyType), propertyType);
        }

        if (localEvaluatedPropertyType is FallbackObjectPropertyType nti && nti.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(LocalEvaluatedPropertyType), nti);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(LocalEvaluatedPropertyType), default(FallbackObjectPropertyType?));
        }

        if (localAndAppliedEvaluatedPropertyType is FallbackObjectPropertyType ui && ui.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(LocalAndAppliedEvaluatedPropertyType), ui);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(LocalAndAppliedEvaluatedPropertyType), default(FallbackObjectPropertyType?));
        }

        static FallbackObjectPropertyType? GetPropertyType<T>(
            TypeDeclaration typeDeclaration,
            ObjectPropertyKeywordAccessor<T> keywordAccessor,
            Func<TypeDeclaration, FallbackObjectPropertyType?> childAccessor,
            bool withComposition = true)
            where T : IFallbackObjectPropertyTypeProviderKeyword
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return null;
            }

            if (typeDeclaration.ImpliedCoreTypes().CountTypes() != 1)
            {
                return null;
            }

            FallbackObjectPropertyType? declaration = null;

            foreach (T keyword in typeDeclaration.Keywords().OfType<T>())
            {
                if (keywordAccessor(keyword, typeDeclaration, out FallbackObjectPropertyType? value) &&
                    value is FallbackObjectPropertyType itemsType)
                {
                    if (declaration is null ||
                            (itemsType.IsExplicit && !declaration.IsExplicit))
                    {
                        // We don't have an existing declaration,
                        declaration = itemsType;
                    }
                    else if (declaration.ReducedType == itemsType.ReducedType)
                    {
                        // The new type is the same as the old type
                        // (typically because a composing type explicitly restates the
                        // type from one of the composed types)
                        continue;
                    }
                    else
                    {
                        // We have more than one keyword that explicitly provides an array
                        // type.
                        return null;
                    }
                }
            }

            if (!withComposition)
            {
                return declaration;
            }

            // Now go through all the allOf union types and see if we can find one
            foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IAllOfSubschemaValidationKeyword>())
            {
                foreach (TypeDeclaration t in keyword.GetSubschemaTypeDeclarations(typeDeclaration))
                {
                    if (childAccessor(t) is FallbackObjectPropertyType referencedObjectPropertyTypeDeclaration)
                    {
                        if (declaration is null)
                        {
                            // We don't have an existing declaration, or
                            // we are overriding an implicit declaration with
                            // an explicit declaration
                            declaration = new(referencedObjectPropertyTypeDeclaration.UnreducedType, referencedObjectPropertyTypeDeclaration.Keyword, false);
                        }
                        else if (declaration.ReducedType == referencedObjectPropertyTypeDeclaration.ReducedType)
                        {
                            // The new type is the same as the old type
                            // (typically because a composing type explicitly restates the
                            // type from one of the composed types)
                            continue;
                        }
                        else
                        {
                            // We have more than keyword that explicitly provides an array
                            // type in the subschema; however we will just ignore that and carry on
                            // so we pick up any explicit type already defined
                            continue;
                        }
                    }
                }
            }

            return declaration;
        }
    }

    private static bool DeniesNonTupleItems(ArrayItemsTypeDeclaration? nonTupleItems, ArrayItemsTypeDeclaration? unevaluatedItems)
    {
        // We could provide a more sophisticated approach in future.
        return
            nonTupleItems?.ReducedType.LocatedSchema.Schema.ValueKind == JsonValueKind.False ||
            (nonTupleItems is null && unevaluatedItems?.ReducedType.LocatedSchema.Schema.ValueKind == JsonValueKind.False);
    }
}
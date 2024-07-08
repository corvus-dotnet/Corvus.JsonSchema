// <copyright file="TypeDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Extension methods for <see cref="TypeDeclaration"/>.
/// </summary>
public static class TypeDeclarationExtensions
{
    private delegate bool KeywordAccessor<T>(T keyword, TypeDeclaration typeDeclaration, out ArrayItemsTypeDeclaration? value)
        where T : IArrayItemsTypeProviderKeyword;

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
                that.LocatedSchema.Vocabulary.Keywords
                    .OfType<IHidesSiblingsKeyword>()
                    .Any(k => that.HasKeyword(k));
            that.SetMetadata(nameof(HasSiblingHidingKeyword), hasSiblingHidingKeyword);
        }

        return hasSiblingHidingKeyword;
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
                foreach (IReferenceKeyword refKeyword in baseType.LocatedSchema.Vocabulary.Keywords.OfType<IReferenceKeyword>())
                {
                    if (refKeyword.GetSubschemaTypeDeclarations(baseType).FirstOrDefault() is TypeDeclaration referencedTypeDeclaration)
                    {
                        JsonReference updatedPathModifier = currentPathModifier.AppendUnencodedPropertyNameToFragment(refKeyword.Keyword);
                        ReducedTypeDeclaration declaration = referencedTypeDeclaration.ReducedTypeDeclaration();
                        updatedPathModifier = updatedPathModifier.AppendFragment(declaration.ReducedPathModifier);
                        return new(declaration.ReducedType, updatedPathModifier);
                    }
                }
            }

            return new(baseType, currentPathModifier);
        }
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

            if ((isFixedSizeArray ?? false) &&
                that.ArrayItemsType() is ArrayItemsTypeDeclaration childItemsType &&
                (childItemsType.ReducedType.ImpliedCoreTypes() & CoreTypes.Array) != 0)
            {
                // Overwrite the value if our child array is not fixed size.
                isFixedSizeArray = childItemsType.ReducedType.IsFixedSizeArray();
                that.SetMetadata(nameof(IsFixedSizeArray), isFixedSizeArray);
            }
        }

        return isFixedSizeArray ?? false;

        static bool GetIsFixedSizeArray(
            TypeDeclaration typeDeclaration)
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
    /// Gets the leaf array items type of a higher-rank array.
    /// </summary>
    /// <param name="that">The type declaration for which to get the leaf array items type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the deepest-nested array.</returns>
    public static ArrayItemsTypeDeclaration? LeafArrayItemsType(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(LeafArrayItemsType), out ArrayItemsTypeDeclaration? leafArrayItemsType))
        {
            leafArrayItemsType = that.ArrayItemsType();

            // Set the value before we correct for the child (to protect against
            // recursive references)
            that.SetMetadata(nameof(LeafArrayItemsType), leafArrayItemsType);

            if (
                leafArrayItemsType is ArrayItemsTypeDeclaration childItemsType &&
                (childItemsType.ReducedType.ImpliedCoreTypes() & CoreTypes.Array) != 0)
            {
                // Overwrite the value if our items type was not in fact that leaf
                that.SetMetadata(nameof(LeafArrayItemsType), childItemsType.ReducedType.LeafArrayItemsType());
            }
        }

        return leafArrayItemsType;
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

        static int? GetArrayDimension(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return null;
            }

            int minimumValue = int.MaxValue;
            int maximumValue = 0;

            foreach (IArrayLengthConstantValidationKeyword keyword in
                        typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IArrayLengthConstantValidationKeyword>())
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
                        default:
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
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
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
    /// Gets the type of a tuple, or <see langword="null"/> if the type is not
    /// a strongly-typed tuple.
    /// </summary>
    /// <param name="that">The type declaration for which to get the array type.</param>
    /// <returns>The <see cref="ArrayItemsTypeDeclaration"/> for the strongly typed array items.</returns>
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
            isInDefinitionsContainer = IsInDefinitionsContainer(that);
            that.SetMetadata(nameof(IsInDefinitionsContainer), isInDefinitionsContainer);
        }

        return isInDefinitionsContainer;

        static bool IsInDefinitionsContainer(TypeDeclaration typeDeclaration)
        {
            JsonReference reference = typeDeclaration.LocatedSchema.Location;

            foreach (IKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IDefinitionsKeyword>())
            {
                if (reference.HasFragment && reference.Fragment.Length > 1 && reference.Fragment.LastIndexOf('/') == keyword.Keyword.Length + 2 && reference.Fragment[2..].StartsWith(keyword.Keyword.AsSpan()))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires property evaluation tracking.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires property evaluation tracking.</returns>
    public static bool RequiresItemsEvaluationTracking(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresItemsEvaluationTracking during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresItemsEvaluationTracking), out bool requiresTracking))
        {
            requiresTracking = RequiresItemsEvaluationTracking(that);
            that.SetMetadata(nameof(RequiresItemsEvaluationTracking), requiresTracking);
        }

        return requiresTracking;

        static bool RequiresItemsEvaluationTracking(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IArrayValidationKeyword>().Any(k => k.RequiresItemsEvaluationTracking(typeDeclaration));
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
            requiresTracking = RequiresPropertyEvaluationTracking(that);
            that.SetMetadata(nameof(RequiresPropertyEvaluationTracking), requiresTracking);
        }

        return requiresTracking;

        static bool RequiresPropertyEvaluationTracking(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IObjectValidationKeyword>().Any(k => k.RequiresPropertyEvaluationTracking(typeDeclaration));
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
            requiresJsonValueKind = RequiresJsonValueKind(that);
            that.SetMetadata(nameof(RequiresJsonValueKind), requiresJsonValueKind);
        }

        return requiresJsonValueKind;

        static bool RequiresJsonValueKind(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IValueKindValidationKeyword>().Any(k => typeDeclaration.HasKeyword(k));
        }
    }

    /// <summary>
    /// Gets the single constant value for an instance.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns>The single constant value for the instance, or <see langword="default"/> if there is
    /// no single constant value.</returns>
    public static JsonElement SingleConstantValue(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use DefaultValue during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(SingleConstantValue), out JsonElement? singleConstantValue))
        {
            singleConstantValue = GetSingleConstantValue(that);
            that.SetMetadata(nameof(SingleConstantValue), singleConstantValue);
        }

        return singleConstantValue!.Value;

        static JsonElement GetSingleConstantValue(
            TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return default;
            }

            foreach (
                ISingleConstantValidationKeyword keyword in
                typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<ISingleConstantValidationKeyword>())
            {
                if (keyword.TryGetConstantValue(typeDeclaration, out JsonElement defaultValue))
                {
                    return defaultValue;
                }
            }

            return default;
        }
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

            foreach (
                IDefaultValueProviderKeyword keyword in
                typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IDefaultValueProviderKeyword>())
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
            if (!TryGetValidationKeywords(that, out validationKeywords))
            {
                validationKeywords = [];
            }

            that.SetMetadata(nameof(ValidationKeywords), validationKeywords);
        }

        return validationKeywords ?? [];

        static bool TryGetValidationKeywords(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyCollection<IValidationKeyword>? validationKeywords)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                validationKeywords = null;
                return false;
            }

            var keywords =
                typeDeclaration.LocatedSchema.Vocabulary.Keywords
                    .OfType<IValidationKeyword>()
                    .Where(k => typeDeclaration.HasKeyword(k)).ToList();

            if (keywords.Count > 0)
            {
                validationKeywords = keywords;
                return true;
            }

            validationKeywords = null;
            return false;
        }
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
            if (!TryGetDefinitionsKeywords(that, out definitionKeywords))
            {
                definitionKeywords = [];
            }

            that.SetMetadata(nameof(DefinitionsKeywords), definitionKeywords);
        }

        return definitionKeywords ?? [];

        static bool TryGetDefinitionsKeywords(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyCollection<IDefinitionsKeyword>? definitionsKeywords)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                definitionsKeywords = null;
                return false;
            }

            var keywords =
                typeDeclaration.LocatedSchema.Vocabulary.Keywords
                    .OfType<IDefinitionsKeyword>()
                    .Where(k => typeDeclaration.HasKeyword(k)).ToList();

            if (keywords.Count > 0)
            {
                definitionsKeywords = keywords;
                return true;
            }

            definitionsKeywords = null;
            return false;
        }
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
            if (!TryGetTypeNameProviderKeywords(that, out typeNameProviderKeywords))
            {
                typeNameProviderKeywords = [];
            }

            that.SetMetadata(nameof(TypeNameProviderKeywords), typeNameProviderKeywords);
        }

        return typeNameProviderKeywords ?? [];

        static bool TryGetTypeNameProviderKeywords(
            TypeDeclaration typeDeclaration,
            [NotNullWhen(true)] out IReadOnlyCollection<ITypeNameProviderKeyword>? typeNameProviderKeywords)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                typeNameProviderKeywords = null;
                return false;
            }

            var keywords =
                typeDeclaration.LocatedSchema.Vocabulary.Keywords
                    .OfType<ITypeNameProviderKeyword>()
                    .Where(k => typeDeclaration.HasKeyword(k)).ToList();

            if (keywords.Count > 0)
            {
                typeNameProviderKeywords = keywords;
                return true;
            }

            typeNameProviderKeywords = null;
            return false;
        }
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

            foreach (IValidationConstantProviderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IValidationConstantProviderKeyword>())
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

            foreach (IValidationRegexProviderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IValidationRegexProviderKeyword>())
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

    private static void SetFormat(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.HasSiblingHidingKeyword())
        {
            typeDeclaration.SetMetadata(nameof(ExplicitFormat), default(string?));
            typeDeclaration.SetMetadata(nameof(Format), default(string?));
            return;
        }

        string? foundFormat = null;

        foreach (IFormatProviderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IFormatProviderKeyword>())
        {
            if (keyword.TryGetFormat(typeDeclaration, out string? value) &&
                value is string format)
            {
                if (foundFormat is null)
                {
                    // We don't have an existing format,
                    foundFormat = format;
                }
                else if (format.Equals(format, StringComparison.Ordinal))
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

        // Now go through all the allOf union types and see if we can find one
        foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IAllOfSubschemaValidationKeyword>())
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
                    k.TryGetArrayItemsType(t, out v),
                static t => t.NonTupleItemsType());

        ArrayItemsTypeDeclaration? unevaluatedItems =
            GetItemsType(
                typeDeclaration,
                static (IUnevaluatedArrayItemsTypeProviderKeyword k, TypeDeclaration t, out ArrayItemsTypeDeclaration? v) =>
                    k.TryGetArrayItemsType(t, out v),
                static t => t.ExplicitUnevaluatedItemsType());

        bool isTuple = tupleType is not null && DeniesNonTupleItems(nonTupleItems);

        if (isTuple)
        {
            typeDeclaration.SetMetadata(nameof(TupleType), tupleType);
        }
        else
        {
            typeDeclaration.SetMetadata(nameof(TupleType), default(TupleTypeDeclaration?));
        }

        if (tupleType is TupleTypeDeclaration t && t.IsExplicit)
        {
            typeDeclaration.SetMetadata(nameof(ExplicitTupleType), t);
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
            KeywordAccessor<T> keywordAccessor,
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

            foreach (T keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<T>())
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
            foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IAllOfSubschemaValidationKeyword>())
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
                            declaration = new(referencedArrayItemsTypeDeclaration.UnreducedType, false);
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

            foreach (ITupleTypeProviderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<ITupleTypeProviderKeyword>())
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
            foreach (IAllOfSubschemaValidationKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IAllOfSubschemaValidationKeyword>())
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
                            declaration = new(referencedTupleTypeDeclaration.UnreducedTypes, false);
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
                            // We have two implicit items-defnining keywords, that are maybe compatible
                            // One day, we could do analysis to analytically produce a
                            // lowest-common-denominator type for these cases.
                            // TODO: we should consider logging
                            return null;
                        }
                    }
                }
            }

            return declaration;
        }
    }

    private static bool DeniesNonTupleItems(ArrayItemsTypeDeclaration? nonTupleItems)
    {
        // We could provide a more sophisticated approach in future.
        return nonTupleItems?.ReducedType.LocatedSchema.Schema.ValueKind == JsonValueKind.False;
    }
}
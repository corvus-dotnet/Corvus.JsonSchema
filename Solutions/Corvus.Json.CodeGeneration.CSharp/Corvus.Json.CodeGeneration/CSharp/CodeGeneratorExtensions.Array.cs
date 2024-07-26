// <copyright file="CodeGeneratorExtensions.Array.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Append the static property which provides an empty array instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEmptyArrayInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("EmptyArray")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets an empty array.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" EmptyArray { get; } = From(ImmutableList<JsonAny>.Empty);");
    }

    /// <summary>
    /// Append the static property which provides the rank of the array.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayRankStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayRank() is int rank)
        {
            generator
                .ReserveName("Rank")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets the rank of the array.
                    /// </summary>
                    """)
                .AppendIndent("public static int Rank => ")
                .Append(rank)
                .AppendLine(";");
        }

        return generator;
    }

    /// <summary>
    /// Append the static property which provides the dimension of the array.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayDimensionStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayDimension() is int dimension)
        {
            generator
                .ReserveName("Dimension")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets the dimension of the array.
                    /// </summary>
                    """)
                .AppendIndent("public static int Dimension => ")
                .Append(dimension)
                .AppendLine(";");
        }

        return generator;
    }

    /// <summary>
    /// Append the static property which provides the value buffer size of the array.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayValueBufferSizeStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayValueBufferSize() is int valueBufferSize)
        {
            generator
                .ReserveName("ValueBufferSize")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets the total size of a buffer required to represent the array.
                    /// </summary>
                    /// <remarks>
                    /// This calculates the array based on the dimension of each rank. It is generally
                    /// used to determine the size of the buffer required by
                    /// <see cref="TryGetNumericValues"/>.
                    /// </remarks>
                    """)
                .AppendIndent("public static int ValueBufferSize => ")
                .Append(valueBufferSize)
                .AppendLine(";");
        }

        return generator;
    }

    /// <summary>
    /// Append the TryGetNumericValues() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetNumericValuesMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is not ArrayItemsTypeDeclaration arrayItemsType ||
            arrayItemsType.ReducedType.PreferredDotnetNumericTypeName() is not string preferredNumericTypeName ||
            typeDeclaration.ArrayRank() is not int arrayRank)
        {
            return generator;
        }

        string arrayItemsTypeName = arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName();

        if (typeDeclaration.ArrayValueBufferSize() is int valueBufferSize)
        {
            generator
                .ReserveName("TryGetNumericValues")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <param name="items">The <see cref="Span{T}"/> to fill with the values in the array.</param>
                    /// <param name="written">The number of values written.</param>
                    /// <returns><see langword="true"/> if the array was written successfully, otherwise <see langword="false" />.</returns>
                    /// <remarks>
                    /// You can determine the size of the array to fill by interrogating
                    /// <see cref="ArrayBufferSize"/>
                    /// </remarks>
                    """)
                .AppendLineIndent("public bool TryGetNumericValues(Span<", preferredNumericTypeName, "> items, out int written)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (items.Length < ValueBufferSize)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("written = 0;")
                        .AppendLineIndent("return false;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("int index = 0;")
                    .AppendConditionalBackingValueCallbackIndent(
                        "Backing.Array",
                        "arrayBacking",
                        (g, f) => AppendForArrayBacking(g, f, preferredNumericTypeName, arrayItemsTypeName, arrayRank))
                    .AppendSeparatorLine()
                    .AppendConditionalBackingValueCallbackIndent(
                        "Backing.JsonElement",
                        "jsonElementBacking",
                        (g, f) => AppendForJsonElementBacking(g, f, preferredNumericTypeName, arrayItemsTypeName, arrayRank))
                    .AppendSeparatorLine()
                    .AppendLineIndent("written = 0;")
                    .AppendLineIndent("return false;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;

        static void AppendForJsonElementBacking(CodeGenerator generator, string fieldName, string preferredNumericTypeName, string arrayItemsTypeName, int arrayRank)
        {
            generator
                .AppendLineIndent("if (this.", fieldName, ".ValueKind != JsonValueKind.Number)")
                .AppendBlockIndent(
                    """
                    {
                        written = 0;
                        return false;
                    }
                    """)
                .AppendSeparatorLine()
                .AppendLineIndent("foreach (JsonElement item in this.", fieldName, ".EnumerateArray())")
                .AppendLineIndent("{")
                .PushIndent();

            if (arrayRank == 1)
            {
                string dotNetTypeName = FormatProviderRegistry.Instance.NumberTypeFormatProviders.GetDotnetTypeNameForCSharpNumericLangwordOrTypeName(preferredNumericTypeName) ?? "Double";
                generator
                    .AppendLineIndent(
                        "if (item.ValueKind != JsonValueKind.Number || !item.TryGet",
                        dotNetTypeName,
                        "(out ",
                        preferredNumericTypeName,
                        " value))")
                    .AppendBlockIndent(
                        """
                        {
                            written = 0;
                            return false;
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendLineIndent("items[index++] = value;");
            }
            else
            {
                generator
                    .AppendLineIndent(arrayItemsTypeName, " child = ", arrayItemsTypeName, ".FromJson(item);")
                    .AppendBlockIndent(
                        """
                        if (!child.TryGetNumericValues(items[index..], out int writtenChildren))
                        {
                            written = 0;
                            return false;
                        }

                        index += writtenChildren;
                        """);
            }

            generator
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("written = index;")
                .AppendLineIndent("return true;");
        }

        static void AppendForArrayBacking(CodeGenerator generator, string fieldName, string preferredNumericTypeName, string arrayItemsTypeName, int arrayRank)
        {
            generator
                .AppendLineIndent("foreach (", arrayItemsTypeName, " item in this.", fieldName, ")")
                .AppendLineIndent("{")
                .PushIndent();

            if (arrayRank == 1)
            {
                generator
                    .AppendBlockIndent(
                        """
                        if (item.ValueKind != JsonValueKind.Number)
                        {
                            written = 0;
                            return false;
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendLineIndent("items[index++] = (", preferredNumericTypeName, ")item;");
            }
            else
            {
                generator
                    .AppendBlockIndent(
                        """
                        if (!item.TryGetNumericValues(items[index..], out int writtenChildren))
                        {
                            written = 0;
                            return false;
                        }

                        index += writtenChildren;
                        """);
            }

            generator
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("written = index;")
                .AppendLineIndent("return true;");
        }
    }

    /// <summary>
    /// Append array indexer properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayIndexerProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine();

        if (typeDeclaration.ArrayItemsType() is null &&
            typeDeclaration.TupleType() is null)
        {
            generator
                .AppendLineIndent(
                    "/// <inheritdoc/>")
                .AppendArrayIndexer(typeDeclaration, WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName(), isExplicit: false);
        }
        else
        {
            generator
                .AppendLineIndent(
                    "/// <inheritdoc/>")
                .AppendArrayIndexer(typeDeclaration, WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName(), isExplicit: true);

            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration itemsType)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets the item at the given index.
                    /// </summary>
                    /// <param name="index">The index at which to retrieve the item.</param>
                    /// <returns>The item at the given index.</returns>
                    /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
                    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
                    """)
                    .AppendArrayIndexer(typeDeclaration, itemsType.ReducedType.FullyQualifiedDotnetTypeName(), isExplicit: false);
            }
        }

        return generator;
    }

    /// <summary>
    /// Append properties for tuple items.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the properties.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTupleItemProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TupleType() is TupleTypeDeclaration tupleType)
        {
            generator
                .AppendSeparatorLine();

            for (int i = 0; i < tupleType.ItemsTypes.Length; i++)
            {
                TypeDeclaration tupleItem = tupleType.ItemsTypes[i].ReducedType;
                generator
                    .ReserveName($"Item{i + 1}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendIndent("/// Gets the tuple item as a ")
                    .AppendTypeAsSeeCref(tupleItem.FullyQualifiedDotnetTypeName())
                    .AppendLine(".")
                    .AppendLineIndent("/// </summary>")
                    .AppendIndent("public ")
                    .Append(tupleItem.FullyQualifiedDotnetTypeName())
                    .Append(" Item")
                    .Append(i + 1)
                    .AppendLine()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("get")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendConditionalBackingValueCallbackIndent(
                                "Backing.JsonElement",
                                "jsonElementBacking",
                                (g, f) => AppendJsonElementTupleAccessor(g, f, i))
                            .AppendConditionalBackingValueCallbackIndent(
                                "Backing.Array",
                                "arrayBacking",
                                (g, f) => AppendArrayTupleAccessor(g, f, i, tupleItem.FullyQualifiedDotnetTypeName()))
                            .AppendSeparatorLine()
                            .AppendLineIndent("return default;")
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;

        static void AppendJsonElementTupleAccessor(CodeGenerator generator, string fieldName, int index)
        {
            generator
                .AppendIndent("return new(this.")
                .Append(fieldName)
                .Append("[")
                .Append(index)
                .AppendLine("]);");
        }

        static void AppendArrayTupleAccessor(CodeGenerator generator, string fieldName, int index, string itemsTypeName)
        {
            generator
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendIndent("return this.")
                .Append(fieldName)
                .Append('[')
                .Append(index)
                .Append(']');

            if (itemsTypeName != WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName())
            {
                generator
                    .Append(".As<")
                    .Append(itemsTypeName)
                    .Append(">()");
            }

            generator
                .AppendLine(";")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("catch (ArgumentOutOfRangeException ex)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent("throw new IndexOutOfRangeException(ex.Message, ex);")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Append array Add methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayAddMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        bool isTupleOrHasArrayItemsType = typeDeclaration.IsTuple() || typeDeclaration.ArrayItemsType() is not null;

        return generator
            .ReserveNameIfNotReserved("Add")
            .ReserveNameIfNotReserved("Insert")
            .ReserveNameIfNotReserved("AddRange")
            .ReserveNameIfNotReserved("InsertRange")
            .AppendAddItemJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendAddParamsJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendAddRangeTArray(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendAddRangeEnumerableTItem(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendAddRangeEnumerableJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendInsertItemJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendInsertRangeTArray(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendInsertRangeEnumerableTItem(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendInsertRangeEnumerableJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendReplaceJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendSetItemJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendAddItemArrayItemsType(typeDeclaration)
            .AppendAddParamsArrayItemsType(typeDeclaration)
            .AppendAddRangeEnumerableArrayItemsType(typeDeclaration)
            .AppendInsertItemArrayItemsType(typeDeclaration)
            .AppendInsertRangeEnumerableArrayItemsType(typeDeclaration)
            .AppendReplaceArrayItemsType(typeDeclaration)
            .AppendSetItemArrayItemsType(typeDeclaration);
    }

    /// <summary>
    /// Append array Remove methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayRemoveMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        bool isTupleOrHasArrayItemsType = typeDeclaration.IsTuple() || typeDeclaration.ArrayItemsType() is not null;

        return generator
            .ReserveNameIfNotReserved("Remove")
            .ReserveNameIfNotReserved("RemoveAt")
            .ReserveNameIfNotReserved("RemoveRange")
            .AppendRemoveJsonAny(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendRemoveAt(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendRemoveRange(typeDeclaration, isTupleOrHasArrayItemsType)
            .AppendRemoveArrayItemsType(typeDeclaration);
    }

    /// <summary>
    /// Append the __CorvusArrayHelpers private class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the properties.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCorvusArrayHelpers(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("__CorvusArrayHelpers")
            .AppendSeparatorLine()
            .AppendLineIndent("private static class __CorvusArrayHelpers")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <returns>An immutable list of <see cref = "JsonAny"/> built from the array.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableList(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return ", "arrayBacking", ";", identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("return GetImmutableListBuilder(arrayInstance).ToImmutable();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}.Builder"/> from the array.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <returns>An immutable list builder of <see cref = "JsonAny"/>, built from the existing array.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny>.Builder GetImmutableListBuilder(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendBuildImmutableList, identifier: "arrayInstance")
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return ", "arrayBacking", ".ToBuilder();", identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, replacing the item at the specified index with the given item.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <param name="index">The index at which to add the element.</param>
                /// <param name="value">The value to add.</param>
                /// <returns>An immutable list containing the contents of the list, with the specified item at the index.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableListSetting(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance, int index, in JsonAny value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendGetImmutableListSettingIndexToValue, identifier: "arrayInstance")
                    .AppendConditionalBackingValueCallbackIndent("Backing.Array", "arrayBacking", AppendSetItemAtIndexToValue, identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, removing the first item that equals the given value, and replacing it with the specified item.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <param name="oldItem">The item to remove.</param>
                /// <param name="newItem">The item to insert.</param>
                /// <returns>An immutable list containing the contents of the list, without the first instance that matches the old item, replacing it with the new item.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableListReplacing(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance, in JsonAny oldItem, in JsonAny newItem)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendGetImmutableListReplacingValue, identifier: "arrayInstance")
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return  ", "arrayBacking", ".Replace(oldItem, newItem);", identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, removing the first item arrayInstance equals the given value.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <param name="item">The item to remove.</param>
                /// <returns>An immutable list containing the contents of the list, without the first instance arrayInstance matches the given item.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableListWithout(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance, in JsonAny item)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendGetImmutableListWithout, identifier: "arrayInstance")
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return  ", "arrayBacking", ".Remove(item);", identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, removing the given range.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <param name="index">The start index of the range to remove.</param>
                /// <param name="count">The length of the range to remove.</param>
                /// <returns>An immutable list containing the contents of the list, without the given range of items.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableListWithoutRange(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance, int index, int count)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendGetImmutableListWithoutRange, identifier: "arrayInstance")
                    .AppendConditionalBackingValueCallbackIndent("Backing.Array", "arrayBacking", AppendRemoveRange, identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                // <summary>
                // Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, inserting the given item at the index.
                // </summary>
                // <param name="arrayInstance">The array instance.</param>
                // <param name="index">The index at which to add the element.</param>
                // <param name="value">The value to add.</param>
                // <returns>An immutable list containing the contents of the list, without the array.</returns>
                // <exception cref = "InvalidOperationException">The value is not an array.</exception>
                // <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableListWith(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance, int index, in JsonAny value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendGetImmutableListInserting, identifier: "arrayInstance")
                    .AppendConditionalBackingValueCallbackIndent("Backing.Array", "arrayBacking", AppendInsertAtIndex, identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref = "ImmutableList{JsonAny}"/> from the array, inserting the items at the
                /// given index.
                /// </summary>
                /// <param name="arrayInstance">The array instance.</param>
                /// <param name="index">The index at which to add the element.</param>
                /// <param name="values">The values to add.</param>
                /// <returns>An immutable list containing the contents of the list, without the array.</returns>
                /// <exception cref = "InvalidOperationException">The value is not an array.</exception>
                /// <exception cref = "IndexOutOfRangeException">Thrown if the range is beyond the bounds of the array.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonAny> GetImmutableListWith<TEnumerable>(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" arrayInstance, int index, TEnumerable values)")
                .PushIndent()
                    .AppendLineIndent("where TEnumerable : IEnumerable<JsonAny>")
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendGetImmutableListInsertingEnumerable, identifier: "arrayInstance")
                    .AppendConditionalBackingValueCallbackIndent("Backing.Array", "arrayBacking", AppendInsertEnumerable, identifier: "arrayInstance")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");

        static void AppendGetImmutableListInsertingEnumerable(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.GetImmutableListFromJsonElementWith(arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(", index, values);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendInsertEnumerable(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(".InsertRange(index, values);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("catch (ArgumentOutOfRangeException ex)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("throw new IndexOutOfRangeException(ex.Message, ex);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendGetImmutableListInserting(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.GetImmutableListFromJsonElementWith(arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(", index, value);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendInsertAtIndex(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(".Insert(index, value);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("catch (ArgumentOutOfRangeException ex)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("throw new IndexOutOfRangeException(ex.Message, ex);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendGetImmutableListWithoutRange(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(", index, count);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendRemoveRange(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(".RemoveRange(index, count);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("catch (ArgumentOutOfRangeException ex)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("throw new IndexOutOfRangeException(ex.Message, ex);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendGetImmutableListWithout(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.GetImmutableListFromJsonElementWithout(arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(", item);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendGetImmutableListReplacingValue(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.GetImmutableListFromJsonElementReplacing(arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(", oldItem, newItem);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendGetImmutableListSettingIndexToValue(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.GetImmutableListFromJsonElementSetting(arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(", index, value);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendSetItemAtIndexToValue(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return arrayInstance.")
                    .Append(fieldName)
                    .AppendLine(".SetItem(index, value);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("catch (ArgumentOutOfRangeException ex)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("throw new IndexOutOfRangeException(ex.Message, ex);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendBuildImmutableList(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendIndent("if (arrayInstance.")
                .Append(fieldName)
                .AppendLine(".ValueKind == JsonValueKind.Array)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendBlockIndent(
                    """
                    ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                    foreach (JsonElement item in arrayInstance.jsonElementBacking.EnumerateArray())
                    {
                        builder.Add(new(item));
                    }

                    return builder;
                    """)
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Append the EnunmerateArray() methods.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEnumerateArrayMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        bool hasStrongType = false;

        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            hasStrongType = true;
            generator
                .ReserveNameIfNotReserved("EnumerateArray")
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public JsonArrayEnumerator<")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine("> EnumerateArray()")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return new(", "jsonElementBacking", ");")
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return new(", "arrayBacking", ");")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}");
        }

        generator
            .ReserveNameIfNotReserved("EnumerateArray")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>");

        if (hasStrongType)
        {
            generator
                .AppendIndent("JsonArrayEnumerator ")
                .Append("IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }
        else
        {
            generator
                .AppendIndent("public JsonArrayEnumerator ");
        }

        generator
            .AppendLine("EnumerateArray()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return new(", "jsonElementBacking", ");")
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return new(", "arrayBacking", ");")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        generator
            .ReserveNameIfNotReserved("EnumerateArray")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>");

        if (hasStrongType)
        {
            generator
                .AppendIndent("JsonArrayEnumerator<TItem> ")
                .Append("IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.EnumerateArray<TItem>()");
        }
        else
        {
            generator
                .AppendIndent("public JsonArrayEnumerator<TItem> ")
                .AppendLine("EnumerateArray<TItem>()")
                .PushIndent()
                    .AppendLineIndent("where TItem : struct, IJsonValue<TItem>")
                .PopIndent();
        }

        generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return new(", "jsonElementBacking", ");")
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return new(", "arrayBacking", ");")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    /// <summary>
    /// Append the GetArrayLength() method.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetArrayLengthMethod(this CodeGenerator generator)
    {
        return generator
            .ReserveNameIfNotReserved("GetArrayLength")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public int GetArrayLength()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return ", "jsonElementBacking", ".GetArrayLength();")
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Array", "return ", "arrayBacking", ".Count;")
                .AppendSeparatorLine()
                .AppendLineIndent("return 0;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the immutable list creation methods.
    /// </summary>
    /// <param name="generator">The generator to which to append the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsImmutableListMethods(this CodeGenerator generator)
    {
        return generator
            .ReserveNameIfNotReserved("AsImmutableList")
            .ReserveNameIfNotReserved("AsImmutableListBuilder")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <inheritdoc/>
            public ImmutableList<JsonAny> AsImmutableList()
            {
                return __CorvusArrayHelpers.GetImmutableList(this);
            }

            /// <inheritdoc/>
            public ImmutableList<JsonAny>.Builder AsImmutableListBuilder()
            {
                return __CorvusArrayHelpers.GetImmutableListBuilder(this);
            }
            """);
    }

    /// <summary>
    /// Append the collection enumerable methods.
    /// </summary>
    /// <param name="generator">The generator to which to append the methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append collection enumerable methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCollectionEnumerableMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.IsTuple())
        {
            return generator;
        }

        string arrayItemsType =
            typeDeclaration.ArrayItemsType()?.ReducedType.FullyQualifiedDotnetTypeName()
            ?? WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName();

        return generator
            .ReserveNameIfNotReserved("GetEnumerator")
            .ReserveNameIfNotReserved("Count")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendIndent("IEnumerator<")
            .Append(arrayItemsType)
            .Append("> IEnumerable<")
            .Append(arrayItemsType)
            .AppendLine(">.GetEnumerator() => this.EnumerateArray();")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("IEnumerator IEnumerable.GetEnumerator() => this.EnumerateArray();")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendIndent("int IReadOnlyCollection<")
            .Append(arrayItemsType)
            .AppendLine(">.Count => this.GetArrayLength();");
    }

    /// <summary>
    /// Append the tuple createion factory method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the factory method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCreateTupleFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TupleType() is TupleTypeDeclaration tupleType)
        {
            generator
                .ReserveNameIfNotReserved("Create")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Create a tuple from the given items.
                /// </summary>
                """);

            for (int i = 0; i < tupleType.ItemsTypes.Length; ++i)
            {
                generator
                    .AppendIndent("/// <param name=\"item")
                    .Append(i + 1)
                    .Append("\">The value for the ")
                    .AppendOrdinalName(i + 1)
                    .AppendLine(" item.</param>");
            }

            generator
                .AppendLineIndent("/// <returns>The new tuple created from the items.</returns>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Create(");

            for (int i = 0; i < tupleType.ItemsTypes.Length; ++i)
            {
                if (i > 0)
                {
                    generator.Append(", ");
                }

                generator
                    .Append("in ")
                    .Append(tupleType.ItemsTypes[i].ReducedType.FullyQualifiedDotnetTypeName())
                    .Append(" item")
                    .Append(i + 1);
            }

            generator
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return new([")
                    .AppendCommaSeparatedNumericSuffixItems("item", tupleType.ItemsTypes.Length)
                    .AppendLine("]);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    /// <summary>
    /// Append conversions for tuples.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the properties.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTupleConversions(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // We can only do ValueTuple<T1,T2> so there must be more than two items in tuple.
        if (typeDeclaration.TupleType() is TupleTypeDeclaration tupleType &&
            tupleType.ItemsTypes.Length > 1)
        {
            generator
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Conversion to tuple.
                    /// </summary>
                    /// <param name="value">The value from which to convert.</param>
                    """)
                .AppendIndent("public static implicit operator (")
                .AppendCommaSeparatedTupleTypes(tupleType)
                .Append(")(")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return (")
                    .AppendCommaSeparatedValueItems(tupleType)
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Conversion from tuple.
                    /// </summary>
                    /// <param name="value">The value from which to convert.</param>
                    """)
                .AppendIndent("public static implicit operator ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append("((")
                .AppendCommaSeparatedTupleTypes(tupleType)
                .AppendLine(") value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return ")
                    .Append(typeDeclaration.DotnetTypeName())
                    .Append(".Create(")
                    .AppendCommaSeparatedValueItems(tupleType)
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    /// <summary>
    /// Appends a factory method to produce an instance of the type
    /// from an immutable list of JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to create the method..</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromImmutableListOfJsonAnyFactoryMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("From")
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"items\">The list of items from which to construct the array.</param>")
            .AppendLineIndent("/// <returns>An instance of the array constructed from the list.</returns>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" From(ImmutableList<JsonAny> items)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return new(items);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a factory method to produce an instance of the type
    /// from a span of the array items type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to create the method..</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCreateFromSpanFactoryMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        // Don't generate this if we have a tuple type or a fixed-size numeric array.
        if (typeDeclaration.IsFixedSizeArray() || typeDeclaration.IsTuple())
        {
            return generator;
        }

        string arrayItemsType =
            typeDeclaration.ArrayItemsType()?.ReducedType.FullyQualifiedDotnetTypeName()
                ?? WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName();

        return generator
            .ReserveNameIfNotReserved("Create")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Create an new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine("\" struct from a span of items.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"items\">The span of items from which to construct the array.</param>")
            .AppendLineIndent("/// <returns>An instance of the array constructed from the span.</returns>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" Create(ReadOnlySpan<")
            .Append(arrayItemsType)
            .Append("> items)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return new([..items]);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append factory methods to produce an instance of the type
    /// from various collections of items.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to create the method..</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromItemsFactoryMethods(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.IsFixedSizeArray() || typeDeclaration.IsTuple())
        {
            return generator;
        }

        string arrayItemsType =
            typeDeclaration.ArrayItemsType()?.ReducedType.FullyQualifiedDotnetTypeName()
            ?? WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName();

        generator
            .ReserveNameIfNotReserved("FromItems")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"items\">The value from which to construct the instance.</param>")
            .AppendLineIndent("/// <returns>An instance of the array constructed from the value.</returns>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" FromItems(params ")
            .Append(arrayItemsType)
            .AppendLine("[] items)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendReturnNewArrayTypeBuiltFromValue("items")
            .PopIndent()
            .AppendLineIndent("}");

        if (arrayItemsType == WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName())
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendIndent("/// Initializes a new instance of the ")
                .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
                .AppendLine(" struct.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <typeparam name=\"TItem\">The type of the items in the list.</typeparam>")
                .AppendLineIndent("/// <param name=\"items\">The value from which to construct the instance.</param>")
                .AppendLineIndent("/// <returns>An instance of the array constructed from the value.</returns>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" FromItems<TItem>(params TItem[] items)")
                .PushIndent()
                    .AppendLineIndent("where TItem : struct, IJsonValue<TItem>")
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendReturnNewArrayTypeBuiltFromValue("items.Select(item => item.AsAny)")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendGenericFromItemsFactoryMethods(typeDeclaration);
        }
        else
        {
            generator
                .AppendNonGenericFromItemsFactoryMethods(typeDeclaration, arrayItemsType);
        }

        return generator;
    }

    /// <summary>
    /// Append factory methods to produce an instance of the type
    /// from various enumerables of items.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to create the method..</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromRangeFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        string arrayItemsType =
            typeDeclaration.ArrayItemsType()?.ReducedType.FullyQualifiedDotnetTypeName()
            ?? WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName();

        return generator
            .AppendFromRangeFactoryMethods(typeDeclaration, arrayItemsType)
            .AppendFromRangeBuiltInTypeFactoryMethod(typeDeclaration, "string")
            .AppendFromRangeBuiltInTypeFactoryMethod(typeDeclaration, "double")
            .AppendFromRangeBuiltInTypeFactoryMethod(typeDeclaration, "float")
            .AppendFromRangeBuiltInTypeFactoryMethod(typeDeclaration, "int")
            .AppendFromRangeBuiltInTypeFactoryMethod(typeDeclaration, "long")
            .AppendFromRangeBuiltInTypeFactoryMethod(typeDeclaration, "bool");
    }

    /// <summary>
    /// Append a factory method to produce an instance of the type
    /// from a span of values.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to create the method..</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromValuesFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.IsFixedSizeNumericArray())
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("FromValues")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Creates an instance of the array of rank ")
            .Append(typeDeclaration.ArrayRank())
            .Append(" and dimension ")
            .Append(typeDeclaration.ArrayDimension())
            .AppendLine(" from the given values.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"values\">The numeric values from which to create the array.</param>")
            .AppendLineIndent("/// <returns>An instance of the array populated from the given values.</returns>")
            .AppendLineIndent("/// <remarks>")
            .AppendIndent("/// The <paramref name=\"values\"> span should be of length ")
            .Append(typeDeclaration.ArrayValueBufferSize())
            .AppendLine(".")
            .AppendLineIndent("/// </remarks>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" FromValues(ReadOnlySpan<")
            .Append(typeDeclaration.ArrayItemsType()!.ReducedType.PreferredDotnetNumericTypeName())
            .AppendLine("> values)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (values.Length != ValueBufferSize)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("throw new ArgumentException(nameof(values));")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();")
                .AppendLineIndent("int index = 0;")
                .AppendBuildNumericArray(typeDeclaration)
                .AppendSeparatorLine()
                .AppendLineIndent("return new(builder.ToImmutable());")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a factory method to produce an instance of the type
    /// from a serialized enumerable of items.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to create the method..</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromSerializedItemsMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.IsFixedSizeArray() || typeDeclaration.IsTuple() || typeDeclaration.ArrayItemsType() is not null)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Create an array from the given items.
            /// </summary>
            /// <typeparam name="T">The type of the <paramref name = "items"/> from which to create the array.</typeparam>
            /// <param name="items">The items from which to create the array.</param>
            /// <returns>The new array created from the items.</returns>
            /// <remarks>
            /// This will serialize the items to create the underlying array. Note the
            /// other overloads which avoid this serialization step.
            /// </remarks>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" From<T>(IEnumerable<T> items)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
                var abw = new ArrayBufferWriter<byte>();
                using var writer = new Utf8JsonWriter(abw);

                foreach (T item in items)
                {
                    writer.Reset();
                    JsonSerializer.Serialize(writer, item);
                    writer.Flush();
                    builder.Add(JsonAny.Parse(abw.WrittenMemory));
                }

                return new(builder.ToImmutable());
                """)
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    /// <summary>
    /// Append a collection builder attribute for NET8_0_OR_GREATER.
    /// </summary>
    /// <param name="generator">The code generator to which to append the attribute.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the attribute.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendDotnet80OrGreaterCollectionBuilderAttribute(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendLine("#if NET8_0_OR_GREATER")
            .AppendIndent("[CollectionBuilder(typeof(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine("), \"Create\")]")
            .AppendLine("#endif");
    }

    private static CodeGenerator AppendArrayIndexer(this CodeGenerator generator, TypeDeclaration typeDeclaration, string itemsTypeName, bool isExplicit)
    {
        if (!isExplicit)
        {
            generator
                .AppendIndent("public ")
                .Append(itemsTypeName)
                .Append(" ");
        }
        else
        {
            generator
                .AppendIndent(itemsTypeName)
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }

        return generator
            .AppendLine("this[int index]")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendConditionalConstructFromBacking(
                    "Backing.JsonElement",
                    "jsonElementBacking")
                .AppendSeparatorLine()
                .AppendConditionalBackingValueCallbackIndent(
                    "Backing.Array",
                    "arrayBacking",
                    (g, fieldName) => AppendArrayItem(g, fieldName, itemsTypeName))
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");

        static void AppendArrayItem(CodeGenerator generator, string fieldName, string itemsTypeName)
        {
            generator
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendIndent("return this.")
                .Append(fieldName)
                .Append("[index]");

            if (itemsTypeName != WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName())
            {
                generator
                    .Append(".As<")
                    .Append(itemsTypeName)
                    .Append(">()");
            }

            generator
                .AppendLine(";")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("catch (ArgumentOutOfRangeException ex)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent("throw new IndexOutOfRangeException(ex.Message, ex);")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    private static CodeGenerator AppendGenericFromItemsFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        for (int i = 1; i <= 7; ++i)
        {
            generator
                .AppendGenericFromItemsFactoryMethod(typeDeclaration, i);
        }

        return generator;
    }

    private static CodeGenerator AppendNonGenericFromItemsFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration, string arrayItemsType)
    {
        for (int i = 1; i <= 7; ++i)
        {
            generator
                .AppendNonGenericFromItemsFactoryMethod(typeDeclaration, arrayItemsType, i);
        }

        return generator;
    }

    private static CodeGenerator AppendFromRangeFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration, string arrayItemsType)
    {
        if (!(typeDeclaration.IsFixedSizeArray() || typeDeclaration.IsTuple()))
        {
            // Adds the strongly typed implementation for either JsonAny or any other type.
            generator
                .AppendSeparatorLine()
                .ReserveNameIfNotReserved("FromRange")
                .AppendLineIndent("/// <summary>")
                .AppendIndent("/// Initializes a new instance of the ")
                .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
                .AppendLine(" struct.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"items\">The items from which to construct the instance.</param>")
                .AppendLineIndent("/// <returns>An instance of the array constructed from the items.</returns>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" FromRange(IEnumerable<")
                .Append(arrayItemsType)
                .AppendLine("> items)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent("return new([..items]);")
                .PopIndent()
                .AppendLineIndent("}");

            if (arrayItemsType == WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName())
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendIndent("/// Initializes a new instance of the ")
                    .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
                    .AppendLine(" struct.")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <typeparam name=\"TItem\">The type of the items to add.</typeparam>")
                    .AppendLineIndent("/// <param name=\"items\">The items from which to construct the instance.</param>")
                    .AppendLineIndent("/// <returns>An instance of the array constructed from the items.</returns>")
                    .AppendIndent("public static ")
                    .Append(typeDeclaration.DotnetTypeName())
                    .AppendLine(" FromRange<TItem>(IEnumerable<TItem> items)")
                    .PushIndent()
                        .AppendLineIndent("where TItem : struct, IJsonValue<TItem>")
                    .PopIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return new([..items.Select(item => item.AsAny)]);")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
            else
            {
                AddExplicitImplementations(generator, typeDeclaration);
            }
        }
        else
        {
            AddExplicitImplementations(generator, typeDeclaration);
        }

        return generator;

        static void AddExplicitImplementations(CodeGenerator generator, TypeDeclaration typeDeclaration)
        {
            generator
                   .ReserveNameIfNotReserved("FromRange")
                   .AppendSeparatorLine()
                   .AppendLine("#if NET8_0_OR_GREATER")
                   .AppendLineIndent("/// <summary>")
                   .AppendIndent("/// Initializes a new instance of the ")
                   .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
                   .AppendLine(" struct.")
                   .AppendLineIndent("/// </summary>")
                   .AppendLineIndent("/// <param name=\"items\">The items from which to construct the instance.</param>")
                   .AppendLineIndent("/// <returns>An instance of the array constructed from the items .</returns>")
                   .AppendIndent("static ")
                   .Append(typeDeclaration.DotnetTypeName())
                   .Append(" IJsonArray<")
                   .Append(typeDeclaration.DotnetTypeName())
                   .AppendLine(">.FromRange(IEnumerable<JsonAny> items)")
                   .AppendLineIndent("{")
                   .PushIndent()
                   .AppendLineIndent("return new([..items]);")
                   .PopIndent()
                   .AppendLineIndent("}")
                   .AppendSeparatorLine()
                   .AppendLineIndent("/// <summary>")
                   .AppendIndent("/// Initializes a new instance of the ")
                   .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
                   .AppendLine(" struct.")
                   .AppendLineIndent("/// </summary>")
                   .AppendLineIndent("/// <typeparam name=\"T\">The type of the items to add.</typeparam>")
                   .AppendLineIndent("/// <param name=\"items\">The items from which to construct the instance.</param>")
                   .AppendLineIndent("/// <returns>An instance of the array constructed from the items.</returns>")
                   .AppendIndent("static ")
                   .Append(typeDeclaration.DotnetTypeName())
                   .Append(" IJsonArray<")
                   .Append(typeDeclaration.DotnetTypeName())
                   .AppendLine(">.FromRange<T>(IEnumerable<T> items)")
                   .AppendLineIndent("{")
                   .PushIndent()
                       .AppendLineIndent("return new([..items.Select(item => item.AsAny)]);")
                   .PopIndent()
                   .AppendLineIndent("}")
                   .AppendLine("#endif");
        }
    }

    private static CodeGenerator AppendNonGenericFromItemsFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string arrayItemsType, int itemCount)
    {
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>");

        for (int i = 1; i <= itemCount; ++i)
        {
            generator
                .AppendIndent("/// <param name=\"item")
                .Append(i)
                .Append("\">The ")
                .AppendOrdinalName(i)
                .AppendLine(" item in the array.</param>");
        }

        return generator
            .AppendLineIndent("/// <returns>An instance of the array constructed from the values.</returns>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" FromItems(")
            .AppendCommaSeparatedInParameterAndNumericSuffixItems(arrayItemsType, "item", itemCount)
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendReturnNewArrayTypeBuiltFromItems(itemCount, isJsonAny: false)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendGenericFromItemsFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, int itemCount)
    {
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>");

        for (int i = 1; i <= itemCount; ++i)
        {
            generator
                .AppendIndent("/// <typeparam name=\"TItem")
                .Append(i)
                .Append("\">The type of the ")
                .AppendOrdinalName(i)
                .AppendLine(" item in the array.</typeparam>");
        }

        for (int i = 1; i <= itemCount; ++i)
        {
            generator
                .AppendIndent("/// <param name=\"item")
                .Append(i)
                .Append("\">The ")
                .AppendOrdinalName(i)
                .AppendLine(" item in the array.</param>");
        }

        generator
            .AppendLineIndent("/// <returns>An instance of the array constructed from the values.</returns>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" FromItems<")
            .AppendCommaSeparatedNumericSuffixItems("TItem", itemCount)
            .Append(">(")
            .AppendCommaSeparatedNumericSuffixItems("in TItem", "item", itemCount)
            .AppendLine(")")
            .PushIndent();

        for (int i = 1; i <= itemCount; ++i)
        {
            generator
                .AppendIndent("where TItem")
                .Append(i)
                .Append(" : struct, IJsonValue<TItem")
                .Append(i)
                .AppendLine(">");
        }

        return generator
            .PopIndent()
            .AppendLineIndent("{")
            .PushIndent()
                .AppendReturnNewArrayTypeBuiltFromItems(itemCount, isJsonAny: false)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendReturnNewArrayTypeBuiltFromItems(this CodeGenerator generator, int itemCount, bool isJsonAny)
    {
        generator
            .AppendSeparatorLine()
            .AppendIndent("return new([");

        for (int i = 1; i <= itemCount; ++i)
        {
            if (i > 1)
            {
                generator.Append(", ");
            }

            generator
                .Append("item")
                .Append(i);

            if (!isJsonAny)
            {
                generator
                    .Append(".AsAny");
            }
        }

        return generator
            .AppendLine("]);");
    }

    private static CodeGenerator AppendBuildNumericArray(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        TypeDeclaration arrayItemsType =
                 typeDeclaration.ArrayItemsType()?.ReducedType
                ?? throw new InvalidOperationException("Unexpected missing ArrayItemsType() in numeric array build.");

        generator
            .AppendLineIndent("while (index < ValueBufferSize)")
            .AppendLineIndent("{")
            .PushIndent();

        if (typeDeclaration.ArrayRank() > 1)
        {
            generator
                .AppendIndent(arrayItemsType.FullyQualifiedDotnetTypeName())
                .Append(" child = ")
                .Append(arrayItemsType.FullyQualifiedDotnetTypeName())
                .Append(".FromValues(values.Slice(index, ")
                .Append(arrayItemsType.FullyQualifiedDotnetTypeName())
                .AppendLine(".ValueBufferSize));")
                .AppendLineIndent("builder.Add(child);")
                .AppendIndent("index += ")
                .Append(arrayItemsType.FullyQualifiedDotnetTypeName())
                .AppendLine(".ValueBufferSize;");
        }
        else
        {
            generator
                .AppendLineIndent("builder.Add((JsonAny)values[index++]);");
        }

        return generator
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendReturnNewArrayTypeBuiltFromValue(this CodeGenerator generator, string valueName)
    {
        return generator
            .AppendIndent("return new([..")
            .Append(valueName)
            .AppendLine("]);");
    }

    private static CodeGenerator AppendFromRangeBuiltInTypeFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string itemsType)
    {
        if (typeDeclaration.ArrayItemsType() is not null || typeDeclaration.IsTuple())
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("FromRange")
            .AppendBlockIndent(
            """
            /// <summary>
            /// Create an array from the given items.
            /// </summary>
            /// <param name="items">The items from which to create the array.</param>
            /// <returns>The new array created from the items.</returns>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" FromRange(IEnumerable<")
            .Append(itemsType)
            .AppendLine("> items)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new([..items]);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendInsertRangeTArray(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.InsertRange<TArray>(int index, in TArray items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" InsertRange<TArray>(int index, in TArray items)")
                .PushIndent()
                    .AppendLineIndent("where TArray : struct, IJsonArray<TArray>")
                .PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWith(this, index, items.EnumerateArray()));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendInsertItemJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.Insert(int index, in JsonAny item1)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" Insert(int index, in JsonAny item1)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWith(this, index, item1));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendInsertItemArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Insert(int index, in ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine(" item1)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWith(this, index, item1));")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendInsertRangeEnumerableJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.InsertRange(int index, IEnumerable<JsonAny> items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" InsertRange(int index, IEnumerable<JsonAny> items)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWith(this, index, items));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendInsertRangeEnumerableArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" InsertRange(int index, IEnumerable<")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine("> items)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWith(this, index, items.Select(item => item.AsAny)));")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendInsertRangeEnumerableTItem(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.InsertRange<TItem>(int index, IEnumerable<TItem> items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" InsertRange<TItem>(int index, IEnumerable<TItem> items)")
                .PushIndent()
                    .AppendLineIndent("where TItem : struct, IJsonValue<TItem>")
                .PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWith(this, index, items.Select(item => item.AsAny)));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddRangeEnumerableJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.AddRange(IEnumerable<JsonAny> items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" AddRange(IEnumerable<JsonAny> items)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);
                builder.AddRange(items);
                return new(builder.ToImmutable());
                """)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddRangeEnumerableArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" AddRange(IEnumerable<")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine("> items)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);")
                    .AppendIndent("foreach (")
                    .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                    .AppendLine(" item in items)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("builder.Add(item.AsAny);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("return new(builder.ToImmutable());")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendAddRangeEnumerableTItem(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.AddRange<TItem>(IEnumerable<TItem> items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" AddRange<TItem>(IEnumerable<TItem> items)")
                .PushIndent()
                    .AppendLineIndent("where TItem : struct, IJsonValue<TItem>")
                .PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);
                foreach (TItem item in items)
                {
                    builder.Add(item.AsAny);
                }

                return new(builder.ToImmutable());
                """)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddParamsJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.Add(params JsonAny[] items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" Add(params JsonAny[] items)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                return new([..items]);
                """)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddParamsArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Add(params ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine("[] items)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);")
                    .AppendIndent("foreach (")
                    .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                    .AppendLine(" item in items)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("builder.Add(item.AsAny);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("return new(builder.ToImmutable());")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendAddRangeTArray(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.AddRange<TArray>(in TArray items)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" AddRange<TArray>(in TArray items)")
                .PushIndent()
                    .AppendLineIndent("where TArray : struct, IJsonArray<TArray>")
                .PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);
                foreach (JsonAny item in items.EnumerateArray())
                {
                    builder.Add(item.AsAny);
                }

                return new(builder.ToImmutable());
                """)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddItemJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.Add(in JsonAny item1)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" Add(in JsonAny item1)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);
                builder.Add(item1);
                return new(builder.ToImmutable());
                """)
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddItemArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Add(in ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine(" item1)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendBlockIndent(
                    """
                    ImmutableList<JsonAny>.Builder builder = __CorvusArrayHelpers.GetImmutableListBuilder(this);
                    builder.Add(item1.AsAny);
                    return new(builder.ToImmutable());
                    """)
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendRemoveJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.Remove(in JsonAny oldValue)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" Remove(in JsonAny oldValue)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWithout(this, oldValue));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendRemoveArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Remove(in ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine(" oldValue)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWithout(this, oldValue));")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendReplaceJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.Replace(in JsonAny oldValue, in JsonAny newValue)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" Replace(in JsonAny oldValue, in JsonAny newValue)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListReplacing(this, oldValue, newValue));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendReplaceArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Replace(in ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .Append(" oldValue, in ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine(" newValue)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListReplacing(this, oldValue, newValue));")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendRemoveAt(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.RemoveAt(int index)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" RemoveAt(int index)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWithoutRange(this, index, 1));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendRemoveRange(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.RemoveRange(int index, int count)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" RemoveRange(int index, int count)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListWithoutRange(this, index, count));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendSetItemJsonAny(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isTupleOrHasArrayItemsType)
    {
        generator.AppendSeparatorLine();

        if (isTupleOrHasArrayItemsType)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent(typeDeclaration.DotnetTypeName())
                .Append(" IJsonArray<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.SetItem(int index, in JsonAny value)");
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" SetItem(int index, in JsonAny value)");
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListSetting(this, index, value));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendSetItemArrayItemsType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" SetItem(int index, in ")
                .Append(arrayItemsType.ReducedType.FullyQualifiedDotnetTypeName())
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(__CorvusArrayHelpers.GetImmutableListSetting(this, index, value));")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }
}
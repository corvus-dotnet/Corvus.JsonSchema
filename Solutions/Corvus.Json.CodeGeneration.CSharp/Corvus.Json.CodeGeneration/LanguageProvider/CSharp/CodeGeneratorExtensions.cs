// <copyright file="CodeGeneratorExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static class CodeGeneratorExtensions
{
    /// <summary>
    /// Append using statements for the given namespaces.
    /// </summary>
    /// <param name="generator">The generator to which to append usings.</param>
    /// <param name="namespaces">The namespace to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUsings(this CodeGenerator generator, params ConditionalCodeSpecification[] namespaces)
    {
        ConditionalCodeSpecification.AppendConditionalsInOrder(
            generator,
            namespaces,
            static (g, a, _) => Append(g, a));

        return generator;

        static void Append(CodeGenerator generator, Action<CodeGenerator> action)
        {
            generator.Append("using ");
            action(generator);
            generator.AppendLine(";");
        }
    }

    /// <summary>
    /// Appends a blank line if the previous line ended with a closing brace.
    /// </summary>
    /// <param name="generator">The generator to which to append the separator line.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSeparatorLine(this CodeGenerator generator)
    {
        if ((generator.ScopeType == ScopeType.Type && generator.EndsWith($";{Environment.NewLine}")) ||
            generator.EndsWith($"}}{Environment.NewLine}") ||
            generator.EndsWith($"#endif{Environment.NewLine}"))
        {
            // Append a blank line
            generator.AppendLine();
        }

        return generator;
    }

    /// <summary>
    /// Append a namespace statement.
    /// </summary>
    /// <param name="generator">The generator to which to append usings.</param>
    /// <param name="ns">The namespace to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginNamespace(this CodeGenerator generator, string ns)
    {
        return generator
            .Append("namespace ")
            .Append(ns)
            .AppendLine(";")
            .PushMemberScope(ns, ScopeType.TypeContainer);
    }

    /// <summary>
    /// Append a namespace statement.
    /// </summary>
    /// <param name="generator">The generator to which to append usings.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndNamespace(this CodeGenerator generator)
    {
        return generator
            .PopMemberScope();
    }

    /// <summary>
    /// Begin a method declaration for an explicit name which will be reserved in the scope.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <param name="visibilityAndModifiers">The visibility and modifiers for the method.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="methodName">The method name, which will be reserved in the scope.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginReservedMethodDeclaration(
        this CodeGenerator generator,
        string visibilityAndModifiers,
        string returnType,
        string methodName,
        params MethodParameter[] parameters)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent(visibilityAndModifiers)
            .Append(' ')
            .Append(returnType)
            .Append(' ')
            .Append(methodName)
            .ReserveName(methodName) // Reserve the method name in the parent scope
            .PushMemberScope(methodName, ScopeType.Method) // Then move to the method scope before appending parameters
            .AppendParameterList(parameters)
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Begin a method declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <param name="visibilityAndModifiers">The visibility and modifiers for the method.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="methodName">The method name, which will be reserved in the scope.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginMethodDeclaration(
        this CodeGenerator generator,
        string visibilityAndModifiers,
        string returnType,
        MemberName methodName,
        params MethodParameter[] parameters)
    {
        string realisedMethodName = generator.GetOrAddMemberName(methodName);

        return generator
            .AppendIndent(visibilityAndModifiers)
            .Append(' ')
            .Append(returnType)
            .Append(' ')
            .Append(realisedMethodName)
            .PushMemberScope(realisedMethodName, ScopeType.Method)
            .AppendParameterList(parameters)
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Append the backing fields for the implied core types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="impliedCoreTypes">The implied core types.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBackingFields(this CodeGenerator generator, CoreTypes impliedCoreTypes)
    {
        return generator
            .AppendBackingField("Backing", "backing")
            .AppendBackingField("JsonElement", "jsonElementBacking")
            .AppendBackingField("string", "stringBacking", impliedCoreTypes, CoreTypes.String)
            .AppendBackingField("bool", "boolBacking", impliedCoreTypes, CoreTypes.Boolean)
            .AppendBackingField("BinaryJsonNumber", "numberBacking", impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
            .AppendBackingField("ImmutableList<JsonAny>", "arrayBacking", impliedCoreTypes, CoreTypes.Array)
            .AppendBackingField("ImmutableList<JsonObjectProperty>", "objectBacking", impliedCoreTypes, CoreTypes.Object);
    }

    /// <summary>
    /// Append the schema location static property for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSchemaLocationStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("SchemaLocation")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets the schema location from which this type was generated.
            /// </summary>
            """)
            .AppendIndent("public static string SchemaLocation { get; } = ")
            .Append(SymbolDisplay.FormatLiteral(typeDeclaration.RelativeSchemaLocation, true))
            .AppendLine(";");
    }

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
                    .AppendArrayIndexer(typeDeclaration, itemsType.ReducedType.DotnetTypeName(), isExplicit: false);
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
                .AppendLine(")]);");
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
                /// <param name = "index">The index at which to add the element.</param>
                /// <param name = "value">The value to add.</param>
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
                /// <param name = "oldItem">The item to remove.</param>
                /// <param name = "newItem">The item to insert.</param>
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
                /// <param name = "item">The item to remove.</param>
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
                /// <param name = "index">The start index of the range to remove.</param>
                /// <param name = "count">The length of the range to remove.</param>
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
                // <param name = "index">The index at which to add the element.</param>
                // <param name = "value">The value to add.</param>
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
                /// <param name = "index">The index at which to add the element.</param>
                /// <param name = "values">The values to add.</param>
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
    /// Append the static property which provides a null instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNullInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("Null")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets a Null instance.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" Null { get; } = new(JsonValueHelpers.NullElement);");
    }

    /// <summary>
    /// Append the static property which provides a null instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUndefinedInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("Undefined")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets an Undefined instance.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" Undefined { get; }");
    }

    /// <summary>
    /// Append the static property which provides a default instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendDefaultInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .ReserveName("DefaultInstance")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets the default instance.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" DefaultInstance { get; }");

        return typeDeclaration.DefaultValue().ValueKind switch
        {
            JsonValueKind.Undefined => generator.AppendLine(),
            JsonValueKind.Null => generator
                                    .Append(" = ")
                                    .Append(typeDeclaration.DotnetTypeName())
                                    .AppendLine(".ParseValue(\"null\"u8);"),
            _ => generator
                    .Append(" = ")
                    .Append(typeDeclaration.DotnetTypeName())
                    .Append(".ParseValue(")
                    .Append(SymbolDisplay.FormatLiteral(typeDeclaration.DefaultValue().GetRawText(), true))
                    .AppendLine("u8);"),
        };
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonAny instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsAnyProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsAny")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public JsonAny AsAny")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalConstructFromBacking(
                        "Backing.JsonElement",
                        "jsonElementBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendConditionalConstructFromBacking(
                        "Backing.String",
                        "stringBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Bool",
                        "boolBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Number",
                        "numberBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Array",
                        "arrayBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Object",
                        "objectBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendReturnNullInstanceIfNull()
                    .AppendSeparatorLine()
                    .AppendLineIndent("return JsonAny.Undefined;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonElement instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsJsonElementProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsJsonElement")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public JsonElement AsJsonElement")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return ",
                        "jsonElementBacking",
                        ";",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.String",
                        "return JsonValueHelpers.StringToJsonElement(",
                        "stringBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Bool",
                        "return JsonValueHelpers.BoolToJsonElement(",
                        "boolBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Number",
                        "return JsonValueHelpers.NumberToJsonElement(",
                        "numberBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Array",
                        "return JsonValueHelpers.ArrayToJsonElement(",
                        "arrayBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Object",
                        "return JsonValueHelpers.ObjectToJsonElement(",
                        "objectBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendSeparatorLine()
                    .AppendReturnNullJsonElementIfNull()
                    .AppendSeparatorLine()
                    .AppendLineIndent("return default;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonString instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsStringProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsString")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.String) != 0
                    ? "public JsonString AsString"
                    : "JsonString IJsonValue.AsString")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.String",
                        "return new(",
                        "stringBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonBoolean instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsBooleanProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsBoolean")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Boolean) != 0
                    ? "public JsonBoolean AsBoolean"
                    : "JsonBoolean IJsonValue.AsBoolean")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Bool",
                        "return new(",
                        "boolBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonNumber instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsNumberProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsNumber")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & (CoreTypes.Number | CoreTypes.Integer)) != 0
                    ? "public JsonNumber AsNumber"
                    : "JsonNumber IJsonValue.AsNumber")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Number",
                        "return new(",
                        "numberBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonObject instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsObjectProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsObject")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Object) != 0
                    ? "public JsonObject AsObject"
                    : "JsonObject IJsonValue.AsObject")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Object",
                        "return new(",
                        "objectBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonArray instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsArrayProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsArray")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Array) != 0
                    ? "public JsonArray AsArray"
                    : "JsonArray IJsonValue.AsArray")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Array",
                        "return new(",
                        "arrayBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a property which gets a value indicating if the instance has a JsonElement backing.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasJsonElementBackingProperty(this CodeGenerator generator)
    {
        return generator
            .ReserveName("HasJsonElementBacking")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public bool HasJsonElementBacking")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return ")
                    .AppendTestBacking("Backing.JsonElement")
                    .AppendLine(";")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a property which gets a value indicating if the instance has a .NET core type backing.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasDotnetBackingProperty(this CodeGenerator generator)
    {
        return generator
            .ReserveName("HasDotnetBacking")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public bool HasDotnetBacking")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return ")
                    .AppendTestBacking("Backing.Dotnet")
                    .AppendLine(";")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a property which gets the <see cref="JsonValueKind"/> for the instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValueKindProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("ValueKind")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public JsonValueKind ValueKind")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return ",
                        "jsonElementBacking",
                        ".ValueKind;")
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.String",
                        "return JsonValueKind.String;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Bool",
                        "return ",
                        "boolBacking",
                        " ? JsonValueKind.True : JsonValueKind.False;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Number",
                        "return JsonValueKind.Number;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Array",
                        "return JsonValueKind.Array;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Object",
                        "return JsonValueKind.Object;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Null",
                        "return JsonValueKind.Null;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Null)
                    .AppendSeparatorLine()
                    .AppendLineIndent("return JsonValueKind.Undefined;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the default constructor for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicDefaultConstructor(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SingleConstantValue().ValueKind != JsonValueKind.Undefined)
        {
            // Don't emit this for a type that has a single constant value.
            return generator;
        }

        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypes();

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine("()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBackingFieldAssignment("jsonElementBacking", "default")
                .AppendBackingFieldAssignment("backing", "Backing.JsonElement")
                .AppendBackingFieldAssignment("stringBacking", "string.Empty", impliedCoreTypes, CoreTypes.String)
                .AppendBackingFieldAssignment("boolBacking", "default", impliedCoreTypes, CoreTypes.Boolean)
                .AppendBackingFieldAssignment("numberBacking", "default", impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
                .AppendBackingFieldAssignment("arrayBacking", "ImmutableList<JsonAny>.Empty", impliedCoreTypes, CoreTypes.Array)
                .AppendBackingFieldAssignment("objectBacking", "ImmutableList<JsonObjectProperty>.Empty", impliedCoreTypes, CoreTypes.Object)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the default constructor for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicJsonElementConstructor(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypes();

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to construct the instance.</param>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine("(in JsonElement value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBackingFieldAssignment("jsonElementBacking", "value")
                .AppendBackingFieldAssignment("backing", "Backing.JsonElement")
                .AppendBackingFieldAssignment("stringBacking", "string.Empty", impliedCoreTypes, CoreTypes.String)
                .AppendBackingFieldAssignment("boolBacking", "default", impliedCoreTypes, CoreTypes.Boolean)
                .AppendBackingFieldAssignment("numberBacking", "default", impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
                .AppendBackingFieldAssignment("arrayBacking", "ImmutableList<JsonAny>.Empty", impliedCoreTypes, CoreTypes.Array)
                .AppendBackingFieldAssignment("objectBacking", "ImmutableList<JsonObjectProperty>.Empty", impliedCoreTypes, CoreTypes.Object)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromTypeUsingConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion from ")
            .AppendTypeAsSeeCref(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(sourceType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(value);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <param name="sourceValueKind">The expected <see cref="JsonValueKind"/> for the conversion.</param>
    /// <param name="dotnetTypeConversion">The code that converts the "value" to a dotnet value suitable
    /// for a constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromJsonValueTypeUsingConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType,
        JsonValueKind sourceValueKind,
        string dotnetTypeConversion)
    {
        return AppendImplicitConversionFromJsonValueTypeUsingConstructor(
            generator,
            typeDeclaration,
            sourceType,
            [sourceValueKind],
            dotnetTypeConversion);
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
            /// <typeparam name = "T">The type of the <paramref name = "items"/> from which to create the array.</typeparam>
            /// <param name = "items">The items from which to create the array.</param>
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
    /// Appends an implicit conversion to bool for a boolean-backed type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToBoolean(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Conversion to <see langword="bool"/>.
                /// </summary>
                /// <param name="value">The value from which to convert.</param>
                /// <exception cref="InvalidOperationException">The value was not a boolean.</exception>
                """)
            .AppendIndent("public static implicit operator bool(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendBlockIndent(
                """
                {
                    return value.GetBoolean() ?? throw new InvalidOperationException();
                }
                """);
    }

    /// <summary>
    /// Appends an explicit conversion to string for a string-backed type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExplicitConversionToString(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");
        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Conversion to string.
                /// </summary>
                /// <param name="value">The value from which to convert.</param>
                /// <exception cref="InvalidOperationException">The value was not a string.</exception>
                """)
            .AppendIndent("public static explicit operator string(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendBlockIndent(
                $$"""
                {
                    if ((value.{{backing}} & Backing.JsonElement) != 0)
                    {
                        if (value.{{jsonElementBacking}}.GetString() is string result)
                        {
                            return result;
                        }

                        throw new InvalidOperationException();
                    }

                    if ((value.{{backing}} & Backing.String) != 0)
                    {
                        return value.{{stringBacking}};
                    }

                    throw new InvalidOperationException();
                }
                """);
    }

    /// <summary>
    /// Append an implicit conversion to the well-known string format type
    /// if appropriate.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToStringFormat(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (WellKnownStringFormatHelpers.GetDotnetTypeNameFor(typeDeclaration.Format()) is string formatType)
        {
            return generator
                .AppendImplicitConversionFromJsonValueTypeUsingAs(typeDeclaration, formatType)
                .AppendImplicitConversionToJsonValueTypeUsingAs(typeDeclaration, formatType);
        }

        return generator;
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <param name="sourceValueKinds">The expected <see cref="JsonValueKind"/> or kinds for the conversion.</param>
    /// <param name="dotnetTypeConversion">The code that converts the "value" to a dotnet value suitable
    /// for a constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromJsonValueTypeUsingConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType,
        JsonValueKind[] sourceValueKinds,
        string dotnetTypeConversion)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion from ")
            .Append(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(sourceType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("if (value.HasDotnetBacking && ")
                .AppendShortcircuitingOr(sourceValueKinds, static (g, v) => g.AppendJsonValueKindEquals("value", v), includeParensIfMultiple: true)
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(")
                    .PushIndent()
                        .AppendBlockIndent(dotnetTypeConversion, omitLastLineEnd: true)
                    .PopIndent()
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return new(value.AsJsonElement);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends conversions to and from the <paramref name="numericType"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration from which to convert.</param>
    /// <param name="numericType">The name of the numeric type for conversion.</param>
    /// <param name="numericValueAccessorMethodName">The name of the method that converts from a <see cref="JsonElement"/>
    /// value to the <paramref name="numericType"/>.</param>
    /// <param name="frameworkType">The framework type for which to emit the code.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendConversionsForNumber(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string numericType,
        string numericValueAccessorMethodName,
        FrameworkType frameworkType = FrameworkType.All)
    {
        generator.AppendSeparatorLine();

        return ConditionalCodeSpecification.AppendConditional(generator, AppendConversions, frameworkType);

        void AppendConversions(CodeGenerator generator)
        {
            string operatorKind = typeDeclaration.PreferredDotnetNumericTypeName() == numericType ? "implicit" : "explicit";
            generator
                .AppendSeparatorLine()
                .AppendIndent("public static ")
                .Append(operatorKind)
                .Append(" operator ")
                .Append(numericType)
                .Append('(')
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent(
                        "Backing.JsonElement",
                        "jsonElementBacking",
                        (g, f) =>
                        {
                            g.AppendIndent("return value.")
                             .Append(f)
                             .Append('.')
                             .Append(numericValueAccessorMethodName)
                             .AppendLine("();");
                        })
                    .AppendConditionalBackingValueCallbackIndent(
                        "Backing.Number",
                        "numberBacking",
                        (g, f) =>
                        {
                            g.AppendIndent("return value.")
                             .Append(f)
                             .Append(".CreateChecked<")
                             .Append(numericType)
                             .AppendLine(">();");
                        })
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendIndent("public static ")
                .Append(operatorKind)
                .Append(" operator ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append('(')
                .Append(numericType)
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(new BinaryJsonNumber(value));")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Appends an implicit conversion to <paramref name="targetType"/> from the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration from which to convert.</param>
    /// <param name="targetType">The name of the target type to which to convert.</param>
    /// <param name="forCoreTypes">The core types for which the conversion applies.</param>
    /// <param name="dotnetTypeConversion">The code that converts the value to the target type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToJsonValueType(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string targetType,
        CoreTypes forCoreTypes,
        string dotnetTypeConversion)
    {
        if ((typeDeclaration.ImpliedCoreTypes() & forCoreTypes) == 0)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion to ")
            .Append(targetType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(targetType)
            .Append('(')
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return ")
                .PushIndent()
                    .AppendBlockIndent(dotnetTypeConversion, omitLastLineEnd: true)
                .PopIndent()
                .AppendLine(";")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append <c>&lt;see cref="[typeName]"/&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeName">The type name to which to append the reference.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTypeAsSeeCref(
        this CodeGenerator generator,
        string typeName)
    {
        generator
            .Append("<see cref=\"");

        foreach (char c in typeName)
        {
            if (c == '<')
            {
                generator.Append('{');
            }
            else if (c == '>')
            {
                generator.Append('}');
            }
            else
            {
                generator.Append(c);
            }
        }

        return generator
            .Append("\"/>");
    }

    /// <summary>
    /// Appends an implicit conversion from the
    /// dotnet type of the <paramref name="typeDeclaration"/> to the <paramref name="targetType"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="targetType">The name of the target type towhich to convert.</param>
    /// <param name="dotnetTypeConversion">The code that converts the "value" to a dotnet value suitable
    /// for a constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToType(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string targetType,
        string dotnetTypeConversion)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion to ")
            .AppendTypeAsSeeCref(targetType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(targetType)
            .Append('(')
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendBlockIndent(dotnetTypeConversion, omitLastLineEnd: true)
                .PopIndent()
                .AppendLine(";")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromJsonValueTypeUsingAs(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion from ")
            .Append(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(sourceType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return value.As<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToJsonValueTypeUsingAs(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion to ")
            .Append(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(sourceType)
            .Append('(')
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return value.As<")
                .Append(sourceType)
                .AppendLine(">();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from dotnet type of the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToJsonAny(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Conversion to JsonAny.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator JsonAny(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return value.AsAny;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the FromJson static factory method.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromJsonFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("FromJson")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets an instance of the JSON value from a <see cref="JsonElement"/> value.
                /// </summary>
                /// <param name="value">The <see cref="JsonElement"/> value from which to instantiate the instance.</param>
                /// <returns>An instance of this type, initialized from the <see cref="JsonElement"/>.</returns>
                /// <remarks>The returned value will have a <see cref = "IJsonValue.ValueKind"/> of <see cref = "JsonValueKind.Undefined"/> if the
                /// value cannot be constructed from the given instance (e.g. because they have an incompatible .NET backing type).
                /// </remarks>                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" FromJson(in JsonElement value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(value);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the FromAny static factory method.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromAnyFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("FromAny")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
                /// </summary>
                /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
                /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" FromAny(in JsonAny value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConversionFromValue("value", typeDeclaration.ImpliedCoreTypes())
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends static factory method of the form FromXXX{TValue}.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="forCoreTypes">The core types for which to append conversions.</param>
    /// <param name="jsonValueTypeBaseName">The base name for the JSON value type (e.g. Boolean, String).</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromTValueFactoryMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        CoreTypes forCoreTypes,
        string jsonValueTypeBaseName)
    {
        if ((typeDeclaration.ImpliedCoreTypes() & forCoreTypes) != 0)
        {
            return generator
                .ReserveName($"From{jsonValueTypeBaseName}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
                    /// </summary>
                    /// <typeparam name="TValue">The type of the value.</typeparam>
                    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
                    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
                    """)
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" From")
                .Append(jsonValueTypeBaseName)
                .AppendLine("<TValue>(in TValue value)")
                .PushIndent()
                .AppendIndent("where TValue : struct, IJson")
                .Append(jsonValueTypeBaseName)
                .AppendLine("<TValue>")
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConversionFromValue("value", forCoreTypes)
                .PopIndent()
                .AppendLineIndent("}");
        }
        else
        {
            return generator
                .ReserveName($"From{jsonValueTypeBaseName}")
                .AppendSeparatorLine()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
                    /// </summary>
                    /// <typeparam name="TValue">The type of the value.</typeparam>
                    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
                    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
                    """)
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendIndent("static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" IJsonValue<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.")
                .Append("From")
                .Append(jsonValueTypeBaseName)
                .AppendLine("<TValue>(in TValue value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("if (")
                    .AppendLine("value.HasJsonElementBacking)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendIndent("return new(")
                        .AppendLine("value.AsJsonElement);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("return Undefined;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLine("#endif");
        }
    }

    /// <summary>
    /// Appends a static Parse() method to parse an instance of the <paramref name="sourceType"/> to an instance of the
    /// <paramref name="typeDeclaration"/> type.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <param name="sourceType">The type of the source from which to parse.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParseMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .ReserveNameIfNotReserved("Parse")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Parses the ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"source\">The source of the JSON string to parse.</param>")
            .AppendLineIndent("/// <param name=\"options\">The (optional) JsonDocumentOptions.</param>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" Parse(")
            .Append(sourceType)
            .AppendLine(" source, JsonDocumentOptions options = default)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("using var jsonDocument = JsonDocument.Parse(source, options);")
                .AppendLineIndent("return new(jsonDocument.RootElement.Clone());")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the As{T} conversion method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsTMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveNameIfNotReserved("As")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an instance of the target value.
                /// </summary>
                /// <typeparam name="TTarget">The type of the target.</typeparam>
                /// <returns>An instance of the target type.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public TTarget As<TTarget>()
                    where TTarget : struct, IJsonValue<TTarget>
                {
                """)
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
            .AppendConditionalBackingValueLineIndent("Backing.JsonElement", "return TTarget.FromJson(this.jsonElementBacking);")
            .AppendConditionalBackingValueLineIndent("Backing.String", "return TTarget.FromString(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.String)
            .AppendConditionalBackingValueLineIndent("Backing.Bool", "return TTarget.FromBoolean(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Boolean)
            .AppendConditionalBackingValueLineIndent("Backing.Number", "return TTarget.FromNumber(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Number | CoreTypes.Integer)
            .AppendConditionalBackingValueLineIndent("Backing.Array", "return TTarget.FromArray(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Array)
            .AppendConditionalBackingValueLineIndent("Backing.Object", "return TTarget.FromObject(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Object)
            .AppendConditionalBackingValueLineIndent("Backing.Null", "return TTarget.Null;")
            .AppendSeparatorLine()
            .AppendLineIndent("return TTarget.Undefined;")
            .AppendLine("#else")
            .AppendIndent("return this.As<")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(", TTarget>();")
            .AppendLine("#endif")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the WriteTo() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendWriteToMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("WriteTo")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public void WriteTo(Utf8JsonWriter writer)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalBackingValueCallbackIndent(
                    "Backing.JsonElement",
                    "jsonElementBacking",
                    static (g, name) => g.AppendWriteJsonElementBacking(name),
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Array",
                    "JsonValueHelpers.WriteItems(",
                    "arrayBacking",
                    ", writer);",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Array,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Bool",
                    "writer.WriteBooleanValue(",
                    "boolBacking",
                    ");",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Boolean,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Number",
                    string.Empty,
                    "numberBacking",
                    ".WriteTo(writer);",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Number | CoreTypes.Integer,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Object",
                    "JsonValueHelpers.WriteProperties(",
                    "objectBacking",
                    ", writer);",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Object,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "writer.WriteStringValue(",
                    "stringBacking",
                    ");",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.String,
                    returnFromClause: true)
                .AppendConditionalBackingValueLineIndent(
                    "Backing.Null",
                    "writer.WriteNullValue();",
                    returnFromClause: true)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the Equals() method overloads.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsOverloads(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveNameIfNotReserved("Equals")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <inheritdoc/>
                public override bool Equals(object? obj)
                {
                    return
                        (obj is IJsonValue jv && this.Equals(jv.AsAny)) ||
                        (obj is null && this.IsNull());
                }

                /// <inheritdoc/>
                public bool Equals<T>(in T other)
                    where T : struct, IJsonValue<T>
                {
                    return JsonValueHelpers.CompareValues(this, other);
                }

                /// <summary>
                /// Equality comparison.
                /// </summary>
                /// <param name = "other">The other item with which to compare.</param>
                /// <returns><see langword="true"/> if the values were equal.</returns>
                """)
            .AppendIndent("public bool Equals(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" other)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return JsonValueHelpers.CompareValues(this, other);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a static ParseValue() method to parse an instance of the <paramref name="sourceType"/> to an instance of the
    /// <paramref name="typeDeclaration"/> type.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <param name="sourceType">The type of the source from which to parse.</param>
    /// <param name="byRef">Whether the parameter is a by-ref value.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParseValueMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType,
        bool byRef = false)
    {
        generator
            .ReserveNameIfNotReserved("ParseValue")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Parses the ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"source\">The source of the JSON string to parse.</param>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" ParseValue(");

        if (byRef)
        {
            generator.Append("ref ");
        }

        generator
            .Append(sourceType)
            .AppendLine(" source)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendIndent("return IJsonValue<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.ParseValue(");

        if (byRef)
        {
            generator.Append("ref ");
        }

        generator
                .AppendLine("source);")
                .AppendLine("#else")
                .AppendIndent("return JsonValueHelpers.ParseValue<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">(");

        if (byRef)
        {
            generator.Append("ref ");
        }

        return generator
            .AppendLine("source);")
            .AppendLine("#endif")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a binary operator for the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to add the operator.</param>
    /// <param name="returnType">The return type of the operator.</param>
    /// <param name="operatorSymbol">The symbol to inject for the operator.</param>
    /// <param name="operatorBody">The body to inject for the operator.</param>
    /// <param name="returnValueDocumentation">The return value documentation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBinaryOperator(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string returnType,
        string operatorSymbol,
        string operatorBody,
        string returnValueDocumentation)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Operator ")
            .Append(operatorSymbol)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The lhs of the operator.</param>")
            .AppendLineIndent("/// <param name=\"right\">The rhs of the operator.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendBlockIndentWithPrefix(returnValueDocumentation, "/// ")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static ")
            .Append(returnType)
            .Append(" operator ")
            .Append(operatorSymbol)
            .Append("(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" left, in ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(operatorBody)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a family of string concatenation functions.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendStringConcatenation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        for (int i = 2; i <= 8; ++i)
        {
            generator.AppendSeparatorLine();
            AppendStringConcatenation(generator, typeDeclaration, i);
        }

        return generator;

        static void AppendStringConcatenation(CodeGenerator generator, TypeDeclaration typeDeclaration, int count)
        {
            generator
                .ReserveName("Concatenate")
                .AppendLineIndent("/// <summary>")
                .AppendIndent("/// Concatenate ")
                .Append(count)
                .Append(" JSON values, producing an instance of the string type ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(".")
                .AppendLineIndent("/// </summary>");

            for (int i = 1; i <= count; ++i)
            {
                generator
                    .AppendIndent("/// <typeparam name=\"T")
                    .Append(i)
                    .Append("\">The type of the ")
                    .AppendOrdinalName(i)
                    .AppendLine(" value.</typeparam>");
            }

            generator
                .AppendLineIndent("/// <param name=\"buffer\">The buffer into which to concatenate the values.</param>");

            for (int i = 1; i <= count; ++i)
            {
                generator
                    .AppendIndent("/// <param name=\"value")
                    .Append(i)
                    .Append("\">The ")
                    .AppendOrdinalName(i)
                    .AppendLine(" value.</param>");
            }

            generator
                .AppendLineIndent("/// <returns>An instance of this string type.</returns>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Concatenate<");

            for (int i = 1; i <= count; ++i)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator
                    .Append('T')
                    .Append(i);
            }

            generator
                .Append(">(Span<byte> buffer, ");

            for (int i = 1; i <= count; ++i)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator
                    .Append("in T")
                    .Append(i)
                    .Append(" value")
                    .Append(i);
            }

            generator
                .AppendLine(")")
                .PushIndent();

            for (int i = 1; i <= count; ++i)
            {
                generator
                    .AppendIndent("where T")
                    .Append(i)
                    .Append(" : struct, IJsonValue<T")
                    .Append(i)
                    .AppendLine(">");
            }

            generator
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, ");

            for (int i = 1; i <= count; ++i)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator
                    .Append("value")
                    .Append(i);
            }

            generator
                .AppendLine(");")
                .AppendLineIndent("return ParseValue(buffer[..written]);")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Appends the <c>TryGetBoolean()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetBoolean(this CodeGenerator generator)
    {
        return generator
            .ReserveName("TryGetBoolean")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Try to retrieve the value as a boolean.
            /// </summary>
            /// <param name="result"><see langword="true"/> if the value was true, otherwise <see langword="false"/>.</param>
            /// <returns><see langword="true"/> if the value was representable as a boolean, otherwise <see langword="false"/>.</returns>
            public bool TryGetBoolean([NotNullWhen(true)] out bool result)
            {
                switch (this.ValueKind)
                {
                    case JsonValueKind.True:
                        result = true;
                        return true;
                    case JsonValueKind.False:
                        result = false;
                        return true;
                    default:
                        result = default;
                        return false;
                }
            }
            """);
    }

    /// <summary>
    /// Appends the <c>TryGetString()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetString(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveName("TryGetString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <inheritdoc/>
                public bool TryGetString([NotNullWhen(true)] out string? value)
                {
                    if ((this.{{backing}} & Backing.String) != 0)
                    {
                        value = this.{{stringBacking}};
                        return true;
                    }

                    if ((this.{{backing}} & Backing.JsonElement) != 0 &&
                        this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                    {
                        value = this.{{jsonElementBacking}}.GetString();
                        return value is not null;
                    }

                    value = null;
                    return false;
                }
                """);
    }

    /// <summary>
    /// Appends the <c>GetBoolean()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetBoolean(this CodeGenerator generator)
    {
        return generator
            .ReserveName("GetBoolean")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Get the value as a boolean.
            /// </summary>
            /// <returns>The value of the boolean, or <see langword="null"/> if the value was not representable as a boolean.</returns>
            public bool? GetBoolean()
            {
                if (this.TryGetBoolean(out bool result))
                {
                    return result;
                }

                return null;
            }
            """);
    }

    /// <summary>
    /// Appends the <c>GetString()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetString(this CodeGenerator generator)
    {
        return generator
            .ReserveName("GetString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the string value.
                /// </summary>
                /// <returns><c>The string if this value represents a string</c>, otherwise <c>null</c>.</returns>
                public string? GetString()
                {
                    if (this.TryGetString(out string? value))
                    {
                        return value;
                    }

                    return null;
                }
                """);
    }

    /// <summary>
    /// Appends the <c>EqualsUtf8Bytes()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsUtf8Bytes(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveName("EqualsUtf8Bytes")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <summary>
                /// Compare to a sequence of characters.
                /// </summary>
                /// <param name="utf8Bytes">The UTF8-encoded character sequence to compare.</param>
                /// <returns><c>True</c> if the sequences match.</returns>
                public bool EqualsUtf8Bytes(ReadOnlySpan<byte> utf8Bytes)
                {
                    if ((this.{{backing}} & Backing.JsonElement) != 0)
                    {
                        if (this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                        {
                            return this.{{jsonElementBacking}}.ValueEquals(utf8Bytes);
                        }
                    }

                    if ((this.{{backing}} & Backing.String) != 0)
                    {
                        int maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Bytes.Length);
                """)
            .PushIndent()
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendBlockIndent(
                        """
                        char[]? pooledChars = null;

                        Span<char> chars = maxCharCount <= JsonValueHelpers.MaxStackAlloc  ?
                            stackalloc char[maxCharCount] :
                            (pooledChars = ArrayPool<char>.Shared.Rent(maxCharCount));

                        try
                        {
                            int written = Encoding.UTF8.GetChars(utf8Bytes, chars);
                            return chars[..written].SequenceEqual(this.stringBacking);
                        }
                        finally
                        {
                            if (pooledChars is char[] pc)
                            {
                                ArrayPool<char>.Shared.Return(pc);
                            }
                        }
                        """)
                    .AppendLine("#else")
                    .AppendBlockIndent(
                        """
                        char[] chars = ArrayPool<char>.Shared.Rent(maxCharCount);
                        byte[] bytes = ArrayPool<byte>.Shared.Rent(utf8Bytes.Length);
                        utf8Bytes.CopyTo(bytes);

                        try
                        {
                            int written = Encoding.UTF8.GetChars(bytes, 0, bytes.Length, chars, 0);
                            return chars.SequenceEqual(this.stringBacking);
                        }
                        finally
                        {
                            ArrayPool<char>.Shared.Return(chars);
                            ArrayPool<byte>.Shared.Return(bytes);
                        }
                        """)
            .AppendLine("#endif")
            .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the <c>EqualsString()</c> method overloads.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsString(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveName("EqualsString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            $$"""
            /// <summary>
            /// Compare to a sequence of characters.
            /// </summary>
            /// <param name="chars">The character sequence to compare.</param>
            /// <returns><c>True</c> if the sequences match.</returns>
            public bool EqualsString(string chars)
            {
                if ((this.{{backing}} & Backing.JsonElement) != 0)
                {
                    if (this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                    {
                        return this.{{jsonElementBacking}}.ValueEquals(chars);
                    }

                    return false;
                }

                if ((this.{{backing}} & Backing.String) != 0)
                {
                    return chars.Equals(this.{{stringBacking}}, StringComparison.Ordinal);
                }

                return false;
            }

            /// <summary>
            /// Compare to a sequence of characters.
            /// </summary>
            /// <param name="chars">The character sequence to compare.</param>
            /// <returns><c>True</c> if the sequences match.</returns>
            public bool EqualsString(ReadOnlySpan<char> chars)
            {
                if ((this.{{backing}} & Backing.JsonElement) != 0)
                {
                    if (this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                    {
                        return this.{{jsonElementBacking}}.ValueEquals(chars);
                    }

                    return false;
                }

                if ((this.{{backing}} & Backing.String) != 0)
                {
            """)
            .PushIndent()
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendLineIndent($"return chars.SequenceEqual(this.{stringBacking});")
            .AppendLine("#else")
                    .AppendLineIndent($"return chars.SequenceEqual(this.{stringBacking}.AsSpan());")
            .AppendLine("#endif")
            .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends <c>TryFormat()</c> and <c>ToString()</c> methods for .NET 8.0 or greater.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNet80Formatting(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");

        return generator
            .ReserveName("__Corvus__Output")
            .ReserveNameIfNotReserved("TryFormat")
            .ReserveNameIfNotReserved("ToString")
            .AppendSeparatorLine()
            .AppendBlockIndentWithHashOutdent(
            $$"""
            #if NET8_0_OR_GREATER
            /// <inheritdoc/>
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            {
                if ((this.{{backing}} & Backing.String) != 0)
                {
                    int length = Math.Min(destination.Length, this.{{stringBacking}}.Length);
                    this.{{stringBacking}}.AsSpan(0, length).CopyTo(destination);
                    charsWritten = length;
                    return true;
                }

                if ((this.{{backing}} & Backing.JsonElement) != 0)
                {
                    char[] buffer = ArrayPool<char>.Shared.Rent(destination.Length);
                    try
                    {
                        bool result = this.jsonElementBacking.TryGetValue(FormatSpan, new __Corvus__Output(buffer, destination.Length), out charsWritten);
                        if (result)
                        {
                            buffer.AsSpan(0, charsWritten).CopyTo(destination);
                        }

                        return result;
                    }
                    finally
                    {
                        ArrayPool<char>.Shared.Return(buffer);
                    }
                }

                charsWritten = 0;
                return false;

                static bool FormatSpan(ReadOnlySpan<char> source, in __Corvus__Output output, out int charsWritten)
                {
                    int length = Math.Min(output.Length, source.Length);
                    source[..length].CopyTo(output.Destination);
                    charsWritten = length;
                    return true;
                }
            }

            /// <inheritdoc/>
            public string ToString(string? format, IFormatProvider? formatProvider)
            {
                // There is no formatting for the string
                return this.ToString();
            }

            private readonly record struct __Corvus__Output(char[] Destination, int Length);
            #endif
            """);
    }

    /// <summary>
    /// Appends the GetHashCode() and ToString() methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetHashCodeAndToStringMethods(this CodeGenerator generator)
    {
        return generator
            .ReserveNameIfNotReserved("GetHashCode")
            .ReserveNameIfNotReserved("ToString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <inheritdoc/>
                public override int GetHashCode()
                {
                    return JsonValueHelpers.GetHashCode(this);
                }

                /// <inheritdoc/>
                public override string ToString()
                {
                    return this.Serialize();
                }
                """);
    }

    /// <summary>
    /// Append an ordinal name for a number.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="number">The number for which to generate the ordinal.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendOrdinalName(this CodeGenerator generator, int number)
    {
        generator
                .Append(number);

        if (number >= 11 && number <= 13)
        {
            return generator
                .Append("th");
        }

        return (number % 10) switch
        {
            1 => generator.Append("st"),
            2 => generator.Append("nd"),
            3 => generator.Append("rd"),
            _ => generator.Append("th"),
        };
    }

    /// <summary>
    /// Append a short-circuiting set of OR (||) operations.
    /// </summary>
    /// <typeparam name="T">The type of the entity to be passed to the <paramref name="appendCallback"/>.</typeparam>
    /// <param name="generator">The generator.</param>
    /// <param name="values">The values to append.</param>
    /// <param name="appendCallback">The callback which appends the value.</param>
    /// <param name="includeParensIfMultiple">Indicates whether to wrap the clause in round brackets if there
    /// are multiple values.
    /// </param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendShortcircuitingOr<T>(this CodeGenerator generator, T[] values, Action<CodeGenerator, T> appendCallback, bool includeParensIfMultiple)
    {
        bool includeParens = values.Length > 1 && includeParensIfMultiple;

        if (includeParens)
        {
            generator.Append('(');
        }

        for (int i = 0; i < values.Length; ++i)
        {
            if (i > 0)
            {
                generator.Append(" || ");
            }

            appendCallback(generator, values[i]);
        }

        if (includeParens)
        {
            generator.Append(')');
        }

        return generator;
    }

    /// <summary>
    /// Append an equality comparison for a JsonValueKind.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="lhs">The left hand side of the comparison.</param>
    /// <param name="jsonValueKind">The value kind to compare.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonValueKindEquals(
        this CodeGenerator generator,
        string lhs,
        JsonValueKind jsonValueKind)
    {
        return generator
            .Append(lhs)
            .Append(".ValueKind == ")
            .AppendJsonValueKind(jsonValueKind);
    }

    /// <summary>
    /// Append a JSON value kind.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="valueKind">The value kind to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonValueKind(this CodeGenerator generator, JsonValueKind valueKind)
    {
        return generator
            .Append("JsonValueKind.")
            .Append(valueKind.ToString());
    }

    /// <summary>
    /// Append the public numeric constructor appropriate for the type declaration.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the numeric constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <exception cref="InvalidOperationException">This method was called for a non-numeric type declaration.</exception>
    public static CodeGenerator AppendPublicNumericConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        string preferredNumericTypeName =
            typeDeclaration.PreferredDotnetNumericTypeName()
            ?? throw new InvalidOperationException("There must be a preferred numeric type name for a numeric type");

        bool isNet80OrGreaterType = IsNet8OrGreaterNumericType(preferredNumericTypeName);
        if (isNet80OrGreaterType)
        {
            generator
                .AppendLine("#if NET8_0_OR_GREATER");
        }

        generator
            .AppendPublicNumericConstructor(typeDeclaration, preferredNumericTypeName);

        if (isNet80OrGreaterType)
        {
            generator
                .AppendLine("#else")
                .AppendPublicNumericConstructor(
                    typeDeclaration,
                    (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Integer) != 0 ? "long" : "double")
                .AppendLine("#endif");
        }

        return generator;
    }

    /// <summary>
    /// Determines if a .NET type is a NET8_0_OR_GREATER type.
    /// </summary>
    /// <param name="preferredNumericTypeName">The type name.</param>
    /// <returns><see langword="true"/> if the type is for .NET 8.0 or greater.</returns>
    public static bool IsNet8OrGreaterNumericType(string preferredNumericTypeName)
    {
        return preferredNumericTypeName switch
        {
            "Half" => true,
            "UInt128" => true,
            "Int128" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Append the specific public numeric constructor.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the numeric constructor.</param>
    /// <param name="numericTypeName">The name of the .NET numeric type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicNumericConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string numericTypeName)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(numericTypeName)
            .AppendLine(" value)")
            .PushIndent()
                .AppendLineIndent(": this(new BinaryJsonNumber(value))")
            .PopIndent()
            .AppendLineIndent("{")
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the default constructor for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the constructor.</param>
    /// <param name="valueType">The type of the value.</param>
    /// <param name="valueCoreType">The core type of the value type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicValueConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string valueType,
        CoreTypes valueCoreType)
    {
        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypes();

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to construct the instance.</param>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append("(")
            .Append(valueType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBackingFieldAssignment("backing", GetBacking(valueCoreType))
                .AppendBackingFieldAssignment("jsonElementBacking", "default")
                .AppendBackingFieldAssignment("stringBacking", GetValue(valueCoreType, CoreTypes.String, "string.Empty"), impliedCoreTypes, CoreTypes.String)
                .AppendBackingFieldAssignment("boolBacking", GetValue(valueCoreType, CoreTypes.Boolean, "default"), impliedCoreTypes, CoreTypes.Boolean)
                .AppendBackingFieldAssignment("numberBacking", GetValue(valueCoreType, CoreTypes.Number | CoreTypes.Integer, "default"), impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
                .AppendBackingFieldAssignment("arrayBacking", GetValue(valueCoreType, CoreTypes.Array, "ImmutableList<JsonAny>.Empty"), impliedCoreTypes, CoreTypes.Array)
                .AppendBackingFieldAssignment("objectBacking", GetValue(valueCoreType, CoreTypes.Object, "ImmutableList<JsonObjectProperty>.Empty"), impliedCoreTypes, CoreTypes.Object)
            .PopIndent()
            .AppendLineIndent("}");

        static string GetBacking(CoreTypes valueCoreTypes)
        {
            return valueCoreTypes switch
            {
                CoreTypes.String => "Backing.String",
                CoreTypes.Boolean => "Backing.Bool",
                CoreTypes.Number => "Backing.Number",
                CoreTypes.Integer => "Backing.Number",
                CoreTypes.Array => "Backing.Array",
                CoreTypes.Object => "Backing.Object",
                _ => throw new InvalidOperationException($"Unsupported backing type {valueCoreTypes}"),
            };
        }

        static string GetValue(CoreTypes typeDeclarationImpliedCoreTypes, CoreTypes valueCoreTypes, string defaultValue)
        {
            return (typeDeclarationImpliedCoreTypes & valueCoreTypes) != 0
                ? "value"
                : defaultValue;
        }
    }

    /// <summary>
    /// End a method declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndMethodDeclaration(this CodeGenerator generator)
    {
        return generator
            .PopMemberScope()
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a parameter list. This will produce the parameters on a single line
    /// for 0, 1, or 2 parameters, and an indented multi-line list for 3 or more parameters.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter list.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterList(
        this CodeGenerator generator,
        params MethodParameter[] parameters)
    {
        if (parameters.Length < 3)
        {
            return AppendParameterListSingleLine(generator, parameters);
        }

        return AppendParameterListIndent(generator, parameters);
    }

    /// <summary>
    /// Append a parameter list on a single line.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter list.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterListSingleLine(CodeGenerator generator, MethodParameter[] parameters)
    {
        if (parameters.Length == 0)
        {
            // If we have no parameters, just emit the brackets.
            return generator.AppendLine("()");
        }

        generator.Append("(");
        bool first = true;

        foreach (MethodParameter parameter in parameters)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                generator.Append(", ");
            }

            generator.AppendParameter(parameter);
        }

        return generator
            .AppendLine(")");
    }

    /// <summary>
    /// Append a parameter list on multiple lines, indented.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter list.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterListIndent(CodeGenerator generator, MethodParameter[] parameters)
    {
        if (parameters.Length == 0)
        {
            // If we have no parameters, just emit the brackets.
            return generator.AppendLine("()");
        }

        generator.AppendLine("(");
        generator.PushIndent();
        bool first = true;

        foreach (MethodParameter parameter in parameters)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                generator.AppendLine(",");
            }

            generator.AppendParameterIndent(parameter);
        }

        return generator
            .PopIndent()
            .AppendLine(")");
    }

    /// <summary>
    /// Append a parameter in a parameter list.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter.</param>
    /// <param name="parameter">The parameter to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterIndent(
        this CodeGenerator generator,
        MethodParameter parameter)
    {
        string name = parameter.GetName(generator, isDeclaration: true);

        return generator
            .AppendIndent(parameter.TypeAndModifiers)
            .Append(' ')
            .Append(name);
    }

    /// <summary>
    /// Append a parameter in a parameter list.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter.</param>
    /// <param name="parameter">The parameter to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameter(
        this CodeGenerator generator,
        MethodParameter parameter)
    {
        string name = parameter.GetName(generator, isDeclaration: true);

        return generator
            .Append(parameter.TypeAndModifiers)
            .Append(' ')
            .Append(name);
    }

    /// <summary>
    /// Emits the parent/child nesting.
    /// </summary>
    /// <param name="generator">The generator to which to append the parent/child declaration nesting.</param>
    /// <param name="typeDeclaration">The type declaration being emitted.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginTypeDeclarationNesting(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        Stack<TypeDeclaration> parentTypes = new();

        TypeDeclaration? current = typeDeclaration.Parent();

        // We need to reverse the order, so we push them onto a stack...
        while (current is not null)
        {
            parentTypes.Push(current);
            current = current.Parent();
        }

        // ...and then pop them off again.
        while (parentTypes.Count > 0)
        {
            TypeDeclaration parent = parentTypes.Pop();
            generator
                .AppendSeparatorLine()
                .AppendDocumentation(parent)
                .BeginPublicReadonlyPartialStructDeclaration(
                    parent.DotnetTypeName());
        }

        return generator;
    }

    /// <summary>
    /// Closes off the parent/child nesting.
    /// </summary>
    /// <param name="generator">The generator to which to append the nested-type closing.</param>
    /// <param name="typeDeclaration">The type declaration being emitted.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndTypeDeclarationNesting(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        TypeDeclaration? current = typeDeclaration.Parent();
        while (current is not null)
        {
            generator.EndClassOrStructDeclaration();
            current = current.Parent();
        }

        return generator;
    }

    /// <summary>
    /// Emits the end of a class or struct declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the end of the struct declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndClassOrStructDeclaration(this CodeGenerator generator)
    {
        return generator
            .PopMemberScope()
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a numeric string.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNumericLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Number, "The value must be a number.");

        generator.Append(value.GetRawText());

        if (!value.TryGetDouble(out double _))
        {
            // Fall back to a decimal if we can't process the value with a double.
            generator.Append("M");
        }

        return generator;
    }

    /// <summary>
    /// Append a quoted string value.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendQuotedStringLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.String, "The value must be a string.");

        generator.Append(SymbolDisplay.FormatLiteral(value.GetString()!, true));

        return generator;
    }

    /// <summary>
    /// Append a quoted string value.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendQuotedStringLiteral(this CodeGenerator generator, string value)
    {
        return generator
            .Append(SymbolDisplay.FormatLiteral(value, true));
    }

    /// <summary>
    /// Append an object serialized as a string literal.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSerializedObjectStringLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Object, "The value must be an object.");

        return generator
            .Append(SymbolDisplay.FormatLiteral(value.GetRawText(), true));
    }

    /// <summary>
    /// Append an array serialized as a string literal.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSerializedArrayStringLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Array, "The value must be an array.");

        return generator
            .Append(SymbolDisplay.FormatLiteral(value.GetRawText(), true));
    }

    /// <summary>
    /// Format a type name of the form <c>{genericTypeName}&lt;{typeDeclaration.DotnetTypeName()}&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="genericTypeName">The name of the genertic type.</param>
    /// <param name="typeDeclaration">The type declaration for which to form the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator GenericTypeOf(
        this CodeGenerator generator,
        string genericTypeName,
        TypeDeclaration typeDeclaration)
    {
        return generator
                .Append(genericTypeName)
                .Append('<')
                .Append(typeDeclaration.FullyQualifiedDotnetTypeName())
                .Append('>');
    }

    /// <summary>
    /// Format a type name of the form
    /// <c>{genericTypeName}&lt;{typeDeclaration1.FullyQualifiedDotnetTypeName()}, {typeDeclaration2.FullyQualifiedDotnetTypeName()}}&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="genericTypeName">The name of the genertic type.</param>
    /// <param name="typeDeclaration1">The first type declaration from which to form the name.</param>
    /// <param name="typeDeclaration2">The second type declaration from which to form the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator GenericTypeOf(
        this CodeGenerator generator,
        string genericTypeName,
        TypeDeclaration typeDeclaration1,
        TypeDeclaration typeDeclaration2)
    {
        return generator
                .Append(genericTypeName)
                .Append('<')
                .Append(typeDeclaration1.FullyQualifiedDotnetTypeName())
                .Append(", ")
                .Append(typeDeclaration2.FullyQualifiedDotnetTypeName())
                .Append('>');
    }

    /// <summary>
    /// Format a type name of the form
    /// <c>{genericTypeName}&lt;{typeDeclaration1.FullyQualifiedDotnetTypeName()}, {typeDeclaration2.FullyQualifiedDotnetTypeName()}, {typeDeclaration3.FullyQualifiedDotnetTypeName()}&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="genericTypeName">The name of the genertic type.</param>
    /// <param name="typeDeclaration1">The 1st type declaration from which to form the name.</param>
    /// <param name="typeDeclaration2">The 2nd type declaration from which to form the name.</param>
    /// <param name="typeDeclaration3">The 3rd type declaration from which to form the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator GenericTypeOf(
        this CodeGenerator generator,
        string genericTypeName,
        TypeDeclaration typeDeclaration1,
        TypeDeclaration typeDeclaration2,
        TypeDeclaration typeDeclaration3)
    {
        return generator
                .Append(genericTypeName)
                .Append('<')
                .Append(typeDeclaration1.FullyQualifiedDotnetTypeName())
                .Append(", ")
                .Append(typeDeclaration2.FullyQualifiedDotnetTypeName())
                .Append(", ")
                .Append(typeDeclaration3.FullyQualifiedDotnetTypeName())
                .Append('>');
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

    /// <summary>
    /// Emits the start of a partial struct declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <param name="interfaces">Interfaces to implement.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPublicReadonlyPartialStructDeclaration(
        this CodeGenerator generator,
        string dotnetTypeName,
        ConditionalCodeSpecification[]? interfaces = null)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        generator
            .AppendIndent("public readonly partial struct ")
            .AppendLine(name);

        if (interfaces is ConditionalCodeSpecification[] conditionalSpecifications)
        {
            generator.PushIndent();
            ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(generator, conditionalSpecifications, AppendInterface);
            generator.PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();

        static void AppendInterface(CodeGenerator generator, Action<CodeGenerator> appendFunction, int elementIndexInConditionalBlock)
        {
            if (elementIndexInConditionalBlock == 0)
            {
                generator.AppendIndent(": ");
                appendFunction(generator);
            }
            else
            {
                generator
                    .AppendLine(",")
                    .AppendIndent("  "); // Align with the ": "
                appendFunction(generator);
            }
        }
    }

    /// <summary>
    /// Emits the start of a private static class declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPrivateStaticClassDeclaration(this CodeGenerator generator, string dotnetTypeName)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        return generator
            .AppendIndent("private static class ")
            .AppendLine(name)
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();
    }

    /// <summary>
    /// Emits the start of a public static class declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPublicStaticClassDeclaration(this CodeGenerator generator, string dotnetTypeName)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        return generator
            .AppendIndent("public static class ")
            .AppendLine(name)
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();
    }

    /// <summary>
    /// Emits the auto-generated header.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAutoGeneratedHeader(this CodeGenerator generator)
    {
        return generator
            .AppendLine(
            """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            """);
    }

    /// <summary>
    /// Append the JsonConverter attribute.
    /// </summary>
    /// <param name="generator">The generator to which to append the JsonConverter attribute.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonConverterAttribute(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendIndent("[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(">))]");
    }

    /// <summary>
    /// Append documentation for the type declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append documentation.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendDocumentation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ShortDocumentation() is string shortDocumentation)
        {
            generator.AppendSummary(shortDocumentation);
        }
        else
        {
            generator.AppendSummary("Generated from JSON Schema.");
        }

        return generator
            .AppendRemarks(typeDeclaration.LongDocumentation(), typeDeclaration.Examples());
    }

    /// <summary>
    /// Appends a summary, ensuring that the summary text is correctly formatted.
    /// </summary>
    /// <param name="generator">The generator to which to append the summary.</param>
    /// <param name="summaryText">The summary text to append. Multiple lines will be correctly indented.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSummary(this CodeGenerator generator, string summaryText)
    {
        return generator
            .AppendLineIndent("/// <summary>")
            .AppendBlockIndentWithPrefix(summaryText, "/// ")
            .AppendLineIndent("/// </summary>");
    }

    /// <summary>
    /// Appends remarks, ensuring that the summary text is correctly formatted.
    /// </summary>
    /// <param name="generator">The generator to which to append the summary.</param>
    /// <param name="longDocumentation">The (optional) long documentation text.</param>
    /// <param name="documentationExamples">The (optional) array of examples text.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// If neither long documentation nor examples are present, then this will not append a remarks section.
    /// </remarks>
    public static CodeGenerator AppendRemarks(this CodeGenerator generator, string? longDocumentation, string[]? documentationExamples)
    {
        if (longDocumentation is null && documentationExamples is null)
        {
            return generator;
        }

        generator.AppendLineIndent("/// <remarks>");

        if (longDocumentation is string docs)
        {
            generator.AppendParagraphs(docs);
        }

        if (documentationExamples is string[] examples)
        {
            generator.AppendExamples(examples);
        }

        return generator
            .AppendLineIndent("/// </remarks>");
    }

    /// <summary>
    /// Append the examples as a code-formatted examples paragraph.
    /// </summary>
    /// <param name="generator">The generator to which to append the example.</param>
    /// <param name="examples">The examples to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExamples(this CodeGenerator generator, string[] examples)
    {
        generator
            .AppendLineIndent("/// <para>")
            .AppendLineIndent("/// Examples:");

        foreach (string example in examples)
        {
            generator.AppendExample(example);
        }

        return generator
            .AppendLineIndent("/// </para>");
    }

    /// <summary>
    /// Append the text as a code-formatted example.
    /// </summary>
    /// <param name="generator">The generator to which to append the example.</param>
    /// <param name="example">The example to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExample(this CodeGenerator generator, string example)
    {
        return generator
            .AppendLineIndent("/// <example>")
            .AppendLineIndent("/// <code>")
            .AppendBlockIndentWithPrefix(example, "/// ")
            .AppendLineIndent("/// </code>")
            .AppendLineIndent("/// </example>");
    }

    /// <summary>
    /// Append the text as paragraphs, splitting on newline and/or carriage return.
    /// </summary>
    /// <param name="generator">The generator to which to append the paragraphs.</param>
    /// <param name="paragraphs">The text containing the paragraphs to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParagraphs(this CodeGenerator generator, string paragraphs)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(paragraphs, removeBlankLines: true);
        foreach (string line in lines)
        {
            generator
                .AppendLineIndent("/// <para>")
                .AppendIndent("/// ")
                .AppendLine(SymbolDisplay.FormatLiteral(line, false))
                .AppendLineIndent("/// </para>");
        }

        return generator;
    }

    /// <summary>
    /// Append a multi-line block of text at the given indent.
    /// </summary>
    /// <param name="generator">The generator to which to append the block.</param>
    /// <param name="block">The block to append.</param>
    /// <param name="trimWhitespaceOnlyLines">Whether to trim lines that are whitespace only.</param>
    /// <param name="omitLastLineEnd">If <see langword="true"/> then the last line is appended without an additional line-end, leaving
    /// the generator at the end of the block.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBlockIndent(this CodeGenerator generator, string block, bool trimWhitespaceOnlyLines = true, bool omitLastLineEnd = false)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(block);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (omitLastLineEnd && i == lines.Length - 1)
            {
                generator
                    .AppendIndent(line);
            }
            else
            {
                generator
                    .AppendLineIndent(line, trimWhitespaceOnlyLines);
            }
        }

        return generator;
    }

    /// <summary>
    /// Append a multi-line block of text at the given indent.
    /// </summary>
    /// <param name="generator">The generator to which to append the block.</param>
    /// <param name="block">The block to append.</param>
    /// <param name="trimWhitespaceOnlyLines">Whether to trim lines that are whitespace only.</param>
    /// <param name="omitLastLineEnd">If <see langword="true"/> then the last line is appended without an additional line-end, leaving
    /// the generator at the end of the block.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBlockIndentWithHashOutdent(this CodeGenerator generator, string block, bool trimWhitespaceOnlyLines = true, bool omitLastLineEnd = false)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(block);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (omitLastLineEnd && i == lines.Length - 1)
            {
                if (line[0] == '#')
                {
                    generator.Append(line);
                }
                else
                {
                    generator
                        .AppendIndent(line);
                }
            }
            else
            {
                if (line.Length > 0 && line[0] == '#')
                {
                    generator.AppendLine(line);
                }
                else
                {
                    generator
                        .AppendLineIndent(line, trimWhitespaceOnlyLines);
                }
            }
        }

        return generator;
    }

    /// <summary>
    /// Append a multi-line block of text at the given indent, with a given line prefix.
    /// </summary>
    /// <param name="generator">The generator to which to append the block.</param>
    /// <param name="block">The block to append.</param>
    /// <param name="linePrefix">The prefix for each line.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBlockIndentWithPrefix(this CodeGenerator generator, string block, string linePrefix)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(block);
        foreach (string line in lines)
        {
            generator
                .AppendIndent(linePrefix)
                .AppendLine(line);
        }

        return generator;
    }

    /// <summary>
    /// Appends a public static readonly field.
    /// </summary>
    /// <param name="generator">The generator to which to append the field.</param>
    /// <param name="type">The field type name.</param>
    /// <param name="name">The name of the field.</param>
    /// <param name="value">An (optional) initializer value for the field.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicStaticReadonlyField(
        this CodeGenerator generator,
        string type,
        string name,
        string? value)
    {
        generator
            .AppendIndent("public static readonly ")
            .Append(type)
            .Append(' ')
            .Append(generator.GetStaticReadOnlyFieldNameInScope(name));

        if (value is string intializerValue)
        {
            generator
                .Append(" = ")
                .Append(intializerValue);
        }

        return generator
            .AppendLine(";");
    }

    /// <summary>
    /// Gets the name for a field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets unique name for a field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetStaticReadOnlyFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueStaticReadOnlyFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetPropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniquePropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator ReserveName(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.ReserveName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator ReserveNameIfNotReserved(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.ReserveNameIfNotReserved(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Tries to reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static bool TryReserveName(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.TryReserveName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a variable.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetVariableNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a type.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetTypeNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
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

    private static CodeGenerator AppendConversionFromValue(
        this CodeGenerator generator,
        string identifierName,
        CoreTypes forTypes)
    {
        generator
            .AppendIndent("if (")
            .Append(identifierName)
            .AppendLine(".HasJsonElementBacking)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return new(")
                .Append(identifierName)
                .AppendLine(".AsJsonElement);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLine()
            .AppendIndent("return ")
            .Append(identifierName)
            .AppendLine(".ValueKind switch")
            .AppendLineIndent("{")
            .PushIndent();

        if ((forTypes & CoreTypes.String) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.String => new((string)")
                .Append(identifierName)
                .AppendLine(".AsString),");
        }

        if ((forTypes & CoreTypes.Boolean) != 0)
        {
            generator
                .AppendLineIndent("JsonValueKind.True => new(true),")
                .AppendLineIndent("JsonValueKind.False => new(false),");
        }

        if ((forTypes & CoreTypes.Number) != 0 ||
            (forTypes & CoreTypes.Integer) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.Number => new(")
                .Append(identifierName)
                .AppendLine(".AsNumber.AsBinaryJsonNumber),");
        }

        if ((forTypes & CoreTypes.Array) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.Array => new(")
                .Append(identifierName)
                .AppendLine(".AsArray.AsImmutableList()),");
        }

        if ((forTypes & CoreTypes.Object) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.Object => new(")
                .Append(identifierName)
                .AppendLine(".AsObject.AsPropertyBacking()),");
        }

        return generator
            .AppendLineIndent("JsonValueKind.Null => Null,")
            .AppendLineIndent("_ => Undefined,")
            .PopIndent()
            .AppendLineIndent("};");
    }

    private static CodeGenerator AppendBackingField(
        this CodeGenerator generator,
        string fieldType,
        string fieldName,
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendIndent("private readonly ")
                .Append(fieldType)
                .Append(' ')
                .Append(localBackingFieldName)
                .AppendLine(";");
        }

        return generator;
    }

    private static CodeGenerator AppendBackingFieldAssignment(
        this CodeGenerator generator,
        string fieldName,
        string fieldValue,
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendIndent("this.")
                .Append(localBackingFieldName)
                .Append(" = ")
                .Append(fieldValue)
                .AppendLine(";");
        }

        return generator;
    }

    private static CodeGenerator AppendConditionalConstructFromBacking(
        this CodeGenerator generator,
        string backingType,
        string fieldName,
        string identifier = "this",
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any)
    {
        return AppendConditionalWrappedBackingValueLineIndent(
            generator,
            backingType,
            "return new(",
            fieldName,
            ");",
            identifier,
            impliedCoreTypes,
            forCoreTypes);
    }

    private static CodeGenerator AppendConditionalWrappedBackingValueLineIndent(
        this CodeGenerator generator,
        string backingType,
        string prefix,
        string fieldName,
        string suffix,
        string identifier = "this",
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any,
        bool returnFromClause = false)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string backingName = generator.GetFieldNameInScope("backing");
            string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendSeparatorLine()
                .AppendIndent("if ((")
                .Append(identifier)
                .Append('.')
                .Append(backingName)
                .Append(" & ")
                .Append(backingType)
                .AppendLine(") != 0)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendIndent(prefix)
                .Append(identifier)
                .Append('.')
                .Append(localBackingFieldName)
                .AppendLine(suffix);

            if (returnFromClause)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendConditionalBackingValueLineIndent(
        this CodeGenerator generator,
        string backingType,
        string content,
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any,
        bool returnFromClause = false)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string backingName = generator.GetFieldNameInScope("backing");
            generator
                .AppendSeparatorLine()
                .AppendIndent("if ((this.")
                .Append(backingName)
                .Append(" & ")
                .Append(backingType)
                .AppendLine(") != 0)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent(content);

            if (returnFromClause)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendWriteJsonElementBacking(this CodeGenerator generator, string fieldName)
    {
        return generator
            .AppendIndent("if (this.")
            .Append(fieldName)
            .AppendLine(".ValueKind != JsonValueKind.Undefined)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendIndent("this.")
            .Append(fieldName)
            .AppendLine(".WriteTo(writer);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendConditionalBackingValueCallbackIndent(
        this CodeGenerator generator,
        string backingType,
        string fieldName,
        Action<CodeGenerator, string> callback,
        string identifier = "this",
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any,
        bool returnFromClause = false)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string backingName = generator.GetFieldNameInScope("backing");
            string localFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendSeparatorLine()
                .AppendIndent("if ((")
                .Append(identifier)
                .Append('.')
                .Append(backingName)
                .Append(" & ")
                .Append(backingType)
                .AppendLine(") != 0)")
                .AppendLineIndent("{")
                .PushIndent();

            callback(generator, localFieldName);

            if (returnFromClause)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendReturnNullInstanceIfNull(this CodeGenerator generator)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent("if (")
            .AppendTestBacking("Backing.Null")
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return JsonAny.Null;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendReturnNullJsonElementIfNull(this CodeGenerator generator)
    {
        return generator
            .AppendIndent("if (")
            .AppendTestBacking("Backing.Null")
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return JsonValueHelpers.NullElement;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendTestBacking(
        this CodeGenerator generator,
        string backingType)
    {
        string backingName = generator.GetFieldNameInScope("backing");
        return generator
            .Append("(this.")
            .Append(backingName)
            .Append(" & ")
            .Append(backingType)
            .Append(") != 0");
    }

    private static CodeGenerator AppendBackingFieldAssignment(
        this CodeGenerator generator,
        string fieldName,
        string fieldValue)
    {
        string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
        return generator
            .AppendIndent("this.")
            .Append(localBackingFieldName)
            .Append(" = ")
            .Append(fieldValue)
            .AppendLine(";");
    }

    private static CodeGenerator AppendCommaSeparatedNumericSuffixItems(this CodeGenerator generator, string baseName, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append(baseName)
                .Append(i + 1);
        }

        return generator;
    }

    private static CodeGenerator AppendCommaSeparatedNumericSuffixItems(this CodeGenerator generator, string baseNameFirst, string baseNameSecond, int count, string separator = " ")
    {
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append(baseNameFirst)
                .Append(i + 1)
                .Append(separator)
                .Append(baseNameSecond)
                .Append(i + 1);
        }

        return generator;
    }

    private static CodeGenerator AppendCommaSeparatedInParameterAndNumericSuffixItems(this CodeGenerator generator, string baseNameType, string baseNameForNumericSuffix, int count, string separator = " ")
    {
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append("in ")
                .Append(baseNameType)
                .Append(separator)
                .Append(baseNameForNumericSuffix)
                .Append(i + 1);
        }

        return generator;
    }

    private static CodeGenerator AppendCommaSeparatedValueItems(this CodeGenerator generator, TupleTypeDeclaration tupleType)
    {
        return generator.AppendCommaSeparatedNumericSuffixItems("value.Item", tupleType.ItemsTypes.Length);
    }

    private static CodeGenerator AppendCommaSeparatedTupleTypes(this CodeGenerator generator, TupleTypeDeclaration tupleType)
    {
        for (int i = 0; i < tupleType.ItemsTypes.Length; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append(tupleType.ItemsTypes[i].ReducedType.FullyQualifiedDotnetTypeName());
        }

        return generator;
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
            .AppendLineIndent("while (index < Dimension)")
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
                .AppendLine(".Dimension));")
                .AppendLineIndent("builder.Add(child);")
                .AppendIndent("index += ")
                .Append(arrayItemsType.FullyQualifiedDotnetTypeName())
                .AppendLine(".Dimension;");
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

    private static string[] NormalizeAndSplitBlockIntoLines(string block, bool removeBlankLines = false)
    {
        string normalizedBlock = block.Replace("\r\n", "\n");
        string[] lines = normalizedBlock.Split(['\n'], removeBlankLines ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
        return lines;
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
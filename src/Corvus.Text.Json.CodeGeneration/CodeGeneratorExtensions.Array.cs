// <copyright file="CodeGeneratorExtensions.Array.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions for array-related functionality.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Append mutation methods for arrays.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the mutators.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayMutators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypes() & CoreTypes.Array) == 0)
        {
            return generator;
        }

        // Do not add the standard mutators if this is a tuple.
        if (typeDeclaration.IsTuple())
        {
            return generator;
        }

        // Get the type for the array items.
        string fqdtn;

        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration itemsType)
        {
            if (itemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = itemsType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.ExplicitUnevaluatedItemsType() is ArrayItemsTypeDeclaration unevaluatedItemsType)
        {
            if (unevaluatedItemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = unevaluatedItemsType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

        // Set an item
        generator
            .ReserveNameIfNotReserved("SetItem")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Sets the value of an array element at the specified index.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"itemIndex\">The zero-based index of the array element to set.</param>")
            .AppendLineIndent("/// <param name=\"value\">The item value to set.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentOutOfRangeException\">")
            .AppendLineIndent("///   <paramref name=\"itemIndex\"/> is negative or greater than the array length.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     This method allows replacing existing array elements or appending new elements")
            .AppendLineIndent("///     when <paramref name=\"itemIndex\"/> equals the current array length.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void SetItem(int itemIndex, in ", fqdtn, ".", generator.SourceClassName(fqdtn), " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendSeparatorLine()
                .AppendLineIndent("if (value.IsUndefined)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("RemoveAt(itemIndex);")
                    .AppendLineIndent("return;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 30);")
                .AppendLineIndent("value.AddAsItem(ref cvb);")
                .AppendLineIndent("int arrayLength = GetArrayLength();")
                .AppendLineIndent("if (itemIndex == arrayLength)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("_parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("_parent.GetArrayIndexElement(_idx, itemIndex, out IMutableJsonDocument elementParent, out int elementIdx);")
                    .AppendLineIndent("_parent.OverwriteAndDispose(_idx, elementIdx, elementIdx + elementParent.GetDbSize(elementIdx, true), 1, ref cvb);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}");

        // Insert an item
        generator
            .ReserveNameIfNotReserved("InsertItem")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Inserts an item into the array at the specified index.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"itemIndex\">The zero-based index of the array element at which to insert.</param>")
            .AppendLineIndent("/// <param name=\"value\">The item value to insert.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentOutOfRangeException\">")
            .AppendLineIndent("///   <paramref name=\"itemIndex\"/> is negative or greater than the array length.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     This method allows inserting array elements or appending new elements")
            .AppendLineIndent("///     when <paramref name=\"itemIndex\"/> equals the current array length.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void InsertItem(int itemIndex, in ", fqdtn, ".", generator.SourceClassName(fqdtn), " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendSeparatorLine()
                .AppendLineIndent("if (value.IsUndefined)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 30);")
                .AppendLineIndent("value.AddAsItem(ref cvb);")
                .AppendLineIndent("_parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}");

        // Add an item (append to end)
        generator
            .ReserveNameIfNotReserved("AddItem")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Adds an item to the end of the array.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The item value to add.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void AddItem(in ", fqdtn, ".", generator.SourceClassName(fqdtn), " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("InsertItem(GetArrayLength(), in value);")
            .PopIndent()
            .AppendLineIndent("}");

        // InsertRange (insert multiple items using a builder delegate)
        generator
            .ReserveNameIfNotReserved("InsertRange")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Inserts multiple items into the array at the specified index,")
            .AppendLineIndent("///   using an <see cref=\"JsonElement.ArrayBuilder\"/> delegate to build the items.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"itemIndex\">The zero-based index at which to insert the items.</param>")
            .AppendLineIndent("/// <param name=\"rangeBuilder\">A delegate that adds items to an <see cref=\"JsonElement.ArrayBuilder\"/>.</param>")
            .AppendLineIndent("/// <param name=\"estimatedMemberCount\">The estimated total number of elements for capacity optimization.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentOutOfRangeException\">")
            .AppendLineIndent("///   <paramref name=\"itemIndex\"/> is negative or greater than the array length.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     Only the items built by the delegate are inserted — no array wrapper is added.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("public void InsertRange(int itemIndex, JsonElement.ArrayBuilder.Build rangeBuilder, int estimatedMemberCount = 30)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);")
                .AppendLineIndent("JsonElement.ArrayBuilder.BuildItems(rangeBuilder, ref cvb);")
                .AppendLineIndent("_parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Inserts multiple items into the array at the specified index,")
            .AppendLineIndent("///   using an <see cref=\"JsonElement.ArrayBuilder\"/> delegate with a context parameter.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <typeparam name=\"TContext\">The type of the context to pass to the builder.</typeparam>")
            .AppendLineIndent("/// <param name=\"itemIndex\">The zero-based index at which to insert the items.</param>")
            .AppendLineIndent("/// <param name=\"context\">The context to pass to the builder delegate.</param>")
            .AppendLineIndent("/// <param name=\"rangeBuilder\">A delegate that adds items to an <see cref=\"JsonElement.ArrayBuilder\"/>.</param>")
            .AppendLineIndent("/// <param name=\"estimatedMemberCount\">The estimated total number of elements for capacity optimization.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentOutOfRangeException\">")
            .AppendLineIndent("///   <paramref name=\"itemIndex\"/> is negative or greater than the array length.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     Only the items built by the delegate are inserted — no array wrapper is added.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("public void InsertRange<TContext>(int itemIndex, in TContext context, JsonElement.ArrayBuilder.Build<TContext> rangeBuilder, int estimatedMemberCount = 30)")
            .AppendLineIndent("#if NET9_0_OR_GREATER")
            .AppendLineIndent("    where TContext : allows ref struct")
            .AppendLineIndent("#endif")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);")
                .AppendLineIndent("JsonElement.ArrayBuilder.BuildItems(context, rangeBuilder, ref cvb);")
                .AppendLineIndent("_parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}");

        // AddRange (append multiple items to end using a builder delegate)
        generator
            .ReserveNameIfNotReserved("AddRange")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Appends multiple items to the end of the array,")
            .AppendLineIndent("///   using an <see cref=\"JsonElement.ArrayBuilder\"/> delegate to build the items.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"rangeBuilder\">A delegate that adds items to an <see cref=\"JsonElement.ArrayBuilder\"/>.</param>")
            .AppendLineIndent("/// <param name=\"estimatedMemberCount\">The estimated total number of elements for capacity optimization.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void AddRange(JsonElement.ArrayBuilder.Build rangeBuilder, int estimatedMemberCount = 30)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("InsertRange(GetArrayLength(), rangeBuilder, estimatedMemberCount);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Appends multiple items to the end of the array,")
            .AppendLineIndent("///   using an <see cref=\"JsonElement.ArrayBuilder\"/> delegate with a context parameter.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <typeparam name=\"TContext\">The type of the context to pass to the builder.</typeparam>")
            .AppendLineIndent("/// <param name=\"context\">The context to pass to the builder delegate.</param>")
            .AppendLineIndent("/// <param name=\"rangeBuilder\">A delegate that adds items to an <see cref=\"JsonElement.ArrayBuilder\"/>.</param>")
            .AppendLineIndent("/// <param name=\"estimatedMemberCount\">The estimated total number of elements for capacity optimization.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void AddRange<TContext>(in TContext context, JsonElement.ArrayBuilder.Build<TContext> rangeBuilder, int estimatedMemberCount = 30)")
            .AppendLineIndent("#if NET9_0_OR_GREATER")
            .AppendLineIndent("    where TContext : allows ref struct")
            .AppendLineIndent("#endif")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("InsertRange(GetArrayLength(), context, rangeBuilder, estimatedMemberCount);")
            .PopIndent()
            .AppendLineIndent("}");

        // Remove items
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("RemoveRange")
            .ReserveNameIfNotReserved("Remove")
            .ReserveNameIfNotReserved("RemoveWhere")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes a range of items from the array starting at the specified index.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"startIndex\">The zero-based index at which to begin removing items.</param>")
            .AppendLineIndent("/// <param name=\"count\">The number of items to remove.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentOutOfRangeException\">")
            .AppendLineIndent("///   <paramref name=\"startIndex\"/> is negative or greater than the current array length.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void RemoveRange(int startIndex, int count)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("JsonElementHelpers.RemoveRangeUnsafe(this, startIndex, count);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes a single item from the array at the specified index.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"index\">The zero-based index of the item to remove.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentOutOfRangeException\">")
            .AppendLineIndent("///   <paramref name=\"index\"/> is negative or greater than or equal to the current array length.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void RemoveAt(int index)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("JsonElementHelpers.RemoveRangeUnsafe(this, index, 1);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes the first array element that equals the specified item.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"item\">The item to find and remove.</param>")
            .AppendLineIndent("/// <returns><see langword=\"true\"/> if an element was found and removed; otherwise, <see langword=\"false\"/>.</returns>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("public bool Remove(in ", fqdtn, " item)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("if (!JsonElementHelpers.RemoveFirstUnsafe<Mutable, ", fqdtn, ">(this, in item))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return false;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("_documentVersion = _parent.Version;")
                .AppendLineIndent("return true;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes all array elements that match the specified predicate.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"predicate\">The predicate function that determines which elements to remove.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ArgumentNullException\">")
            .AppendLineIndent("///   <paramref name=\"predicate\"/> is <see langword=\"null\"/>.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     This method efficiently removes elements in a single pass by iterating backwards")
            .AppendLineIndent("///     through the array and removing consecutive blocks of matching elements.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     The predicate function is called for each element in the array. If the predicate")
            .AppendLineIndent("///     returns <see langword=\"true\"/>, the element will be removed from the array.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void RemoveWhere(JsonPredicate<", fqdtn, "> predicate)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("JsonElementHelpers.RemoveWhereUnsafe<Mutable, ", fqdtn, ">(this, predicate);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}");

        // Replace items
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("Replace")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Replaces the first array element that equals the specified item with a new value.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"oldItem\">The item to find.</param>")
            .AppendLineIndent("/// <param name=\"newItem\">The value to replace it with.</param>")
            .AppendLineIndent("/// <returns><see langword=\"true\"/> if an element was found and replaced; otherwise, <see langword=\"false\"/>.</returns>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Array\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("public bool Replace(in ", fqdtn, " oldItem, in ", fqdtn, ".", generator.SourceClassName(fqdtn), " newItem)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendSeparatorLine()
                .AppendLineIndent("if (newItem.IsUndefined)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return Remove(in oldItem);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("var enumerator = EnumeratorCreator.CreateArrayEnumerator<", fqdtn, ">(_parent, _idx);")
                .AppendSeparatorLine()
                .AppendLineIndent("while (enumerator.MoveNext())")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(fqdtn, " current = enumerator.Current;")
                    .AppendLineIndent("if (JsonElementHelpers.DeepEquals(in current, in oldItem))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 30);")
                        .AppendLineIndent("newItem.AddAsItem(ref cvb);")
                        .AppendSeparatorLine()
                        .AppendLineIndent("int elementStart = ((IJsonElement)current).ParentDocumentIndex;")
                        .AppendLineIndent("int elementEnd = elementStart + ((IJsonElement)current).ParentDocument.GetDbSize(elementStart, true);")
                        .AppendLineIndent("_parent.OverwriteAndDispose(_idx, elementStart, elementEnd, 1, ref cvb);")
                        .AppendSeparatorLine()
                        .AppendLineIndent("_documentVersion = _parent.Version;")
                        .AppendLineIndent("return true;")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    /// <summary>
    /// Append the static property which provides the dimension (fixed length) of the array.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayDimensionStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ArrayDimension() is int dimension)
        {
            generator
                .ReserveName("Dimension")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                /// <summary>
                /// Gets the fixed length of the array at its current rank.
                /// </summary>
                """)
                .AppendIndent("public static int Dimension => ")
                .Append(dimension)
                .AppendLine(";");
        }

        return generator;
    }

    /// <summary>
    /// Append array indexer properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <param name="forMutable">Whether to emit the indexers for the mutable element.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayIndexerProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Array) == 0)
        {
            return generator;
        }

        string fqdtn;

        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration itemsType)
        {
            if (itemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = itemsType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.ExplicitUnevaluatedItemsType() is ArrayItemsTypeDeclaration unevaluatedItemsType)
        {
            if (unevaluatedItemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = unevaluatedItemsType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

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
                .AppendLineIndent("public ", fqdtn, forMutable ? ".Mutable" : "", " this[int index]")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("get")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CheckValidInstance();")
                        .AppendLineIndent("return _parent.GetArrayIndexElement<", fqdtn, forMutable ? ".Mutable" : "", ">(_idx, index);")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");

        return generator;
    }

    /// <summary>
    /// Append tuple item properties (Item1, Item2, ...) for types that are pure tuples.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the properties.</param>
    /// <param name="forMutable">Whether to emit the properties for the mutable element.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTupleItemProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Array) == 0)
        {
            return generator;
        }

        TupleTypeDeclaration? tupleType =
            typeDeclaration.TupleType() ??
            typeDeclaration.ExplicitTupleType() ??
            typeDeclaration.ImplicitTupleType();

        if (tupleType is null)
        {
            return generator;
        }

        int index = 0;
        foreach (ReducedTypeDeclaration item in tupleType.ItemsTypes)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (item.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                index++;
                continue;
            }

            string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
            int itemNumber = index + 1;

            generator
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the tuple item.
                /// </summary>
                /// <exception cref="IndexOutOfRangeException">The index was outside the bounds of the array.</exception>
                /// <exception cref="InvalidOperationException">The value is not an array.</exception>
                """)
                .AppendLineIndent("public ", fqdtn, forMutable ? ".Mutable" : "", " Item", itemNumber.ToString())
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("get")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CheckValidInstance();")
                        .AppendLineIndent("return _parent.GetArrayIndexElement<", fqdtn, forMutable ? ".Mutable" : "", ">(_idx, ", index.ToString(), ");")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");

            index++;
        }

        return generator;
    }

    /// <summary>
    /// Append the static property which provides the rank of the array.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayRankStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

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
    /// Append the static property which provides the value buffer size of the array.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayValueBufferSizeStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

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
    /// Append EnumerateArray() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <param name="forMutable">Indicates that the method is for a mutable instance.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEnumerateArray(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Array) == 0)
        {
            return generator;
        }

        string fqdtn;

        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration itemsType)
        {
            if (itemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = itemsType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.ExplicitUnevaluatedItemsType() is ArrayItemsTypeDeclaration unevaluatedItemsType)
        {
            if (unevaluatedItemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = unevaluatedItemsType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

        if (forMutable)
        {
            fqdtn += ".Mutable";
        }

        return generator
            .AppendSeparatorLine()
            .ReserveName("EnumerateArray")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Enumerates the array.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">The value is not an array.</exception>")
            .AppendLineIndent("public ArrayEnumerator<", fqdtn, "> EnumerateArray()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("return EnumeratorCreator.CreateArrayEnumerator<", fqdtn, ">(_parent, _idx);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append GetArrayLength() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetArrayLength(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Array) == 0)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .ReserveName("GetArrayLength")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Gets the array length.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">The value is not an array.</exception>")
            .AppendLineIndent("public int GetArrayLength()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("return _parent.GetArrayLength(_idx);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append TryGetNumericValues() method for numeric array types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the method.</param>
    /// <param name="forMutable">Indicates that the method is for a mutable instance.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetNumericValues(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!typeDeclaration.IsNumericArray() || typeDeclaration.IsTuple())
        {
            return generator;
        }

        NumericTypeName? numericType = typeDeclaration.PreferredDotnetNumericTypeName();
        if (numericType is not NumericTypeName nt)
        {
            return generator;
        }

        if (typeDeclaration.ArrayItemsType() is not ArrayItemsTypeDeclaration itemsType)
        {
            return generator;
        }

        if (itemsType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
        {
            return generator;
        }

        string itemsFqdtn = itemsType.ReducedType.FullyQualifiedDotnetTypeName();
        if (forMutable)
        {
            itemsFqdtn += ".Mutable";
        }

        bool isLeaf = itemsType.ReducedType.ArrayRank() is null;

        if (nt.IsNetOnly)
        {
            generator.AppendLine("#if NET");
        }

        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("TryGetNumericValues")
            .AppendBlockIndent(
                """
            /// <summary>
            /// Tries to get the numeric values from the array into the given buffer.
            /// </summary>
            /// <param name="items">The buffer into which to write the values.</param>
            /// <param name="written">The number of values written to the buffer.</param>
            /// <returns><c>true</c> if the values were successfully written to the buffer, otherwise <c>false</c>.</returns>
            """)
            .AppendLineIndent("public bool TryGetNumericValues(Span<", nt.Name, "> items, out int written)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("written = 0;")
                .AppendLineIndent("foreach (", itemsFqdtn, " item in EnumerateArray())")
                .AppendLineIndent("{")
                .PushIndent();

        if (isLeaf)
        {
            generator
                    .AppendLineIndent("if (written >= items.Length)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return false;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (!item.TryGetValue(out ", nt.Name, " value))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return false;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("items[written++] = value;");
        }
        else
        {
            generator
                    .AppendLineIndent("if (!item.TryGetNumericValues(items.Slice(written), out int innerWritten))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("written += innerWritten;")
                        .AppendLineIndent("return false;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("written += innerWritten;");
        }

        generator
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return true;")
            .PopIndent()
            .AppendLineIndent("}");

        if (nt.IsNetOnly)
        {
            generator.AppendLine("#endif");
        }

        return generator;
    }
}
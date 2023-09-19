// <copyright file="JsonTransformingVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Visitor;

/// <summary>
/// A type which allows you to transform an existing tree.
/// </summary>
public static partial class JsonTransformingVisitor
{
    // Increase our path buffer in 1k increments
    private const int BufferChunkSize = 1024;

    /// <summary>
    /// A delegate for a visitor to the tree.
    /// </summary>
    /// <param name="path">The path visited.</param>
    /// <param name="nodeToVisit">The node to visit.</param>
    /// <param name="result">The result of the visit.</param>
    public delegate void Visitor(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result);

    /// <summary>
    /// Walk the tree, optionally transforming nodes.
    /// </summary>
    /// <typeparam name="T">The type of the root node.</typeparam>
    /// <param name="root">The root of the tree to walk.</param>
    /// <param name="visitor">The method to apply to each node.</param>
    /// <param name="result">The result of the transformation.</param>
    /// <returns>The transformed tree.</returns>
    public static bool Visit<T>(this T root, Visitor visitor, out JsonAny result)
        where T : struct, IJsonValue
    {
        char[] pathBuffer = ArrayPool<char>.Shared.Rent(BufferChunkSize);
        try
        {
            VisitResult visitResult = default;
            Visit(ReadOnlySpan<char>.Empty, root.AsAny, visitor, pathBuffer, ref visitResult);

            if (visitResult.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
            {
                result = root.AsAny;
            }
            else
            {
                result = visitResult.Output;
            }

            return visitResult.IsTransformed;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(pathBuffer, true);
        }
    }

    private static void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, Visitor visitor, char[] pathBuffer, ref VisitResult result)
    {
        // First, visit the entity itself
        visitor(path, nodeToVisit, ref result);

        if (result.Walk == Walk.RemoveAndContinue)
        {
            // We will just return immediately, and this case will be dealt with by VisitObject() or VisitArray().
            return;
        }

        if (result.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
        {
            // We're terminating, and abandoning changes, so just return this node.
            result.Output = nodeToVisit;
            result.Transformed = Transformed.No;
            result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
            return;
        }

        if (result.Walk == Walk.SkipChildren)
        {
            // Don't iterate into the children, but do continue the walk.
            // We're terminating, and abandoning changes, so just return this node.
            result.Output = nodeToVisit;
            result.Transformed = Transformed.No;
            result.Walk = Walk.Continue;
            return;
        }

        // If we are terminating here, don't visit the children.
        if (result.Walk != Walk.Continue)
        {
            return;
        }

        switch (result.Output.ValueKind)
        {
            case JsonValueKind.Object:
                VisitObject(path, result.Output.AsObject, visitor, pathBuffer, ref result);
                break;
            case JsonValueKind.Array:
                VisitArray(path, result.Output.AsArray, visitor, pathBuffer, ref result);
                break;
        }
    }

    private static void VisitArray(ReadOnlySpan<char> path, in JsonArray asArray, Visitor visitor, char[] pathBuffer, ref VisitResult result)
    {
        bool terminateEntireWalkApplyingChanges = false;
        bool hasTransformedItems = false;
        ImmutableList<JsonAny>.Builder builder;

        if (asArray.HasJsonElementBacking)
        {
            builder = ImmutableList.CreateBuilder<JsonAny>();
        }
        else
        {
            builder = asArray.AsImmutableListBuilder();
        }

        int builderIndex = 0;
        int index = 0;
        foreach (JsonAny item in asArray.EnumerateArray())
        {
            if (terminateEntireWalkApplyingChanges)
            {
                if (asArray.HasJsonElementBacking)
                {
                    builder.Add(item);
                    index++;
                    builderIndex++;
                    continue;
                }
                else
                {
                    break;
                }
            }

            // Build the array path
            int digits = index == 0 ? 1 : (int)Math.Floor(Math.Log10(index)) + 1;
            int desiredLength = path.Length + digits + 1;
            TryExtendBuffer(ref pathBuffer, desiredLength);
            Span<char> itemPath = pathBuffer.AsSpan(0, desiredLength);
            path.CopyTo(itemPath);
            itemPath[path.Length] = '/';

            index.TryFormat(itemPath[(path.Length + 1)..], out int digitsWritten);

            // Visit the array item, and determine whether we've transformed it.
            Visit(itemPath, item.AsAny, visitor, pathBuffer, ref result);

            if (result.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
            {
                // We didn't transform any items, and we are baling out right now
                result.Output = asArray;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                return;
            }

            if (result.Walk == Walk.TerminateAtThisNodeAndKeepChanges)
            {
                terminateEntireWalkApplyingChanges = true;

                // We still need to add the property which will occur on the fall through path below.
            }

            hasTransformedItems = hasTransformedItems || result.IsTransformed;

            // We need to build up the set of items, whether we have transformed them or not
            if (index < builder.Count)
            {
                if (result.IsTransformed)
                {
                    if (result.Walk != Walk.RemoveAndContinue)
                    {
                        builder[builderIndex] = result.Output;
                        ++builderIndex;
                    }
                    else
                    {
                        builder.RemoveAt(index);
                    }
                }
            }
            else
            {
                if (result.Walk != Walk.RemoveAndContinue)
                {
                    builder.Add(result.Output);
                    ++builderIndex;
                }
            }

            ++index;
        }

        if (terminateEntireWalkApplyingChanges)
        {
            if (hasTransformedItems)
            {
                // We transformed at least one property, so we have to build a new value from the property
                // set we created
                result.Output = new JsonAny(builder.ToImmutable());
                result.Transformed = Transformed.Yes;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                return;
            }
            else
            {
                result.Output = asArray;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                return;
            }
        }

        if (hasTransformedItems)
        {
            // We transformed at least one property, so we have to build a new value from the property
            // set we created
            result.Output = new JsonAny(builder.ToImmutable());
            result.Transformed = Transformed.Yes;
            result.Walk = Walk.Continue;
            return;
        }

        result.Output = asArray;
        result.Transformed = Transformed.No;
        result.Walk = Walk.Continue;
    }

    private static void VisitObject(ReadOnlySpan<char> path, in JsonObject asObject, Visitor visitor, char[] pathBuffer, ref VisitResult result)
    {
        bool hasTransformedProperties = false;
        bool terminateEntireWalkApplyingChanges = false;
        ImmutableList<JsonObjectProperty>.Builder builder;

        // We have two separate strategies in play.
        // If we have a JsonElement backing, and we are going to mutate the object,
        // we need to build up a copy of the object as we mutate it, so we use
        // an empty immutable dictionary builder.
        // If we *already* have a ImmutableDictionary backing, it is more efficient
        // to mutate the existing copy using the .ToBuilder() method.
        if (asObject.HasJsonElementBacking)
        {
            builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        }
        else
        {
            builder = asObject.AsPropertyBacking().ToBuilder();
        }

        foreach (JsonObjectProperty property in asObject.EnumerateObject())
        {
            if (terminateEntireWalkApplyingChanges)
            {
                if (asObject.HasJsonElementBacking)
                {
                    builder.Add(property);
                    continue;
                }
                else
                {
                    break;
                }
            }

            // Stash the name and value as these may allocated with the current System.Text.JsonProperty
            JsonPropertyName propertyName = property.Name;
            JsonAny propertyValue = property.Value;

            // Build the property path
            int desiredLength = ExtendBufferAndCopy(ref pathBuffer, path, propertyName);
            Span<char> propertyPath = pathBuffer.AsSpan(0, desiredLength);

            // Visit the property, and determine whether we've transformed it.
            Visit(propertyPath, propertyValue, visitor, pathBuffer, ref result);
            if (result.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
            {
                // We didn't transform any properties, and we are bailing out right now
                result.Output = asObject;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                return;
            }

            if (result.Walk == Walk.TerminateAtThisNodeAndKeepChanges)
            {
                terminateEntireWalkApplyingChanges = true;

                // We need to add our transformed property if applicable, which will happen on the fall-through
            }

            hasTransformedProperties = hasTransformedProperties || result.IsTransformed;

            // We need to build up the set of properties, whether we have transformed them or not
            if (result.Walk != Walk.RemoveAndContinue)
            {
                builder.Add(new JsonObjectProperty(propertyName, result.Output));
            }
            else
            {
                builder.Remove(propertyName);
            }
        }

        if (terminateEntireWalkApplyingChanges)
        {
            if (hasTransformedProperties)
            {
                // We transformed at least one property, so we have to build a new value from the property
                // set we created
                result.Output = new JsonAny(builder.ToImmutable());
                result.Transformed = Transformed.Yes;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                return;
            }
            else
            {
                // We didn't transform any properties
                result.Output = asObject;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                return;
            }
        }

        if (hasTransformedProperties)
        {
            // We transformed at least one property, so we have to build a new value from the property
            // set we created
            result.Output = new JsonAny(builder.ToImmutable());
            result.Transformed = Transformed.Yes;
            result.Walk = Walk.Continue;
            return;
        }

        // We didn't transform any properties
        result.Output = asObject;
        result.Transformed = Transformed.No;
        result.Walk = Walk.Continue;
        return;
    }

    private static int ExtendBufferAndCopy(ref char[] pathBuffer, ReadOnlySpan<char> path, JsonPropertyName name)
    {
        int desiredLength = path.Length + name.EstimateCharLength() + 1;
        TryExtendBuffer(ref pathBuffer, desiredLength);
        path.CopyTo(pathBuffer);
        pathBuffer[path.Length] = '/';
        if (name.TryCopyTo(pathBuffer.AsMemory(path.Length + 1), out int nameLength))
        {
            desiredLength = path.Length + nameLength + 1;
        }
        else
        {
            desiredLength = path.Length + nameLength + 1;
            TryExtendBuffer(ref pathBuffer, desiredLength);
            if (!name.TryCopyTo(pathBuffer.AsMemory(path.Length + 1), out _))
            {
                throw new InvalidOperationException("Unable to extend the buffer successfully");
            }
        }

        return desiredLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TryExtendBuffer(ref char[] propertyPathBuffer, int desiredLength)
    {
        int length = propertyPathBuffer.Length;
        if (length < desiredLength)
        {
            ArrayPool<char>.Shared.Return(propertyPathBuffer, true);
            propertyPathBuffer = ArrayPool<char>.Shared.Rent(Math.Max(desiredLength, length + BufferChunkSize));
        }
    }
}
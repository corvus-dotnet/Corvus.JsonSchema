// <copyright file="JsonTransformingVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Visitor;
using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
    /// <returns>The result of visiting the node.</returns>
    public delegate VisitResult Visitor(ReadOnlySpan<char> path, in JsonAny nodeToVisit);

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
            JsonAny rootAny = root.AsAny;
            VisitResult visitResult = Visit(ReadOnlySpan<char>.Empty, rootAny, visitor, ref pathBuffer);

            if (visitResult.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
            {
                result = rootAny;
            }
            else
            {
                result = visitResult.Output;
            }

            return visitResult.IsTransformed;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(pathBuffer);
        }
    }

    private static VisitResult Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, Visitor visitor, ref char[] pathBuffer)
    {
        // First, visit the entity itself
        VisitResult rootResult = visitor(path, nodeToVisit);
        Walk rootResultWalk = rootResult.Walk;

        if (rootResultWalk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
        {
            // We're terminating, and abandoning changes, so just return this node.
#pragma warning disable SA1000 // Keywords should be spaced correctly
            return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
#pragma warning restore SA1000 // Keywords should be spaced correctly
        }

        if (rootResultWalk == Walk.SkipChildren)
        {
            // Don't iterate into the children, but do continue the walk.
#pragma warning disable SA1000 // Keywords should be spaced correctly
            return new(nodeToVisit, Transformed.No, Walk.Continue);
#pragma warning restore SA1000 // Keywords should be spaced correctly
        }

        // If we are terminating here, don't visit the children.
        if (rootResultWalk != Walk.Continue)
        {
            return rootResult;
        }

        JsonAny rootResultOutput = rootResult.Output;
        return rootResultOutput.ValueKind switch
        {
            JsonValueKind.Object => VisitObject(path, rootResultOutput, visitor, ref pathBuffer),
            JsonValueKind.Array => VisitArray(path, rootResultOutput, visitor, ref pathBuffer),
            _ => rootResult,
        };
    }

    private static VisitResult VisitArray(ReadOnlySpan<char> path, in JsonAny asArray, Visitor visitor, ref char[] pathBuffer)
    {
        bool terminateEntireWalkApplyingChanges = false;
        bool hasTransformedItems = false;
        ImmutableList<JsonAny>.Builder builder;

        if (asArray.HasJsonElement)
        {
            builder = ImmutableList.CreateBuilder<JsonAny>();
        }
        else
        {
            builder = asArray.AsItemsList.ToBuilder();
        }

        int index = 0;
        foreach (JsonAny item in asArray.EnumerateArray())
        {
            if (terminateEntireWalkApplyingChanges)
            {
                if (asArray.HasJsonElement)
                {
                    builder.Add(item);
                    index++;
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

#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly
            index.TryFormat(itemPath[(path.Length + 1)..], out int digitsWritten);
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly

            // Visit the array item, and determine whether we've transformed it.
            VisitResult itemResult = Visit(itemPath, item, visitor, ref pathBuffer);
            if (itemResult.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
            {
                // We didn't transform any items, and we are baling out right now
                return new VisitResult(asArray, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
            }

            if (itemResult.Walk == Walk.TerminateAtThisNodeAndKeepChanges)
            {
                terminateEntireWalkApplyingChanges = true;

                // We still need to add the property which will occur on the fall through path below.
            }

            hasTransformedItems = hasTransformedItems || itemResult.IsTransformed;

            // We need to build up the set of items, whether we have transformed them or not
            if (index < builder.Count)
            {
                if (itemResult.IsTransformed)
                {
                    builder[index] = itemResult.Output;
                }
            }
            else
            {
                builder.Add(itemResult.Output);
            }

            ++index;
        }

        if (terminateEntireWalkApplyingChanges)
        {
            if (hasTransformedItems)
            {
                // We transformed at least one property, so we have to build a new value from the property
                // set we created
                return new VisitResult(new JsonAny(builder.ToImmutable()), Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }
            else
            {
                // We didn't transform any properties
                return new VisitResult(asArray, Transformed.No, Walk.TerminateAtThisNodeAndKeepChanges);
            }
        }

        if (hasTransformedItems)
        {
            // We transformed at least one property, so we have to build a new value from the property
            // set we created
            return new VisitResult(new JsonAny(builder.ToImmutable()), Transformed.Yes, Walk.Continue);
        }

        // We didn't transform any properties
        return new VisitResult(asArray, Transformed.No, Walk.Continue);
    }

    private static VisitResult VisitObject(ReadOnlySpan<char> path, JsonAny asObject, Visitor visitor, ref char[] pathBuffer)
    {
        bool hasTransformedProperties = false;
        bool terminateEntireWalkApplyingChanges = false;
        ImmutableDictionary<string, JsonAny>.Builder builder;

        if (asObject.HasJsonElement)
        {
            builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        }
        else
        {
            builder = asObject.AsPropertyDictionary.ToBuilder();
        }

        foreach (Property property in asObject.EnumerateObject())
        {
            if (terminateEntireWalkApplyingChanges)
            {
                if (asObject.HasJsonElement)
                {
                    builder[property.Name] = property.Value;
                    continue;
                }
                else
                {
                    break;
                }
            }

            // Build the property path
            string propertyName = property.Name;
            int desiredLength = path.Length + propertyName.Length + 1;
            TryExtendBuffer(ref pathBuffer, desiredLength);
            Span<char> propertyPath = pathBuffer.AsSpan(0, desiredLength);
            path.CopyTo(propertyPath);
            propertyPath[path.Length] = '/';

#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly
            propertyName.CopyTo(propertyPath[(path.Length + 1)..]);
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly

            // Visit the property, and determine whether we've transformed it.
            VisitResult propertyResult = Visit(propertyPath, property.Value, visitor, ref pathBuffer);
            if (propertyResult.Walk == Walk.TerminateAtThisNodeAndAbandonAllChanges)
            {
                // We didn't transform any properties, and we are baling out right now
                return new VisitResult(asObject, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
            }

            if (propertyResult.Walk == Walk.TerminateAtThisNodeAndKeepChanges)
            {
                terminateEntireWalkApplyingChanges = true;

                // We need to add our transformed property if applicable, which will happen on the fall-through
            }

            hasTransformedProperties = hasTransformedProperties || propertyResult.IsTransformed;

            // We need to build up the set of properties, whether we have transformed them or not
            builder[property.Name] = propertyResult.Output;
        }

        if (terminateEntireWalkApplyingChanges)
        {
            if (hasTransformedProperties)
            {
                // We transformed at least one property, so we have to build a new value from the property
                // set we created
                return new VisitResult(new JsonAny(builder.ToImmutable()), Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }
            else
            {
                // We didn't transform any properties
                return new VisitResult(asObject, Transformed.No, Walk.TerminateAtThisNodeAndKeepChanges);
            }
        }

        if (hasTransformedProperties)
        {
            // We transformed at least one property, so we have to build a new value from the property
            // set we created
            return new VisitResult(new JsonAny(builder.ToImmutable()), Transformed.Yes, Walk.Continue);
        }

        // We didn't transform any properties
        return new VisitResult(asObject, Transformed.No, Walk.Continue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TryExtendBuffer(ref char[] propertyPathBuffer, int desiredLength)
    {
        if (propertyPathBuffer.Length < desiredLength)
        {
            ArrayPool<char>.Shared.Return(propertyPathBuffer);
            propertyPathBuffer = ArrayPool<char>.Shared.Rent(Math.Max(desiredLength, propertyPathBuffer.Length + BufferChunkSize));
        }
    }
}
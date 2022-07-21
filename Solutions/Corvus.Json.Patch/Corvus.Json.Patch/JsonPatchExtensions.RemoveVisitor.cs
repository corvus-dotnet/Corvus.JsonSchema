// <copyright file="JsonPatchExtensions.RemoveVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// JSON Patch Extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct RemoveVisitor
    {
        public RemoveVisitor(Remove patchOperation)
        {
            this.Path = patchOperation.Path;
        }

        public string Path { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VisitResult Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit)
        {
            return VisitForRemove(path, nodeToVisit, this.Path);
        }

        // This is used by Remove and Move
        internal static VisitResult VisitForRemove(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ReadOnlySpan<char> operationPath)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    // We are an exact match, but we should have found that in the parent; we can't remove ourselves.
                    return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                }

                JsonValueKind nodeToVisitValueKind = nodeToVisit.ValueKind;

                if (nodeToVisitValueKind == JsonValueKind.Object)
                {
                    // We are an object, so we need to see if the rest of the path represents a property.
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> propertyName))
                    {
                        // Add does not permit us to replace a property that already exists (that's what Replace is for)
                        if (!nodeToVisit.HasProperty(propertyName))
                        {
                            // So we don't transform, and we abandon the walk at this point.
                            return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                        }

                        // Return the transformed result, and stop walking the tree here.
                        return new(nodeToVisit.RemoveProperty(propertyName), Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                    }

                    // The path element wasn't a terminus, but it could still be a deeper property, so let's continue the walk
                    return new(nodeToVisit, Transformed.No, Walk.Continue);
                }

                if (nodeToVisitValueKind == JsonValueKind.Array)
                {
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> itemIndex))
                    {
                        int arrayLength = nodeToVisit.GetArrayLength();

                        if (TryGetArrayIndex(itemIndex, out int index) && index < arrayLength)
                        {
                            return RemoveNode(index, in nodeToVisit);
                        }

                        // The index wasn't in the correct form (either because it was past the end, or not in an index format)
                        return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                    }

                    // The path element wasn't a terminus, but it could still be a deeper walk into an indexed element, so let's continue the walk
                    return new(nodeToVisit, Transformed.No, Walk.Continue);
                }

                // The parent entity wasn't an object or an array, so it can't be removed from; this is an error.
                return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            return new(nodeToVisit, Transformed.No, Walk.SkipChildren);

            static VisitResult RemoveNode(int index, in JsonAny arrayNode)
            {
                JsonAny returnNode = arrayNode.RemoveAt(index);
                return new(returnNode, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }
        }
    }
}
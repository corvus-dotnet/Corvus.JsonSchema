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
        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            VisitForRemove(path, nodeToVisit, this.Path, ref result);
        }

        // This is used by Remove and Move
        internal static void VisitForRemove(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ReadOnlySpan<char> operationPath, ref VisitResult result)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    // We are an exact match, but we should have found that in the parent; we can't remove ourselves.
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                    return;
                }

                VisitForRemoveCore(path, nodeToVisit, operationPath, ref result);
                return;
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            result.Output = nodeToVisit;
            result.Transformed = Transformed.No;
            result.Walk = Walk.SkipChildren;

            static void RemoveNode(int index, in JsonAny arrayNode, ref VisitResult result)
            {
                result.Output = arrayNode.RemoveAt(index);
                result.Transformed = Transformed.Yes;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
            }

            static void VisitForRemoveCore(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ReadOnlySpan<char> operationPath, ref VisitResult result)
            {
                if (nodeToVisit.ValueKind == JsonValueKind.Object)
                {
                    // We are an object, so we need to see if the rest of the path represents a property.
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> propertyName))
                    {
                        // Add does not permit us to replace a property that already exists (that's what Replace is for)
                        if (!nodeToVisit.HasProperty(propertyName))
                        {
                            // So we don't transform, and we abandon the walk at this point.
                            result.Output = nodeToVisit;
                            result.Transformed = Transformed.No;
                            result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                            return;
                        }

                        // Return the transformed result, and stop walking the tree here.
                        result.Output = nodeToVisit.RemoveProperty(propertyName);
                        result.Transformed = Transformed.Yes;
                        result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                        return;
                    }

                    // The path element wasn't a terminus, but it could still be a deeper property, so let's continue the walk
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.Continue;
                    return;
                }

                if (nodeToVisit.ValueKind == JsonValueKind.Array)
                {
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> itemIndex))
                    {
                        int arrayLength = nodeToVisit.GetArrayLength();

                        if (TryGetArrayIndex(itemIndex, out int index) && index < arrayLength)
                        {
                            RemoveNode(index, in nodeToVisit, ref result);
                            return;
                        }

                        // The index wasn't in the correct form (either because it was past the end, or not in an index format)
                        result.Output = nodeToVisit;
                        result.Transformed = Transformed.No;
                        result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                        return;
                    }

                    // The path element wasn't a terminus, but it could still be a deeper walk into an indexed element, so let's continue the walk
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.Continue;
                    return;
                }

                // The parent entity wasn't an object or an array, so it can't be removed from; this is an error.
                result.Output = nodeToVisit;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                return;
            }
        }
    }
}
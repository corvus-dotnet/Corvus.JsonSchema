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
        public RemoveVisitor(in JsonPatchDocument.RemoveEntity patchOperation)
        {
            this.Path = (string)patchOperation.Path;
            this.BeginTerminator = this.Path.LastIndexOf('/') + 1;
        }

        public RemoveVisitor(string path)
        {
            this.Path = path;
            this.BeginTerminator = this.Path.LastIndexOf('/') + 1;
        }

        public string Path { get; }

        public int BeginTerminator { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            ReadOnlySpan<char> span = this.Path.AsSpan();
            VisitForRemove(path, nodeToVisit, span, span[this.BeginTerminator..], ref result);
        }

        // This is used by Remove and Move
        internal static void VisitForRemove(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ReadOnlySpan<char> operationPath, ReadOnlySpan<char> terminatingPathElement, ref VisitResult result)
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
                else if (operationPath[path.Length] != '/')
                {
                    // If our next character is not a path separator, then we must have a partial node match, and we need to skip on to the next sibling.
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.SkipChildren;
                    return;
                }

                VisitForRemoveCore(path, nodeToVisit, operationPath, terminatingPathElement, ref result);
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

            static void VisitForRemoveCore(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ReadOnlySpan<char> operationPath, ReadOnlySpan<char> terminatingPathElement, ref VisitResult result)
            {
                if (path.Length + terminatingPathElement.Length + 1 != operationPath.Length)
                {
                    // The path element wasn't a terminus, but it could still be a deeper property, so let's continue the walk
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.Continue;
                    return;
                }
                else if (operationPath[path.Length] != '/')
                {
                    // If our next character is not a path separator, then we must have a partial node match, and we need to skip on to the next sibling.
                    result.Output = nodeToVisit;
                    result.Transformed = Transformed.No;
                    result.Walk = Walk.SkipChildren;
                    return;
                }

                if (nodeToVisit.ValueKind == JsonValueKind.Object)
                {
                    // Add does not permit us to replace a property that already exists (that's what Replace is for)
                    if (!nodeToVisit.HasProperty(terminatingPathElement))
                    {
                        // So we don't transform, and we abandon the walk at this point.
                        result.Output = nodeToVisit;
                        result.Transformed = Transformed.No;
                        result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                        return;
                    }

                    // Return the transformed result, and stop walking the tree here.
                    result.Output = nodeToVisit.RemoveProperty(terminatingPathElement);
                    result.Transformed = Transformed.Yes;
                    result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
                    return;
                }

                if (nodeToVisit.ValueKind == JsonValueKind.Array)
                {
                    int arrayLength = nodeToVisit.GetArrayLength();

                    if (TryGetArrayIndex(terminatingPathElement, out int index) && index < arrayLength)
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

                // The parent entity wasn't an object or an array, so it can't be removed from; this is an error.
                result.Output = nodeToVisit;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                return;
            }
        }
    }
}
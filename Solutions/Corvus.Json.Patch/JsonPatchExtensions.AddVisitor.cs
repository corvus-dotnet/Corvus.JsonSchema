// <copyright file="JsonPatchExtensions.AddVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;
using System.Text.Json;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// Json Patch implementation details.
/// </summary>
public static partial class JsonPatchExtensions
{
    private class AddVisitor
    {
        public AddVisitor(Add patchOperation)
        {
            this.PatchOperation = patchOperation;
        }

        public Add PatchOperation { get; }

        public VisitResult Visit(ReadOnlySpan<char> path, JsonAny nodeToVisit)
        {
            return VisitForAdd(path, nodeToVisit, this.PatchOperation.Value, this.PatchOperation.Path.AsSpan());
        }

        // This is used by AddVisitor, CopyVistor and MoveVisitor
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
        internal static VisitResult VisitForAdd(ReadOnlySpan<char> path, JsonAny nodeToVisit, JsonAny? value, ReadOnlySpan<char> operationPath)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    if (!value.HasValue)
                    {
                        return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                    }

                    return new(value.Value, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                }

                if (nodeToVisit.ValueKind == JsonValueKind.Object)
                {
                    // We are an object, so we need to see if the rest of the path represents a property.
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> propertyName))
                    {
                        if (!value.HasValue)
                        {
                            return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                        }

                        // Return the transformed result, and stop walking the tree here.
                        return new(nodeToVisit.SetProperty(propertyName, value.Value), Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                    }

                    // The path element wasn't a terminus, but it could still be a deeper property, so let's continue the walk
                    return new(nodeToVisit, Transformed.No, Walk.Continue);
                }

                if (nodeToVisit.ValueKind == JsonValueKind.Array)
                {
                    JsonArray arrayNode = nodeToVisit.AsArray;

                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> itemIndex))
                    {
                        if (!value.HasValue)
                        {
                            return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                        }

                        int arrayLength = arrayNode.Length;

                        if (itemIndex[0] == '-')
                        {
                            if (itemIndex.Length == 1)
                            {
                                // We got the '-' which means add it at the end
                                return AddNodeAtEnd(arrayNode, value.Value);
                            }
                            else
                            {
                                return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                            }
                        }

                        if (TryGetArrayIndex(itemIndex, out int index))
                        {
                            // You can specify the end explicitly
                            if (index == arrayLength)
                            {
                                return AddNodeAtEnd(in arrayNode, value.Value);
                            }

                            if (index < arrayLength)
                            {
                                return InsertNode(index, in arrayNode, value.Value);
                            }
                        }

                        // The index wasn't in the correct form (either because it was past the end, or not in an index format)
                        return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
                    }

                    // The path element wasn't a terminus, but it could still be a deeper walk into an indexed element, so let's continue the walk
                    return new(nodeToVisit, Transformed.No, Walk.Continue);
                }

                // The parent entity wasn't an object or an array, so it can't be added to; this is an error.
                return new(nodeToVisit, Transformed.No, Walk.TerminateAtThisNodeAndAbandonAllChanges);
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            return new(nodeToVisit, Transformed.No, Walk.SkipChildren);

            static VisitResult AddNodeAtEnd(in JsonArray arrayNode, JsonAny node)
            {
                JsonArray returnNode = arrayNode.Add(node);
                return new(returnNode, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }

            static VisitResult InsertNode(int index, in JsonArray arrayNode, JsonAny node)
            {
                JsonArray returnNode = arrayNode.Insert(index, node);
                return new(returnNode, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }
        }
    }
}

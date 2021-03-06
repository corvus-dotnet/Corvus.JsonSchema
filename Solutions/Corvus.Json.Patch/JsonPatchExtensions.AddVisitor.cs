// <copyright file="JsonPatchExtensions.AddVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// Json Patch implementation details.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct AddVisitor
    {
        public JsonAny Value { get; }

        public string Path { get; }

        public AddVisitor(Add patchOperation)
        {
            this.Value = patchOperation.Value;
            this.Path = patchOperation.Path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VisitResult Visit(ReadOnlySpan<char> path, JsonAny nodeToVisit)
        {
            return VisitForAdd(path, nodeToVisit, this.Value, this.Path);
        }

        // This is used by AddVisitor, CopyVistor and MoveVisitor
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
        internal static VisitResult VisitForAdd(ReadOnlySpan<char> path, in JsonAny nodeToVisit, in JsonAny value, ReadOnlySpan<char> operationPath)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    return new(value, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                }
                else if (operationPath[path.Length] != '/')
                {
                    // If our next character is not a path separator, then we must have a partial node match, and we need to skip on to the next sibling.
                    return new(nodeToVisit, Transformed.No, Walk.SkipChildren);
                }

                JsonValueKind nodeToVisitValueKind = nodeToVisit.ValueKind;

                if (nodeToVisitValueKind == JsonValueKind.Object)
                {
                    // We are an object, so we need to see if the rest of the path represents a property.
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> propertyName))
                    {
                        // Return the transformed result, and stop walking the tree here.
                        return new(nodeToVisit.SetProperty(propertyName, value), Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                    }

                    // The path element wasn't a terminus, but it could still be a deeper property, so let's continue the walk
                    return new(nodeToVisit, Transformed.No, Walk.Continue);
                }

                if (nodeToVisitValueKind == JsonValueKind.Array)
                {
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> itemIndex))
                    {
                        int arrayLength = nodeToVisit.Length;

                        if (itemIndex[0] == '-')
                        {
                            if (itemIndex.Length == 1)
                            {
                                // We got the '-' which means add it at the end
                                return AddNodeAtEnd(nodeToVisit, value);
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
                                return AddNodeAtEnd(in nodeToVisit, value);
                            }

                            if (index < arrayLength)
                            {
                                return InsertNode(index, in nodeToVisit, value);
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

            static VisitResult AddNodeAtEnd(in JsonAny arrayNode, in JsonAny node)
            {
                JsonAny returnNode = arrayNode.Add(node);
                return new(returnNode, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }

            static VisitResult InsertNode(int index, in JsonAny arrayNode, in JsonAny node)
            {
                JsonAny returnNode = arrayNode.Insert(index, node);
                return new(returnNode, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
            }
        }
    }
}

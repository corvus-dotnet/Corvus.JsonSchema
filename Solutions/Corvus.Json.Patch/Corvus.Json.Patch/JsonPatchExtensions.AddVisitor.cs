// <copyright file="JsonPatchExtensions.AddVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// Json Patch implementation details.
/// </summary>
public static partial class JsonPatchExtensions
{
    private readonly struct AddVisitor
    {
        public AddVisitor(Add patchOperation)
        {
            this.Value = patchOperation.Value;
            this.Path = patchOperation.Path;
        }

        public JsonAny Value { get; }

        public string Path { get; }

        public void Visit(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
        {
            VisitForAdd(path, nodeToVisit, this.Value, this.Path, ref result);
        }

        // This is used by AddVisitor, CopyVistor and MoveVisitor
        internal static void VisitForAdd(ReadOnlySpan<char> path, in JsonAny nodeToVisit, in JsonAny value, ReadOnlySpan<char> operationPath, ref VisitResult result)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    result.Output = value;
                    result.Transformed = Transformed.Yes;
                    result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
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

                JsonValueKind nodeToVisitValueKind = nodeToVisit.ValueKind;

                if (nodeToVisitValueKind == JsonValueKind.Object)
                {
                    // We are an object, so we need to see if the rest of the path represents a property.
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> propertyName))
                    {
                        // Return the transformed result, and stop walking the tree here.
                        result.Output = nodeToVisit.SetProperty(propertyName, value);
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

                if (nodeToVisitValueKind == JsonValueKind.Array)
                {
                    if (TryGetTerminatingPathElement(operationPath[path.Length..], out ReadOnlySpan<char> itemIndex))
                    {
                        int arrayLength = nodeToVisit.GetArrayLength();

                        if (itemIndex[0] == '-')
                        {
                            if (itemIndex.Length == 1)
                            {
                                // We got the '-' which means add it at the end
                                AddNodeAtEnd(nodeToVisit, value, ref result);
                                return;
                            }
                            else
                            {
                                result.Output = nodeToVisit;
                                result.Transformed = Transformed.No;
                                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                                return;
                            }
                        }

                        if (TryGetArrayIndex(itemIndex, out int index))
                        {
                            // You can specify the end explicitly
                            if (index == arrayLength)
                            {
                                AddNodeAtEnd(nodeToVisit, value, ref result);
                                return;
                            }

                            if (index < arrayLength)
                            {
                                InsertNode(index, nodeToVisit, value, ref result);
                                return;
                            }
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

                // The parent entity wasn't an object or an array, so it can't be added to; this is an error.
                result.Output = nodeToVisit;
                result.Transformed = Transformed.No;
                result.Walk = Walk.TerminateAtThisNodeAndAbandonAllChanges;
                return;
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            result.Output = nodeToVisit;
            result.Transformed = Transformed.No;
            result.Walk = Walk.SkipChildren;
            return;

            static void AddNodeAtEnd(in JsonAny arrayNode, in JsonAny node, ref VisitResult result)
            {
                result.Output = arrayNode.Add(node);
                result.Transformed = Transformed.Yes;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
            }

            static void InsertNode(int index, in JsonAny arrayNode, in JsonAny node, ref VisitResult result)
            {
                result.Output = arrayNode.Insert(index, node);
                result.Transformed = Transformed.Yes;
                result.Walk = Walk.TerminateAtThisNodeAndKeepChanges;
            }
        }
    }
}